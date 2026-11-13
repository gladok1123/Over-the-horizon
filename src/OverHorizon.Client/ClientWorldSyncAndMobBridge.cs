using OverHorizon.Audio;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Inventory;
using OverHorizon.Gameplay.Mobs;
using OverHorizon.Gameplay.Physics;
using OverHorizon.Gameplay.Redstone;
using OverHorizon.Gameplay.Survival;
using OverHorizon.Rendering;
using OverHorizon.Rendering.Hud;
using OverHorizon.World;
using OverHorizon.World.Lighting;
using OverHorizon.World.Storage;

namespace OverHorizon.Client;

/// <summary>
/// Coordinates concentric Chebyshev chunk-ring snapshot synchronization, <see cref="WorldTimeCycle"/>,
/// <see cref="RedstoneEngine"/> + Pistons, <see cref="PrimedTntAndWorldTicker"/> (TNT craters, falling Sand/Gravel,
/// flowing Water, Grass spread), attachment support detachment, and 3D peaceful/hostile mob simulation + audio.
/// </summary>
internal sealed class ClientWorldSyncAndMobBridge
{
    private static readonly Direction[] SixNeighbors =
    [
        Direction.Down, Direction.Up, Direction.North, Direction.South, Direction.West, Direction.East,
    ];

    private readonly IBlockRegistry _blocks;
    private readonly IRegistry<ItemDefinition> _items;
    private readonly ClientWorldCache _worldCache;
    private readonly IRenderSubsystem _renderer;
    private readonly IAudioSubsystem _audio;
    private readonly WorldMobManager _mobManager;
    private readonly List<WorldMobVisual> _mobVisualScratch = new(36);
    private readonly List<PrimedTntVisual> _tntVisualScratch = new(16);
    private readonly List<BlockPos> _ignitedTntScratch = new(8);
    private readonly List<(BlockPos Pos, RedstoneSoundKind Sound)> _redstoneSoundScratch = new(8);
    private readonly List<Vec3d> _detonatedScratch = new(8);
    private readonly List<Vec3d> _creeperDetonations = new(4);
    private readonly HashSet<ChunkPos> _touchedChunksScratch = [];
    private readonly List<BlockPos> _editedPositionsScratch = new(32);

    private IWorldSubsystem _localWorld;
    private double _ambientMobSoundTimer;
    private int _rollingSyncRingCursor = 9;
    private int _dropSeq;

    public ClientWorldSyncAndMobBridge(
        IBlockRegistry blocks,
        IRegistry<ItemDefinition> items,
        IWorldSubsystem localWorld,
        ClientWorldCache worldCache,
        IRenderSubsystem renderer,
        IAudioSubsystem audio,
        WorldMobManager mobManager)
    {
        _blocks = blocks;
        _items = items;
        _localWorld = localWorld;
        _worldCache = worldCache;
        _renderer = renderer;
        _audio = audio;
        _mobManager = mobManager;
        Redstone = new RedstoneEngine(blocks);
        WorldTicker = new PrimedTntAndWorldTicker(blocks);
    }

    public IWorldSubsystem ActiveLocalWorld => _localWorld;

    public WorldTimeCycle TimeCycle { get; } = new();

    public RedstoneEngine Redstone { get; }

    public PrimedTntAndWorldTicker WorldTicker { get; }

    public void SetActiveLocalWorld(IWorldSubsystem nextWorld, long initialTimeTicks)
    {
        _localWorld = nextWorld;
        TimeCycle.SetTime(initialTimeTicks);
        _mobManager.Clear();
    }

    public void SeedInitialStarterMobs()
    {
        (int Dx, int Dz, MobType Type)[] seeds =
        [
            (9, 6, MobType.Sheep),
            (-10, 7, MobType.Cow),
            (7, -11, MobType.Pig),
            (-8, -9, MobType.Chicken),
        ];

        for (var i = 0; i < seeds.Length; i++)
        {
            var bp = new BlockPos(seeds[i].Dx, 0, seeds[i].Dz);
            if (_localWorld.Chunks.TryGetSnapshot(bp.ToChunkPos(), out var snap))
            {
                var sy = snap.GetHeight(HeightmapType.MotionBlocking, bp.SectionLocalX, bp.SectionLocalZ);
                if (sy > 63)
                {
                    _ = _mobManager.SpawnMob(seeds[i].Type, new Vec3d(seeds[i].Dx + 0.5, sy, seeds[i].Dz + 0.5), i * 85.0f);
                }
            }
        }
    }

