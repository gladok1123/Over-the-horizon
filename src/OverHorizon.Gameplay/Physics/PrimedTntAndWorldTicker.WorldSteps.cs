using System.Globalization;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay.Survival;
using OverHorizon.Physics;

namespace OverHorizon.Gameplay.Physics;

/// <summary>
/// Gravity-block falling, fluid frontier stepping, attachment cascading, and grass spread/decay for <see cref="PrimedTntAndWorldTicker"/>.
/// </summary>
public sealed partial class PrimedTntAndWorldTicker
{
    /// <summary>Executes one immediate step of falling Sand/Gravel blocks.</summary>
    public int StepGravityBlocks(Func<BlockPos, BlockState> getBlock, Action<BlockPos, BlockState> setBlock)
    {
        ArgumentNullException.ThrowIfNull(getBlock);
        ArgumentNullException.ThrowIfNull(setBlock);

        if (_activeGravityBlocks.Count == 0)
        {
            return 0;
        }

        var ordered = _activeGravityBlocks.ToArray();
        Array.Sort(ordered, static (a, b) => a.Y.CompareTo(b.Y));
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
        Action<BlockPos, BlockState> setBlock,
        Action<IReadOnlyList<(BlockPos Pos, BlockState State)>>? setBlockBatch = null)
    {
        ArgumentNullException.ThrowIfNull(collisionWorld);
        ArgumentNullException.ThrowIfNull(getBlock);
        ArgumentNullException.ThrowIfNull(setBlock);

        if (_activeFluidFrontier.Count == 0)
        {
            return 0;
        }

        var takeCount = Math.Min(256, _activeFluidFrontier.Count);
        var batch = new BlockPos[takeCount];
        var idx = 0;
        foreach (var p in _activeFluidFrontier)
        {
            batch[idx++] = p;
            if (idx >= takeCount) break;
        }

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
        var mutations = new List<(BlockPos Pos, BlockState State)>(changes.Count);

        for (var i = 0; i < changes.Count; i++)
        {
            var ch = changes[i];
            if (ch.NewLevel == FluidChange.RemoveFluidSentinel)
            {
                mutations.Add((ch.Position, BlockState.Air));
            }
            else
            {
                var exMeta = _blocks.GetStateMetadata(getBlock(ch.Position));
                if (exMeta.IsSolid)
                {
                    continue;
                }

                var waterState = _blocks.WithProperty(baseWater, "level", ch.NewLevel.ToString(CultureInfo.InvariantCulture));
                mutations.Add((ch.Position, waterState));
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

        if (mutations.Count > 0)
        {
            ApplyMutations(mutations, setBlock, setBlockBatch);
        }

        return mutations.Count;
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

    private void EnqueueFrontierNeighbors(BlockPos pos, Func<BlockPos, BlockState> getBlock)
    {
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
}
