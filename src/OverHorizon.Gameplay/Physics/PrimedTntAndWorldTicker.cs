using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay.Survival;
using OverHorizon.Physics;

namespace OverHorizon.Gameplay.Physics;

/// <summary>
/// Simulates live <see cref="PrimedTntEntity"/> physics &amp; precomputed-ray batch explosions, <c>Sand</c>/<c>Gravel</c> gravity falling,
/// <see cref="FluidSimulator"/> water spreading/draining, support-break cascading, and <c>GrassBlock</c> spread/decay onto <c>Dirt</c>.
/// </summary>
public sealed partial class PrimedTntAndWorldTicker
{
    private const int MaxActivePrimedTnt = 32;
    private const int MaxDetonationsPerStep = 3;
    private static readonly Vec3d[] PrecomputedExplosionRays = BuildPrecomputedExplosionRays();

    private readonly IBlockRegistry _blocks;
    private readonly List<PrimedTntEntity> _primedTnts = [];
    private readonly List<FallingBlockEntity> _fallingBlocks = [];
    private readonly HashSet<BlockPos> _activeFluidFrontier = [];
    private readonly HashSet<BlockPos> _activeGravityBlocks = [];
    private readonly HashSet<BlockPos> _activeFires = [];
    private int _nextTntId = 1;
    private int _nextFallingId = 1;
    private uint _rng = 0xC4F19A37U;
    private double _fluidTimer;
    private double _gravityTimer;
    private double _fireTimer;
    private double _grassTimer;

    /// <summary>Initializes a new instance of the <see cref="PrimedTntAndWorldTicker"/> class.</summary>
    public PrimedTntAndWorldTicker(IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        _blocks = blocks;
    }

    /// <summary>Gets all currently active primed TNT entities.</summary>
    public IReadOnlyList<PrimedTntEntity> PrimedTnts => _primedTnts;

    /// <summary>Gets all currently active smooth-falling Sand/Gravel entities.</summary>
    public IReadOnlyList<FallingBlockEntity> FallingBlocks => _fallingBlocks;

    /// <summary>Gets active burning Fire block positions for crackle audio and flame particles.</summary>
    public IReadOnlyCollection<BlockPos> ActiveFirePositions => _activeFires;

    /// <summary>Clears all active TNT entities, falling blocks, and queued world-tick frontiers (used on world transitions).</summary>
    public void Clear()
    {
        _primedTnts.Clear();
        _fallingBlocks.Clear();
        _activeFluidFrontier.Clear();
        _activeGravityBlocks.Clear();
        _activeFires.Clear();
    }

    /// <summary>Registers an active <c>Fire</c> voxel at <paramref name="pos"/> so it ages, crackles, and spreads boundedly.</summary>
    public void TrackFireBlock(BlockPos pos) => _ = _activeFires.Add(pos);

    /// <summary>Ignites and spawns a live physics-simulated <see cref="PrimedTntEntity"/> with an upward hop.</summary>
    public PrimedTntEntity SpawnPrimedTnt(BlockPos originBlock, float fuseSeconds = 4.0f)
    {
        var angle = ((NextRandom() % 360U) * Math.PI) / 180.0;
        var vel = new Vec3d(Math.Cos(angle) * 1.15, 4.10, Math.Sin(angle) * 1.15);
        var entity = new PrimedTntEntity(_nextTntId++, new Vec3d(originBlock.X + 0.5, originBlock.Y + 0.02, originBlock.Z + 0.5), vel, fuseSeconds);
        if (_primedTnts.Count >= MaxActivePrimedTnt)
        {
            _primedTnts.RemoveAt(0);
        }

        _primedTnts.Add(entity);
        return entity;
    }

    /// <summary>Notifies the ticker that <paramref name="pos"/> changed so water, gravity blocks, fire, and attached neighbors react.</summary>
    public void NotifyWorldBlockChanged(
        BlockPos pos,
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock,
        List<(BlockPos Pos, BlockState DetachedState)>? detachedBlocks = null)
    {
        ArgumentNullException.ThrowIfNull(getBlock);
        ArgumentNullException.ThrowIfNull(setBlock);

        CascadeUnsupportedAttachments(pos, getBlock, setBlock, detachedBlocks);
        EnqueueFrontierNeighbors(pos, getBlock);
    }