    public bool TryInteractRedstoneBlock(BlockPos pos)
    {
        _editedPositionsScratch.Clear();
        _touchedChunksScratch.Clear();
        _ignitedTntScratch.Clear();
        _redstoneSoundScratch.Clear();

        var interacted = Redstone.TryInteractComponent(pos, _localWorld.GetBlockState, RecordWorldSet);
        if (!interacted)
        {
            return false;
        }

        Redstone.EvaluateImmediate(_localWorld.GetBlockState, RecordWorldSet, _ignitedTntScratch, _redstoneSoundScratch);
        ProcessRedstoneOutputsAndRemesh();
        return true;
    }

    public bool ApplyLocalBlockChangeWithCascade(
        BlockPos primaryPos,
        BlockState primaryState,
        PlayerInventory? dropDetachedItemsToInventory)
    {
        if (!_localWorld.SetBlockState(primaryPos, primaryState))
        {
            return false;
        }

        _editedPositionsScratch.Clear();
        _touchedChunksScratch.Clear();
        _ignitedTntScratch.Clear();
        _redstoneSoundScratch.Clear();

        TrackEditedBlock(primaryPos);
        WorldTicker.NotifyWorldBlockChanged(primaryPos, _localWorld.GetBlockState, RecordWorldSet);

        // Detach unsupported neighbor Torches, Ladders, Redstone components, Rails, or Bed halves!
        var checkQueue = new Queue<BlockPos>();
        checkQueue.Enqueue(primaryPos);
        while (checkQueue.Count > 0)
        {
            var cur = checkQueue.Dequeue();
            for (var i = 0; i < SixNeighbors.Length; i++)
            {
                var nPos = cur.Offset(SixNeighbors[i]);
                var nState = _localWorld.GetBlockState(nPos);
                if (nState.IsAir) continue;

                var nKey = _blocks.GetStateMetadata(nState).BlockKey;
                if (!BlockAttachmentAndShapeRules.IsStillSupported(_blocks, _localWorld.GetBlockState, nPos, nState) &&
                    _localWorld.SetBlockState(nPos, BlockState.Air))
                {
                    TrackEditedBlock(nPos);
                    Redstone.NotifyBlockChanged(nPos);
                    WorldTicker.NotifyWorldBlockChanged(nPos, _localWorld.GetBlockState, RecordWorldSet);
                    checkQueue.Enqueue(nPos);

                    if (dropDetachedItemsToInventory is not null)
                    {
                        var drop = BlockDropResolver.ResolveDrop(nKey, heldTool: null, _items, _dropSeq++);
                        if (!drop.IsEmpty)
                        {
                            _ = dropDetachedItemsToInventory.AddItem(drop, _items);
                        }
                    }
                }
            }
        }

        Redstone.NotifyBlockChanged(primaryPos);
        Redstone.EvaluateImmediate(_localWorld.GetBlockState, RecordWorldSet, _ignitedTntScratch, _redstoneSoundScratch);
        ProcessRedstoneOutputsAndRemesh();
        return true;
    }

