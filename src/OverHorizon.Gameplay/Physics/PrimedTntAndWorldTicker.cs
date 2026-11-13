using System.Globalization;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay.Survival;
using OverHorizon.Physics;

namespace OverHorizon.Gameplay.Physics;

/// <summary>
/// Simulates live <see cref="PrimedTntEntity"/> physics &amp; 16-grid raycast explosions, <c>Sand</c>/<c>Gravel</c> gravity falling,
/// <see cref="FluidSimulator"/> water spreading/draining, support-break cascading, and <c>GrassBlock</c> spread/decay onto <c>Dirt</c>.
/// </summary>
public sealed class PrimedTntAndWorldTicker
{
    private readonly IBlockRegistry _blocks;
    private readonly List<PrimedTntEntity> _primedTnts = [];
    private readonly HashSet<BlockPos> _activeFluidFrontier = [];
    private readonly HashSet<BlockPos> _activeGravityBlocks = [];
    private int _nextTntId = 1;
    private uint _rng = 0xC4F19A37U;
    private double _fluidTimer;
    private double _gravityTimer;
    private double _grassTimer;

    /// <summary>Initializes a new instance of the <see cref="PrimedTntAndWorldTicker"/> class.</summary>
    public PrimedTntAndWorldTicker(IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        _blocks = blocks;
    }

    /// <summary>Gets all currently active primed TNT entities.</summary>
    public IReadOnlyList<PrimedTntEntity> PrimedTnts => _primedTnts;

    /// <summary>Ignites and spawns a live physics-simulated <see cref="PrimedTntEntity"/> with an upward hop.</summary>
    public PrimedTntEntity SpawnPrimedTnt(BlockPos originBlock, float fuseSeconds = 4.0f)
    {
        var angle = ((NextRandom() % 360U) * Math.PI) / 180.0;
        var vel = new Vec3d(Math.Cos(angle) * 1.15, 4.10, Math.Sin(angle) * 1.15);
        var entity = new PrimedTntEntity(_nextTntId++, new Vec3d(originBlock.X + 0.5, originBlock.Y + 0.02, originBlock.Z + 0.5), vel, fuseSeconds);
        _primedTnts.Add(entity);
        return entity;
    }

    /// <summary>Notifies the ticker that <paramref name="pos"/> changed so water, gravity blocks, and attached neighbors react.</summary>
    public void NotifyWorldBlockChanged(
        BlockPos pos,
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock)
    {
        ArgumentNullException.ThrowIfNull(getBlock);
        ArgumentNullException.ThrowIfNull(setBlock);

        CascadeUnsupportedAttachments(pos, getBlock, setBlock);

        for (var dy = -1; dy <= 2; dy++)
        {
            var gp = new BlockPos(pos.X, pos.Y + dy, pos.Z);
            if (gp.Y >= EngineConstants.WorldMinY && gp.Y < EngineConstants.WorldMaxY && IsGravityBlock(getBlock(gp)))
            {
                _ = _activeGravityBlocks.Add(gp);
            }
        }

        _ = _activeFluidFrontier.Add(pos);
        foreach (var dir in DirectionExtensions.All)
        {
            var np = pos.Offset(dir);
            if (np.Y >= EngineConstants.WorldMinY && np.Y < EngineConstants.WorldMaxY)
            {
                _ = _activeFluidFrontier.Add(np);
            }
        }
    }