    /// <summary>
    /// Advances primed TNT entities &amp; explosions, smooth 60+ FPS falling Sand/Gravel, bounded Fire, batched Water, and Grass spread.
    /// </summary>
    public void Step(
        double deltaSeconds,
        IVoxelCollisionReader collisionWorld,
        Vec3d playerCenterPos,
        Func<BlockPos, BlockState> getBlock,
        Func<BlockPos, (byte Sky, byte Block)> getLight,
        Action<BlockPos, BlockState> setBlock,
        List<Vec3d> detonatedCenters,
        Action<IReadOnlyList<(BlockPos Pos, BlockState State)>>? setBlockBatch = null,
        List<(BlockPos Pos, BlockState BrokenState)>? brokenFragileDrops = null,
        List<BlockPos>? extinguishedFirePositions = null)
    {
        ArgumentNullException.ThrowIfNull(collisionWorld);
        ArgumentNullException.ThrowIfNull(getBlock);
        ArgumentNullException.ThrowIfNull(getLight);
        ArgumentNullException.ThrowIfNull(setBlock);
        ArgumentNullException.ThrowIfNull(detonatedCenters);

        var dt = Math.Clamp(deltaSeconds, 0.001, 0.1);
        StepPrimedTnts(dt, collisionWorld, getBlock, setBlock, setBlockBatch, detonatedCenters);
        StepFallingBlockEntities(dt, getBlock, setBlock, brokenFragileDrops);

        _gravityTimer += dt;
        if (_gravityTimer >= 0.06 && _activeGravityBlocks.Count > 0)
        {
            _gravityTimer = 0.0;
            _ = DetachDestabilizedGravityBlocks(getBlock, setBlock);
        }

        _fluidTimer += dt;
        if (_fluidTimer >= 0.18 && _activeFluidFrontier.Count > 0)
        {
            _fluidTimer = 0.0;
            _ = StepFluidFrontier(collisionWorld, getBlock, setBlock, setBlockBatch, extinguishedFirePositions);
        }

        _fireTimer += dt;
        if (_fireTimer >= 1.65 && _activeFires.Count > 0)
        {
            _fireTimer = 0.0;
            StepBoundedFireFrontier(getBlock, setBlock, extinguishedFirePositions);
        }

        _grassTimer += dt;
        if (_grassTimer >= 0.55)
        {
            _grassTimer = 0.0;
            _ = StepGrassSpreadAndDecay(playerCenterPos, getBlock, getLight, setBlock, sampleCount: 16);
        }
    }

    /// <summary>Executes an ultra-fast precomputed-ray 3D explosion centered at <paramref name="center"/> with blast power <paramref name="power"/>.</summary>
    public int DetonateRaycastExplosion(
        Vec3d center,
        float power,
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock,
        Action<IReadOnlyList<(BlockPos Pos, BlockState State)>>? setBlockBatch = null)
    {
        ArgumentNullException.ThrowIfNull(getBlock);
        ArgumentNullException.ThrowIfNull(setBlock);

        var destroyed = new HashSet<BlockPos>(192);
        var chainTnts = new List<BlockPos>(8);
        var rays = PrecomputedExplosionRays;

        for (var r = 0; r < rays.Length; r++)
        {
            var dir = rays[r];
            var intensity = power * (0.82f + (((NextRandom() & 0xFFU) / 255.0f) * 0.36f));
            var cur = center;

            var hasLastCell = false;
            BlockPos lastBp = default;
            var lastNonAir = false;
            var lastAttenuation = 0.0f;
            var lastDestroyable = false;
            var lastIsTnt = false;

            while (intensity > 0.0f)
            {
                var bp = cur.ToBlockPos();
                if (bp.Y < EngineConstants.WorldMinY || bp.Y >= EngineConstants.WorldMaxY)
                {
                    break;
                }

                if (!hasLastCell || bp != lastBp)
                {
                    hasLastCell = true;
                    lastBp = bp;
                    var st = getBlock(bp);
                    lastNonAir = !st.IsAir;
                    if (lastNonAir)
                    {
                        var meta = _blocks.GetStateMetadata(st);
                        lastAttenuation = (GetBlastResistance(meta) + 0.3f) * 0.3f;
                        lastDestroyable = meta.Hardness >= 0.0f && !meta.IsFluid;
                        lastIsTnt = meta.BlockKey == BuiltInBlocks.Tnt;
                    }
                }

                if (lastNonAir)
                {
                    intensity -= lastAttenuation;
                    if (intensity > 0.0f && lastDestroyable && destroyed.Add(bp) && lastIsTnt)
                    {
                        chainTnts.Add(bp);
                    }
                }

                cur += dir;
                intensity -= 0.225f;
            }
        }

        if (destroyed.Count == 0)
        {
            return 0;
        }

        var initialDestroyed = destroyed.ToArray();
        for (var i = 0; i < initialDestroyed.Length; i++)
        {
            var bp = initialDestroyed[i];
            var meta = _blocks.GetStateMetadata(getBlock(bp));
            if (BlockAttachmentAndShapeRules.TryGetPartnerBedPosition(meta, bp, out var partnerPos, out _))
            {
                _ = destroyed.Add(partnerPos);
            }
        }

        var batchList = new List<(BlockPos Pos, BlockState State)>(destroyed.Count + 16);
        foreach (var bp in destroyed)
        {
            batchList.Add((bp, BlockState.Air));
        }

        ApplyMutations(batchList, setBlock, setBlockBatch);

        var detachedList = new List<(BlockPos Pos, BlockState State)>(16);
        foreach (var bp in destroyed)
        {
            EnqueueFrontierNeighbors(bp, getBlock);
            foreach (var dir in DirectionExtensions.All)
            {
                var neighbor = bp.Offset(dir);
                if (destroyed.Contains(neighbor) || neighbor.Y < EngineConstants.WorldMinY || neighbor.Y >= EngineConstants.WorldMaxY)
                {
                    continue;
                }

                var ns = getBlock(neighbor);
                if (!ns.IsAir && !BlockAttachmentAndShapeRules.IsStillSupported(_blocks, getBlock, neighbor, ns))
                {
                    _ = destroyed.Add(neighbor);
                    detachedList.Add((neighbor, BlockState.Air));
                }
            }
        }

        if (detachedList.Count > 0)
        {
            ApplyMutations(detachedList, setBlock, setBlockBatch);
        }

        for (var i = 0; i < chainTnts.Count && _primedTnts.Count < MaxActivePrimedTnt; i++)
        {
            var fuse = 0.55f + ((NextRandom() % 75U) / 100.0f);
            _ = SpawnPrimedTnt(chainTnts[i], fuse);
        }

        return destroyed.Count;
    }