    public void StepWorldSimulationAndMobs(ClientPlayerController player, float dt)
    {
        TimeCycle.AdvanceSeconds(dt);
        var skyBright = TimeCycle.SkyBrightness;

        _editedPositionsScratch.Clear();
        _touchedChunksScratch.Clear();
        _ignitedTntScratch.Clear();
        _redstoneSoundScratch.Clear();
        _detonatedScratch.Clear();
        _creeperDetonations.Clear();

        // 1. Step Redstone (buttons, pressure plates, repeaters, pistons, TNT ignition)
        var mobCount = _mobManager.Mobs.Count;
        Span<Vec3d> entityFeet = mobCount <= 32 ? stackalloc Vec3d[1 + mobCount] : new Vec3d[1 + mobCount];
        entityFeet[0] = player.FeetPosition;
        for (var i = 0; i < mobCount; i++)
        {
            entityFeet[1 + i] = _mobManager.Mobs[i].FeetPosition;
        }

        Redstone.Step(dt, entityFeet, _localWorld.GetBlockState, RecordWorldSet, _ignitedTntScratch, _redstoneSoundScratch);

        // 2. Step Primed TNT, Gravity Blocks (Sand/Gravel), Water Flow (0..7), and Grass Spread/Decay
        WorldTicker.Step(dt, _worldCache, player.FeetPosition, _localWorld.GetBlockState, _worldCache.GetLight, RecordWorldSet, _detonatedScratch);

        // 3. Step Peaceful & Hostile Mobs (with real daylight burning & Creeper craters!)
        var mobDamage = _mobManager.Step(
            _worldCache,
            ProbeMobSpawnCell,
            player.FeetPosition,
            player.Mode,
            player.Hotbar.TotalArmorDefense,
            skyBright,
            dt,
            _creeperDetonations);

        if (mobDamage > 0.0f)
        {
            _ = player.ApplyDamage(mobDamage, _audio);
        }

        for (var c = 0; c < _creeperDetonations.Count; c++)
        {
            _ = WorldTicker.DetonateRaycastExplosion(_creeperDetonations[c], 3.0f, _localWorld.GetBlockState, RecordWorldSet);
            _detonatedScratch.Add(_creeperDetonations[c]);
        }

        // Apply explosion damage & knockback to Mobs & Survival Player + play Explosion audio!
        for (var e = 0; e < _detonatedScratch.Count; e++)
        {
            var boomCenter = _detonatedScratch[e];
            _audio.PlayPositional(SoundCue.Explosion, boomCenter, gain: 1.0f * player.MasterVolume);
            _ = _mobManager.ApplyExplosionBlast(boomCenter);

            var dist = player.FeetPosition.DistanceTo(boomCenter);
            if (dist < 8.0 && player.Mode == GameMode.Survival)
            {
                var exp = (float)Math.Clamp(1.0 - (dist / 8.0), 0.0, 1.0);
                var dmg = ((exp * exp) + exp) * 14.0f;
                _ = player.ApplyDamage(dmg, _audio);
            }
        }

        ProcessRedstoneOutputsAndRemesh();
        StepAmbientMobAudio(player, dt);
    }

    public IReadOnlyList<WorldMobVisual> BuildMobVisuals(ClientPlayerController player)
    {
        _mobVisualScratch.Clear();
        var mobs = _mobManager.Mobs;
        var heldTorch = player.Hotbar.CombinedHeldLightEmission;
        var skyMul = TimeCycle.SkyBrightness;
        for (var i = 0; i < mobs.Count; i++)
        {
            var m = mobs[i];
            var (sky, blk) = _worldCache.GetLight(m.FeetPosition.ToBlockPos());
            var effSky = (byte)Math.Clamp((int)MathF.Round(sky * skyMul), 0, 15);
            var distToPlayer = (float)m.FeetPosition.DistanceTo(player.Camera.Position);
            var torchAtMob = heldTorch > distToPlayer ? (byte)Math.Clamp((int)MathF.Round(heldTorch - distToPlayer), 0, 15) : (byte)0;
            var eff = LightingEngine.ComputeEffectiveLightLevel(effSky, blk, torchAtMob);
            var bright = LightingEngine.LightLevelToBrightness(eff);
            _mobVisualScratch.Add(new WorldMobVisual(
                m.FeetPosition,
                m.YawDegrees,
                m.WalkPhase,
                (byte)m.Type,
                m.HurtFlashTimer,
                m.CreeperFuseTimer,
                m.IsSheared,
                bright,
                m.IsOnFire));
        }

        return _mobVisualScratch;
    }