    /// <summary>
    /// Advances primed TNT entities &amp; explosions, gravity-falling Sand/Gravel, flowing Water, and Grass spread/decay.
    /// </summary>
    public void Step(
        double deltaSeconds,
        IVoxelCollisionReader collisionWorld,
        Vec3d playerCenterPos,
        Func<BlockPos, BlockState> getBlock,
        Func<BlockPos, (byte Sky, byte Block)> getLight,
        Action<BlockPos, BlockState> setBlock,
        List<Vec3d> detonatedCenters)
    {
        ArgumentNullException.ThrowIfNull(collisionWorld);
        ArgumentNullException.ThrowIfNull(getBlock);
        ArgumentNullException.ThrowIfNull(getLight);
        ArgumentNullException.ThrowIfNull(setBlock);
        ArgumentNullException.ThrowIfNull(detonatedCenters);

        var dt = Math.Clamp(deltaSeconds, 0.001, 0.1);
        StepPrimedTnts(dt, collisionWorld, getBlock, setBlock, detonatedCenters);

        _gravityTimer += dt;
        if (_gravityTimer >= 0.09 && _activeGravityBlocks.Count > 0)
        {
            _gravityTimer = 0.0;
            StepGravityBlocks(getBlock, setBlock);
        }

        _fluidTimer += dt;
        if (_fluidTimer >= 0.22 && _activeFluidFrontier.Count > 0)
        {
            _fluidTimer = 0.0;
            StepFluidFrontier(collisionWorld, getBlock, setBlock);
        }

        _grassTimer += dt;
        if (_grassTimer >= 0.35)
        {
            _grassTimer = 0.0;
            StepGrassSpreadAndDecay(playerCenterPos, getBlock, getLight, setBlock);
        }
    }

