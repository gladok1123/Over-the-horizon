using OverHorizon.Audio;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Effects;
using OverHorizon.Gameplay.Inventory;
using OverHorizon.Gameplay.Items;
using OverHorizon.Gameplay.Mobs;
using OverHorizon.Gameplay.Physics;
using OverHorizon.Gameplay.Projectiles;
using OverHorizon.Gameplay.Redstone;
using OverHorizon.Gameplay.Survival;
using OverHorizon.Gameplay.Vehicles;
using OverHorizon.Rendering;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Textures;
using OverHorizon.World;
using OverHorizon.World.Storage;

namespace OverHorizon.Client;

/// <summary>
/// Coordinates concentric Chebyshev chunk-ring snapshot synchronization, <see cref="WorldTimeCycle"/>,
/// <see cref="RedstoneEngine"/> (Wire, Repeater, Comparator, Pistons, Doors/Trapdoors, Powered Rails),
/// <see cref="PrimedTntAndWorldTicker"/> (batch craters, smooth 60 FPS <see cref="FallingBlockEntity"/>, controlled Fire, Water),
/// <see cref="DroppedItemManager"/>, <see cref="ParticleSystem"/>, <see cref="MinecartManager"/>, <see cref="ArrowProjectileManager"/>, and 3D Mobs.
/// </summary>
internal sealed partial class ClientWorldSyncAndMobBridge
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
    private readonly TextureAtlasData _atlas;
    private readonly List<WorldMobVisual> _mobVisualScratch = new(36);
    private readonly List<PrimedTntVisual> _tntVisualScratch = new(16);
    private readonly List<MinecartVisual> _minecartVisualScratch = new(16);
    private readonly List<ArrowProjectileVisual> _arrowVisualScratch = new(32);
    private readonly List<DroppedItemVisual> _droppedVisualScratch = new(96);
    private readonly List<FallingBlockVisual> _fallingVisualScratch = new(32);
    private readonly List<WorldParticleVisual> _particleVisualScratch = new(512);
    private readonly List<BlockPos> _ignitedTntScratch = new(8);
    private readonly List<(BlockPos Pos, RedstoneSoundKind Sound)> _redstoneSoundScratch = new(8);
    private readonly List<Vec3d> _detonatedScratch = new(8);
    private readonly List<Vec3d> _creeperDetonations = new(4);
    private readonly List<Vec3d> _creeperFuseStarts = new(4);
    private readonly List<Vec3d> _shieldBlockPositions = new(4);
    private readonly List<(MobType Type, Vec3d Position, bool Died, float FallDamage)> _mobFallScratch = new(8);
    private readonly List<ItemStack> _arrowDropScratch = new(8);
    private readonly List<Vec3d> _arrowHitSoundScratch = new(8);
    private readonly List<(BlockPos Pos, BlockState BrokenState)> _brokenFragileScratch = new(8);
    private readonly List<BlockPos> _extinguishedFireScratch = new(8);
    private readonly HashSet<ChunkPos> _touchedChunksScratch = [];
    private readonly List<BlockPos> _editedPositionsScratch = new(64);

    private IWorldSubsystem _localWorld;
    private double _ambientMobSoundTimer;
    private double _minecartRollAudioTimer;
    private double _fireCrackleTimer;
    private double _tntFuseAudioTimer;
    private double _particleTickTimer;
    private int _rollingSyncRingCursor = 9;
    private int _dropSeq;
    private float _mpDropSpinClock;

    public ClientWorldSyncAndMobBridge(
        IBlockRegistry blocks,
        IRegistry<ItemDefinition> items,
        IWorldSubsystem localWorld,
        ClientWorldCache worldCache,
        IRenderSubsystem renderer,
        IAudioSubsystem audio,
        WorldMobManager mobManager,
        TextureAtlasData? atlas = null)
    {
        _blocks = blocks;
        _items = items;
        _localWorld = localWorld;
        _worldCache = worldCache;
        _renderer = renderer;
        _audio = audio;
        _mobManager = mobManager;
        _atlas = atlas ?? ProceduralAtlasGenerator.CreateDefault(blocks);
        Redstone = new RedstoneEngine(blocks);
        WorldTicker = new PrimedTntAndWorldTicker(blocks);
        Minecarts = new MinecartManager(blocks);
        Arrows = new ArrowProjectileManager(blocks);
        DroppedItems = new DroppedItemManager();
        Particles = new ParticleSystem();
    }

    public IWorldSubsystem ActiveLocalWorld => _localWorld;
    public WorldTimeCycle TimeCycle { get; } = new();
    public RedstoneEngine Redstone { get; }
    public PrimedTntAndWorldTicker WorldTicker { get; }
    public MinecartManager Minecarts { get; }
    public ArrowProjectileManager Arrows { get; }
    public DroppedItemManager DroppedItems { get; }
    public ParticleSystem Particles { get; }

    public void SetActiveLocalWorld(IWorldSubsystem nextWorld, long initialTimeTicks)
    {
        _localWorld = nextWorld;
        TimeCycle.SetTime(initialTimeTicks);
        _mobManager.Clear();
        Minecarts.Clear();
        Arrows.Clear();
        DroppedItems.Clear();
        Particles.Clear();
        WorldTicker.Clear();
        ClearMultiplayerEntitySnapshots();
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

    public void EmitBlockBreakParticles(BlockPos pos, ResourceKey blockKey)
    {
        if (blockKey == BuiltInBlocks.Air || blockKey == BuiltInBlocks.Fire) return;
        var tile = _atlas.GetTileIndex(blockKey, Direction.Up);
        var color = ClientHotbar.ResolveFallbackColor(blockKey);
        Particles.EmitBlockBreak(pos, tile, color, baseCount: 14);
    }

    public bool TryInteractRedstoneBlock(BlockPos pos)
    {
        _editedPositionsScratch.Clear();
        _touchedChunksScratch.Clear();
        _ignitedTntScratch.Clear();
        _redstoneSoundScratch.Clear();

        var interacted = Redstone.TryInteractComponent(pos, _localWorld.GetBlockState, RecordWorldSet);
        if (!interacted) return false;

        Redstone.EvaluateImmediate(_localWorld.GetBlockState, RecordWorldSet, _ignitedTntScratch, _redstoneSoundScratch);
        Particles.EmitBurst(new Vec3d(pos.X + 0.5, pos.Y + 0.35, pos.Z + 0.5), ParticleKind.RedstoneSpark, 6);
        ProcessRedstoneOutputsAndRemesh();
        return true;
    }

    public bool ApplyLocalBlockChangeWithCascade(
        BlockPos primaryPos,
        BlockState primaryState,
        bool spawnDetachedItemDrops)
    {
        var previousState = _localWorld.GetBlockState(primaryPos);
        if (!_localWorld.SetBlockState(primaryPos, primaryState))
        {
            return false;
        }

        _editedPositionsScratch.Clear();
        _touchedChunksScratch.Clear();
        _ignitedTntScratch.Clear();
        _redstoneSoundScratch.Clear();

        TrackEditedBlock(primaryPos);
        if (!primaryState.IsAir && _blocks.GetStateMetadata(primaryState).BlockKey == BuiltInBlocks.Fire)
        {
            WorldTicker.TrackFireBlock(primaryPos);
        }

        WorldTicker.NotifyWorldBlockChanged(primaryPos, _localWorld.GetBlockState, RecordWorldSet);

        // 1. Atomic 2-block RedBed breaking: breaking EITHER head or foot removes its partner without duplicate drops!
        if (primaryState.IsAir &&
            !previousState.IsAir &&
            BlockAttachmentAndShapeRules.TryGetPartnerBedPosition(_blocks.GetStateMetadata(previousState), primaryPos, out var partnerBedPos, out _))
        {
            var partnerState = _localWorld.GetBlockState(partnerBedPos);
            if (!partnerState.IsAir &&
                _blocks.GetStateMetadata(partnerState).BlockKey == BuiltInBlocks.RedBed &&
                _localWorld.SetBlockState(partnerBedPos, BlockState.Air))
            {
                TrackEditedBlock(partnerBedPos);
                WorldTicker.NotifyWorldBlockChanged(partnerBedPos, _localWorld.GetBlockState, RecordWorldSet);
            }
        }

        // 2. Auto-connect adjacent Rails & Powered Rails when placing or breaking tracks!
        var placedKey = primaryState.IsAir ? BuiltInBlocks.Air : _blocks.GetStateMetadata(primaryState).BlockKey;
        var prevKey = previousState.IsAir ? BuiltInBlocks.Air : _blocks.GetStateMetadata(previousState).BlockKey;
        if (RailPlacementRules.IsRailBlock(placedKey) || RailPlacementRules.IsRailBlock(prevKey))
        {
            RailPlacementRules.RefreshAdjacentRails(_blocks, _localWorld.GetBlockState, RecordWorldSet, primaryPos);
        }

        // 3. Detach unsupported neighbor Torches, Lanterns, Ladders, Redstone components, Rails, or Bed halves as 3D DroppedItems!
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

                    if (spawnDetachedItemDrops && nKey != BuiltInBlocks.RedBed && nKey != BuiltInBlocks.Fire)
                    {
                        var drop = BlockDropResolver.ResolveDrop(nKey, heldTool: null, _items, _dropSeq++);
                        if (!drop.IsEmpty)
                        {
                            _ = DroppedItems.SpawnBlockDrop(nPos, drop);
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

    private void RecordWorldBatchSet(IReadOnlyList<(BlockPos Pos, BlockState State)> updates)
    {
        if (updates.Count == 0) return;
        var arr = new (BlockPos Position, BlockState State)[updates.Count];
        for (var i = 0; i < updates.Count; i++) arr[i] = updates[i];

        _ = _localWorld.SetBlockStatesBatch(arr);
        for (var i = 0; i < updates.Count; i++)
        {
            var pos = updates[i].Pos;
            TrackEditedBlock(pos);
            Redstone.NotifyBlockChanged(pos);
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
            _audio.PlayPositional(SoundCue.TntFuse, new Vec3d(tPos.X + 0.5, tPos.Y + 0.5, tPos.Z + 0.5), gain: 0.92f);
        }

        for (var i = 0; i < _redstoneSoundScratch.Count; i++)
        {
            var (sPos, kind) = _redstoneSoundScratch[i];
            var cue = kind switch
            {
                RedstoneSoundKind.PistonExtend => SoundCue.PistonExtend,
                RedstoneSoundKind.PistonRetract => SoundCue.PistonRetract,
                RedstoneSoundKind.TorchFizz => SoundCue.Fizz,
                RedstoneSoundKind.DoorToggle => SoundCue.DoorToggle,
                _ => SoundCue.RedstoneClick,
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

        if (_editedPositionsScratch.Count <= 24)
        {
            for (var i = 0; i < _editedPositionsScratch.Count; i++)
            {
                _worldCache.RemeshAroundBlockImmediate(_editedPositionsScratch[i], _renderer);
            }
        }
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
}