    public IReadOnlyList<PrimedTntVisual> BuildPrimedTntVisuals(ClientPlayerController player)
    {
        _tntVisualScratch.Clear();
        var tnts = WorldTicker.PrimedTnts;
        if (tnts.Count == 0) return _tntVisualScratch;

        var skyMul = TimeCycle.SkyBrightness;
        for (var i = 0; i < tnts.Count; i++)
        {
            var t = tnts[i];
            var (sky, blk) = _worldCache.GetLight(t.FeetPosition.ToBlockPos());
            var effSky = (byte)Math.Clamp((int)MathF.Round(sky * skyMul), 0, 15);
            var bright = LightingEngine.LightLevelToBrightness(Math.Max(effSky, blk));
            _ = player;
            _tntVisualScratch.Add(new PrimedTntVisual(t.FeetPosition, t.IsFlashingWhite, bright));
        }

        return _tntVisualScratch;
    }

    public void SyncLocalSnapshots(ChunkPos center, int viewDistance, int? fullImmediateRadius = null)
    {
        var radius = Math.Clamp(viewDistance, 1, 64);
        var immediate = fullImmediateRadius ?? Math.Min(radius, 8);

        for (var ring = 0; ring <= immediate; ring++)
        {
            SyncChebyshevRing(center, ring);
        }

        if (fullImmediateRadius is null && radius > immediate)
        {
            if (_rollingSyncRingCursor <= immediate || _rollingSyncRingCursor > radius)
            {
                _rollingSyncRingCursor = immediate + 1;
            }

            for (var step = 0; step < 6 && _rollingSyncRingCursor <= radius; step++, _rollingSyncRingCursor++)
            {
                SyncChebyshevRing(center, _rollingSyncRingCursor);
            }
        }

        _ = _worldCache.EvictOutOfRangeChunks(center, radius + 3, _renderer);
    }

    private void RecordWorldSet(BlockPos pos, BlockState state)
    {
        if (_localWorld.SetBlockState(pos, state))
        {
            TrackEditedBlock(pos);
            WorldTicker.NotifyWorldBlockChanged(pos, _localWorld.GetBlockState, static (_, _) => { });
        }
    }

    private void TrackEditedBlock(BlockPos pos)
    {
        _editedPositionsScratch.Add(pos);
        var cp = pos.ToChunkPos();
        _ = _touchedChunksScratch.Add(cp);
        if (pos.SectionLocalX == 0) _ = _touchedChunksScratch.Add(new ChunkPos(cp.X - 1, cp.Z));
        else if (pos.SectionLocalX == 15) _ = _touchedChunksScratch.Add(new ChunkPos(cp.X + 1, cp.Z));
        if (pos.SectionLocalZ == 0) _ = _touchedChunksScratch.Add(new ChunkPos(cp.X, cp.Z - 1));
        else if (pos.SectionLocalZ == 15) _ = _touchedChunksScratch.Add(new ChunkPos(cp.X, cp.Z + 1));
    }

    private void ProcessRedstoneOutputsAndRemesh()
    {
        for (var i = 0; i < _ignitedTntScratch.Count; i++)
        {
            var tPos = _ignitedTntScratch[i];
            _ = WorldTicker.SpawnPrimedTnt(tPos);
            _audio.PlayPositional(SoundCue.TntFuse, new Vec3d(tPos.X + 0.5, tPos.Y + 0.5, tPos.Z + 0.5), gain: 0.90f);
        }

        for (var i = 0; i < _redstoneSoundScratch.Count; i++)
        {
            var (sPos, kind) = _redstoneSoundScratch[i];
            var cue = kind switch
            {
                RedstoneSoundKind.PistonExtend => SoundCue.PistonExtend,
                RedstoneSoundKind.PistonRetract => SoundCue.PistonRetract,
                RedstoneSoundKind.TorchFizz => SoundCue.Fizz,
                _ => SoundCue.UiClick,
            };
            _audio.PlayPositional(cue, new Vec3d(sPos.X + 0.5, sPos.Y + 0.5, sPos.Z + 0.5), gain: 0.72f);
        }

        if (_touchedChunksScratch.Count == 0) return;

        foreach (var cp in _touchedChunksScratch)
        {
            if (_localWorld.Chunks.TryGetSnapshot(cp, out var snap))
            {
                _worldCache.UpsertSnapshot(snap);
            }
        }

        if (_editedPositionsScratch.Count <= 12)
        {
            for (var i = 0; i < _editedPositionsScratch.Count; i++)
            {
                _worldCache.RemeshAroundBlockImmediate(_editedPositionsScratch[i], _renderer);
            }
        }
    }