    /// <summary>Executes an authentic 3D raycast explosion centered at <paramref name="center"/> with blast power <paramref name="power"/>.</summary>
    public int DetonateRaycastExplosion(
        Vec3d center,
        float power,
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock)
    {
        ArgumentNullException.ThrowIfNull(getBlock);
        ArgumentNullException.ThrowIfNull(setBlock);

        var destroyed = new HashSet<BlockPos>();
        var chainTnts = new List<BlockPos>();
        const int grid = 10;

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
                    var dir = new Vec3d(rx, ry, rz).Normalize();

                    var intensity = power * (0.82f + (((NextRandom() & 0xFFU) / 255.0f) * 0.36f));
                    var cur = center;

                    while (intensity > 0.0f)
                    {
                        var bp = cur.ToBlockPos();
                        if (bp.Y < EngineConstants.WorldMinY || bp.Y >= EngineConstants.WorldMaxY)
                        {
                            break;
                        }

                        var st = getBlock(bp);
                        if (!st.IsAir)
                        {
                            var meta = _blocks.GetStateMetadata(st);
                            var resistance = GetBlastResistance(meta);
                            intensity -= (resistance + 0.3f) * 0.3f;
                            if (intensity > 0.0f && meta.Hardness >= 0.0f && !meta.IsFluid)
                            {
                                if (meta.BlockKey == BuiltInBlocks.Tnt)
                                {
                                    chainTnts.Add(bp);
                                }

                                _ = destroyed.Add(bp);
                            }
                        }

                        cur += dir * 0.32;
                        intensity -= 0.225f;
                    }
                }
            }
        }

        foreach (var bp in destroyed)
        {
            setBlock(bp, BlockState.Air);
            NotifyWorldBlockChanged(bp, getBlock, setBlock);
        }

        for (var i = 0; i < chainTnts.Count; i++)
        {
            var fuse = 0.65f + ((NextRandom() % 80U) / 100.0f);
            _ = SpawnPrimedTnt(chainTnts[i], fuse);
        }

        return destroyed.Count;
    }

    /// <summary>Executes one immediate step of falling Sand/Gravel blocks (also callable deterministically from tests).</summary>
    public int StepGravityBlocks(Func<BlockPos, BlockState> getBlock, Action<BlockPos, BlockState> setBlock)
    {
        ArgumentNullException.ThrowIfNull(getBlock);
        ArgumentNullException.ThrowIfNull(setBlock);

        if (_activeGravityBlocks.Count == 0)
        {
            return 0;
        }

        var ordered = _activeGravityBlocks.OrderBy(static p => p.Y).ToArray();
        _activeGravityBlocks.Clear();
        var moved = 0;

        for (var i = 0; i < ordered.Length; i++)
        {
            var pos = ordered[i];
            var st = getBlock(pos);
            if (!IsGravityBlock(st) || pos.Y <= EngineConstants.WorldMinY)
            {
                continue;
            }

            var below = pos.Offset(Direction.Down);
            var belowState = getBlock(below);
            var belowMeta = _blocks.GetStateMetadata(belowState);
            if (belowState.IsAir || belowMeta.IsFluid || belowMeta.BlockKey == BuiltInBlocks.Fire)
            {
                setBlock(pos, BlockState.Air);
                setBlock(below, st);
                moved++;

                _ = _activeGravityBlocks.Add(below);
                var above = pos.Offset(Direction.Up);
                if (above.Y < EngineConstants.WorldMaxY && IsGravityBlock(getBlock(above)))
                {
                    _ = _activeGravityBlocks.Add(above);
                }

                CascadeUnsupportedAttachments(pos, getBlock, setBlock);
            }
        }

        return moved;
    }

    /// <summary>Executes one immediate step of active water flow &amp; drainage using <see cref="FluidSimulator"/>.</summary>
    public int StepFluidFrontier(
        IVoxelCollisionReader collisionWorld,
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock)
    {
        ArgumentNullException.ThrowIfNull(collisionWorld);
        ArgumentNullException.ThrowIfNull(getBlock);
        ArgumentNullException.ThrowIfNull(setBlock);

        if (_activeFluidFrontier.Count == 0)
        {
            return 0;
        }

        var batch = _activeFluidFrontier.Take(256).ToArray();
        for (var i = 0; i < batch.Length; i++)
        {
            _ = _activeFluidFrontier.Remove(batch[i]);
        }

        var changes = FluidSimulator.EvaluateStep(collisionWorld, batch);
        if (changes.Count == 0)
        {
            return 0;
        }

        var baseWater = _blocks.GetDefaultState(BuiltInBlocks.Water);
        for (var i = 0; i < changes.Count; i++)
        {
            var ch = changes[i];
            if (ch.NewLevel == FluidChange.RemoveFluidSentinel)
            {
                setBlock(ch.Position, BlockState.Air);
            }
            else
            {
                // Pop off non-solid fragile blocks (Torch/RedstoneWire/Fire) washed away by flowing water!
                var existing = getBlock(ch.Position);
                var exMeta = _blocks.GetStateMetadata(existing);
                if (exMeta.IsSolid)
                {
                    continue;
                }

                var waterState = _blocks.WithProperty(baseWater, "level", ch.NewLevel.ToString(CultureInfo.InvariantCulture));
                setBlock(ch.Position, waterState);
            }

            _ = _activeFluidFrontier.Add(ch.Position);
            foreach (var dir in DirectionExtensions.All)
            {
                var np = ch.Position.Offset(dir);
                if (np.Y >= EngineConstants.WorldMinY && np.Y < EngineConstants.WorldMaxY)
                {
                    _ = _activeFluidFrontier.Add(np);
                }
            }
        }

        return changes.Count;
    }

    /// <summary>Evaluates Grass Block spread onto nearby Dirt and Grass decay under opaque blocks.</summary>
    public int StepGrassSpreadAndDecay(
        Vec3d centerPos,
        Func<BlockPos, BlockState> getBlock,
        Func<BlockPos, (byte Sky, byte Block)> getLight,
        Action<BlockPos, BlockState> setBlock,
        int sampleCount = 28)
    {
        ArgumentNullException.ThrowIfNull(getBlock);
        ArgumentNullException.ThrowIfNull(getLight);
        ArgumentNullException.ThrowIfNull(setBlock);

        var grassDefault = _blocks.GetDefaultState(BuiltInBlocks.GrassBlock);
        var dirtDefault = _blocks.GetDefaultState(BuiltInBlocks.Dirt);
        var centerBlock = centerPos.ToBlockPos();
        var mutated = 0;

        for (var s = 0; s < sampleCount; s++)
        {
            var dx = (int)(NextRandom() % 49U) - 24;
            var dy = (int)(NextRandom() % 25U) - 12;
            var dz = (int)(NextRandom() % 49U) - 24;
            var pos = new BlockPos(centerBlock.X + dx, Math.Clamp(centerBlock.Y + dy, EngineConstants.WorldMinY + 1, EngineConstants.WorldMaxY - 2), centerBlock.Z + dz);
            var st = getBlock(pos);
            if (_blocks.GetStateMetadata(st).BlockKey != BuiltInBlocks.GrassBlock)
            {
                continue;
            }

            var abovePos = pos.Offset(Direction.Up);
            var aboveMeta = _blocks.GetStateMetadata(getBlock(abovePos));
            if (aboveMeta.IsOpaque || aboveMeta.IsFluid)
            {
                setBlock(pos, dirtDefault);
                mutated++;
                continue;
            }

            var (skyAbove, blkAbove) = getLight(abovePos);
            if (Math.Max(skyAbove, blkAbove) < 9)
            {
                continue;
            }

            for (var attempt = 0; attempt < 4; attempt++)
            {
                var tx = pos.X + ((int)(NextRandom() % 3U) - 1);
                var ty = pos.Y + ((int)(NextRandom() % 5U) - 3);
                var tz = pos.Z + ((int)(NextRandom() % 3U) - 1);
                var target = new BlockPos(tx, ty, tz);
                if (target.Y < EngineConstants.WorldMinY || target.Y >= EngineConstants.WorldMaxY - 1)
                {
                    continue;
                }

                if (_blocks.GetStateMetadata(getBlock(target)).BlockKey != BuiltInBlocks.Dirt)
                {
                    continue;
                }

                var targetAbove = target.Offset(Direction.Up);
                var targetAboveMeta = _blocks.GetStateMetadata(getBlock(targetAbove));
                var (tSky, tBlk) = getLight(targetAbove);
                if (!targetAboveMeta.IsOpaque && !targetAboveMeta.IsFluid && Math.Max(tSky, tBlk) >= 4)
                {
                    setBlock(target, grassDefault);
                    mutated++;
                    break;
                }
            }
        }

        return mutated;
    }

    private void StepPrimedTnts(
        double dt,
        IVoxelCollisionReader collisionWorld,
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock,
        List<Vec3d> detonatedCenters)
    {
        for (var i = _primedTnts.Count - 1; i >= 0; i--)
        {
            var tnt = _primedTnts[i];
            var step = KinematicsIntegrator.StepEntity(collisionWorld, tnt.FeetPosition, tnt.Velocity, 0.94, 0.94, dt);
            var damp = step.OnGround ? 0.72 : 0.98;
            tnt.FeetPosition = step.Position;
            tnt.Velocity = new Vec3d(step.Velocity.X * damp, step.Velocity.Y, step.Velocity.Z * damp);
            tnt.FuseSeconds -= (float)dt;

            if (tnt.FuseSeconds <= 0.0f)
            {
                var center = new Vec3d(tnt.FeetPosition.X, tnt.FeetPosition.Y + 0.48, tnt.FeetPosition.Z);
                _primedTnts.RemoveAt(i);
                detonatedCenters.Add(center);
                _ = DetonateRaycastExplosion(center, power: 4.0f, getBlock, setBlock);
            }
        }
    }

    private void CascadeUnsupportedAttachments(
        BlockPos brokenOrChangedPos,
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock)
    {
        foreach (var dir in DirectionExtensions.All)
        {
            var neighbor = brokenOrChangedPos.Offset(dir);
            if (neighbor.Y < EngineConstants.WorldMinY || neighbor.Y >= EngineConstants.WorldMaxY)
            {
                continue;
            }

            var ns = getBlock(neighbor);
            if (!ns.IsAir && !BlockAttachmentAndShapeRules.IsStillSupported(_blocks, getBlock, neighbor, ns))
            {
                setBlock(neighbor, BlockState.Air);
            }
        }
    }

    private bool IsGravityBlock(BlockState state)
    {
        if (state.IsAir)
        {
            return false;
        }

        var key = _blocks.GetStateMetadata(state).BlockKey;
        return key == BuiltInBlocks.Sand || key == BuiltInBlocks.Gravel;
    }

    private static float GetBlastResistance(BlockStateMetadata meta)
    {
        if (meta.Hardness < 0.0f)
        {
            return 3_600_000.0f;
        }

        if (meta.BlockKey == BuiltInBlocks.Obsidian)
        {
            return 1_200.0f;
        }

        if (meta.IsFluid)
        {
            return 100.0f;
        }

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