    private static void ApplyMutations(
        List<(BlockPos Pos, BlockState State)> mutations,
        Action<BlockPos, BlockState> setBlock,
        Action<IReadOnlyList<(BlockPos Pos, BlockState State)>>? setBlockBatch)
    {
        if (setBlockBatch is not null)
        {
            setBlockBatch(mutations);
            return;
        }

        for (var i = 0; i < mutations.Count; i++)
        {
            setBlock(mutations[i].Pos, mutations[i].State);
        }
    }

    private static Vec3d[] BuildPrecomputedExplosionRays()
    {
        const int grid = 12;
        var list = new List<Vec3d>(610);
        for (var gx = 0; gx < grid; gx++)
        {
            for (var gy = 0; gy < grid; gy++)
            {
                for (var gz = 0; gz < grid; gz++)
                {
                    if (gx != 0 && gx != grid - 1 && gy != 0 && gy != grid - 1 && gz != 0 && gz != grid - 1)
                    {
                        continue;
                    }

                    var rx = ((gx / (double)(grid - 1)) * 2.0) - 1.0;
                    var ry = ((gy / (double)(grid - 1)) * 2.0) - 1.0;
                    var rz = ((gz / (double)(grid - 1)) * 2.0) - 1.0;
                    list.Add(new Vec3d(rx, ry, rz).Normalize() * 0.32);
                }
            }
        }

        return list.ToArray();
    }

    private void StepPrimedTnts(
        double dt,
        IVoxelCollisionReader collisionWorld,
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock,
        Action<IReadOnlyList<(BlockPos Pos, BlockState State)>>? setBlockBatch,
        List<Vec3d> detonatedCenters)
    {
        var detonatedThisStep = 0;
        for (var i = _primedTnts.Count - 1; i >= 0; i--)
        {
            var tnt = _primedTnts[i];
            var step = KinematicsIntegrator.StepEntity(collisionWorld, tnt.FeetPosition, tnt.Velocity, 0.94, 0.94, dt);
            var damp = step.OnGround ? 0.72 : 0.98;
            tnt.FeetPosition = step.Position;
            tnt.Velocity = new Vec3d(step.Velocity.X * damp, step.Velocity.Y, step.Velocity.Z * damp);
            tnt.FuseSeconds -= (float)dt;

            if (tnt.FuseSeconds <= 0.0f && detonatedThisStep < MaxDetonationsPerStep)
            {
                detonatedThisStep++;
                var center = new Vec3d(tnt.FeetPosition.X, tnt.FeetPosition.Y + 0.48, tnt.FeetPosition.Z);
                _primedTnts.RemoveAt(i);
                detonatedCenters.Add(center);
                _ = DetonateRaycastExplosion(center, power: 4.0f, getBlock, setBlock, setBlockBatch);
            }
        }
    }

    private static float GetBlastResistance(BlockStateMetadata meta)
    {
        if (meta.Hardness < 0.0f) return 3_600_000.0f;
        if (meta.BlockKey == BuiltInBlocks.Obsidian) return 1_200.0f;
        if (meta.IsFluid) return 100.0f;
        return MathF.Max(0.2f, meta.Hardness * 2.2f);
    }

    private uint NextRandom()
    {
        var x = _rng;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _rng = x;
        return x;
    }
}