    private void StepAmbientMobAudio(ClientPlayerController player, float dt)
    {
        _ambientMobSoundTimer += dt;
        if (_ambientMobSoundTimer < 4.2 || _mobManager.Mobs.Count == 0) return;
        _ambientMobSoundTimer = 0.0;

        var idx = (int)((uint)Environment.TickCount % (uint)_mobManager.Mobs.Count);
        var m = _mobManager.Mobs[idx];
        if (m.FeetPosition.DistanceTo(player.Camera.Position) > 24.0) return;

        var cue = m.Type switch
        {
            MobType.Pig => SoundCue.MobPigSay,
            MobType.Cow => SoundCue.MobCowSay,
            MobType.Sheep => SoundCue.MobSheepSay,
            MobType.Chicken => SoundCue.MobChickenSay,
            MobType.Zombie => SoundCue.MobZombieSay,
            MobType.Skeleton => SoundCue.MobSkeletonSay,
            MobType.Spider => SoundCue.MobSpiderSay,
            _ => SoundCue.MobCreeperHiss,
        };
        _audio.PlayPositional(cue, m.FeetPosition, gain: 0.65f * player.MasterVolume);
    }

    private void SyncChebyshevRing(ChunkPos center, int ring)
    {
        if (ring == 0)
        {
            if (_localWorld.Chunks.TryGetSnapshot(center, out var cSnap)) _worldCache.UpsertSnapshot(cSnap);
            return;
        }

        for (var dx = -ring; dx <= ring; dx++)
        {
            if (_localWorld.Chunks.TryGetSnapshot(new ChunkPos(center.X + dx, center.Z - ring), out var nSnap)) _worldCache.UpsertSnapshot(nSnap);
            if (_localWorld.Chunks.TryGetSnapshot(new ChunkPos(center.X + dx, center.Z + ring), out var sSnap)) _worldCache.UpsertSnapshot(sSnap);
        }

        for (var dz = -ring + 1; dz <= ring - 1; dz++)
        {
            if (_localWorld.Chunks.TryGetSnapshot(new ChunkPos(center.X - ring, center.Z + dz), out var wSnap)) _worldCache.UpsertSnapshot(wSnap);
            if (_localWorld.Chunks.TryGetSnapshot(new ChunkPos(center.X + ring, center.Z + dz), out var eSnap)) _worldCache.UpsertSnapshot(eSnap);
        }
    }

    private (byte SkyLight, byte BlockLight, bool IsGrassTop, bool IsSolidGround, bool IsClearSpace) ProbeMobSpawnCell(BlockPos feetBlock)
    {
        if (!_worldCache.IsColumnLoaded(feetBlock)) return (15, 0, false, false, false);

        var below = new BlockPos(feetBlock.X, feetBlock.Y - 1, feetBlock.Z);
        var head = new BlockPos(feetBlock.X, feetBlock.Y + 1, feetBlock.Z);
        var belowKey = _blocks.GetStateMetadata(_worldCache.GetBlockState(below)).BlockKey;
        var feetDef = _blocks.GetRequired(_blocks.GetStateMetadata(_worldCache.GetBlockState(feetBlock)).BlockKey);
        var headDef = _blocks.GetRequired(_blocks.GetStateMetadata(_worldCache.GetBlockState(head)).BlockKey);
        var (sky, blk) = _worldCache.GetLight(feetBlock);

        var isSolidBelow = _blocks.GetRequired(belowKey).IsSolid;
        var isClear = !feetDef.IsSolid && !feetDef.IsFluid && !headDef.IsSolid && !headDef.IsFluid;
        return (sky, blk, belowKey == BuiltInBlocks.GrassBlock, isSolidBelow, isClear);
    }
}
