using OverHorizon.Audio;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay.Mobs;
using OverHorizon.Rendering;
using OverHorizon.Rendering.Hud;
using OverHorizon.World;
using OverHorizon.World.Lighting;
using OverHorizon.World.Storage;

namespace OverHorizon.Client;

/// <summary>
/// Concentric Chebyshev chunk-ring snapshot synchronization (scaling up to 64-chunk view distances)
/// and singleplayer 3D peaceful/hostile mob simulation + <c>0..15</c> visual lighting projection.
/// </summary>
internal sealed class ClientWorldSyncAndMobBridge
{
    private readonly IBlockRegistry _blocks;
    private readonly IWorldSubsystem _localWorld;
    private readonly ClientWorldCache _worldCache;
    private readonly IRenderSubsystem _renderer;
    private readonly IAudioSubsystem _audio;
    private readonly WorldMobManager _mobManager;
    private readonly List<WorldMobVisual> _mobVisualScratch = new(36);
    private int _rollingSyncRingCursor = 9;

    public ClientWorldSyncAndMobBridge(
        IBlockRegistry blocks,
        IWorldSubsystem localWorld,
        ClientWorldCache worldCache,
        IRenderSubsystem renderer,
        IAudioSubsystem audio,
        WorldMobManager mobManager)
    {
        _blocks = blocks;
        _localWorld = localWorld;
        _worldCache = worldCache;
        _renderer = renderer;
        _audio = audio;
        _mobManager = mobManager;
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

    public void StepWorldMobs(ClientPlayerController player, float dt)
    {
        var mobDamage = _mobManager.Step(
            _worldCache,
            ProbeMobSpawnCell,
            player.FeetPosition,
            player.Mode,
            playerArmorDefense: 0,
            skyBrightness: 1.0f,
            deltaSeconds: dt);

        if (mobDamage > 0.0f)
        {
            _ = player.ApplyDamage(mobDamage, _audio);
        }
    }

    public IReadOnlyList<WorldMobVisual> BuildMobVisuals(ClientPlayerController player)
    {
        _mobVisualScratch.Clear();
        var mobs = _mobManager.Mobs;
        var heldTorch = player.Hotbar.SelectedHeldLightEmission;
        for (var i = 0; i < mobs.Count; i++)
        {
            var m = mobs[i];
            var (sky, blk) = _worldCache.GetLight(m.FeetPosition.ToBlockPos());
            var distToPlayer = (float)m.FeetPosition.DistanceTo(player.Camera.Position);
            var torchAtMob = heldTorch > distToPlayer ? (byte)Math.Clamp((int)MathF.Round(heldTorch - distToPlayer), 0, 15) : (byte)0;
            var eff = LightingEngine.ComputeEffectiveLightLevel(sky, blk, torchAtMob);
            var bright = LightingEngine.LightLevelToBrightness(eff);
            _mobVisualScratch.Add(new WorldMobVisual(
                m.FeetPosition,
                m.YawDegrees,
                m.WalkPhase,
                (byte)m.Type,
                m.HurtFlashTimer,
                m.CreeperFuseTimer,
                m.IsSheared,
                bright));
        }

        return _mobVisualScratch;
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
