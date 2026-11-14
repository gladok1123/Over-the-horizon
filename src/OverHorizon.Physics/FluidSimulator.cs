using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;

namespace OverHorizon.Physics;

/// <summary>
/// Deterministic cellular-automaton water simulator implementing vanilla-accurate 8-level decay,
/// 4-block slope/ledge pathfinding, vertical waterfalls into air or lakes without mid-air shelves,
/// and infinite <c>2x2</c> water source formation.
/// </summary>
public static class FluidSimulator
{
    /// <summary>Fluid level representing a permanent water source block.</summary>
    public const int SourceLevel = 0;

    /// <summary>Maximum horizontal spread level before water dries up (<c>7</c>).</summary>
    public const int MaxHorizontalLevel = 7;

    /// <summary>Fluid level representing vertically falling water.</summary>
    public const int FallingLevel = 8;

    private const int SlopeSearchMaxDepth = 4;
    private const int NoDropSentinel = 999;

    /// <summary>
    /// Evaluates one deterministic water tick over <paramref name="activePositions"/> against read-only <paramref name="world"/>.
    /// </summary>
    /// <param name="world">Read-only collision and fluid level reader.</param>
    /// <param name="activePositions">Candidate positions scheduled for fluid evaluation.</param>
    /// <returns>Ordered, deduplicated list of <see cref="FluidChange"/> mutations.</returns>
    public static IReadOnlyList<FluidChange> EvaluateStep(
        IVoxelCollisionReader world,
        ReadOnlySpan<BlockPos> activePositions)
    {
        var buffer = new List<FluidChange>(activePositions.Length);
        _ = EvaluateStepInto(world, activePositions, buffer);
        return buffer;
    }

    /// <summary>
    /// Evaluates one deterministic fluid tick into a caller-owned <paramref name="output"/> buffer,
    /// letting long-running tick loops reuse the same list instead of allocating one every step.
    /// </summary>
    /// <param name="world">Read-only collision and fluid level reader.</param>
    /// <param name="activePositions">Candidate positions scheduled for fluid evaluation.</param>
    /// <param name="output">Destination buffer, cleared before use and left deterministically ordered.</param>
    /// <returns>Number of fluid changes appended to <paramref name="output"/>.</returns>
    public static int EvaluateStepInto(
        IVoxelCollisionReader world,
        ReadOnlySpan<BlockPos> activePositions,
        List<FluidChange> output)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(output);

        output.Clear();

        if (activePositions.IsEmpty)
        {
            return 0;
        }

        var pending = new Dictionary<BlockPos, int>(activePositions.Length * 2);

        for (var i = 0; i < activePositions.Length; i++)
        {
            var pos = activePositions[i];
            if (pos.Y < EngineConstants.WorldMinY || pos.Y >= EngineConstants.WorldMaxY || !world.IsColumnLoaded(pos))
            {
                continue;
            }

            if (world.IsSolidBlock(pos))
            {
                continue;
            }

            var currentLevel = world.GetFluidLevelOrNegative(pos);
            if (currentLevel < 0)
            {
                TryFormInfiniteSourceInEmptyCell(world, pos, pending);
                continue;
            }

            EvaluateSourceOrFlowingCell(world, pos, currentLevel, pending);
        }

        if (pending.Count == 0)
        {
            return 0;
        }

        output.Capacity = Math.Max(output.Capacity, pending.Count);
        foreach (var (pos, nextLevel) in pending)
        {
            var existing = world.GetFluidLevelOrNegative(pos);
            if (nextLevel == FluidChange.RemoveFluidSentinel)
            {
                if (existing >= 0)
                {
                    output.Add(new FluidChange(pos, FluidChange.RemoveFluidSentinel));
                }
            }
            else if (existing != nextLevel)
            {
                output.Add(new FluidChange(pos, (byte)nextLevel));
            }
        }

        output.Sort(static (a, b) => CompareBlockPos(a.Position, b.Position));
        return output.Count;
    }

    /// <summary>Computes the visual water surface height in <c>(0, 1]</c> for a given fluid <paramref name="level"/>.</summary>
    public static double GetSurfaceHeight(int level)
    {
        if (level < 0)
        {
            return 0.0;
        }

        if (level >= FallingLevel)
        {
            return 1.0;
        }

        if (level == SourceLevel)
        {
            return 0.875;
        }

        return Math.Clamp((8 - level) / 9.0, 0.125, 0.875);
    }

    private static void TryFormInfiniteSourceInEmptyCell(
        IVoxelCollisionReader world,
        BlockPos pos,
        Dictionary<BlockPos, int> pending)
    {
        if (CountAdjacentSources(world, pos) >= 2 && HasInfiniteSourceFloor(world, pos))
        {
            MergePending(pending, pos, SourceLevel);
        }
    }

    private static void EvaluateSourceOrFlowingCell(
        IVoxelCollisionReader world,
        BlockPos pos,
        int currentLevel,
        Dictionary<BlockPos, int> pending)
    {
        if (currentLevel != SourceLevel)
        {
            var recomputed = ComputeTargetLevel(world, pos);
            if (recomputed < 0)
            {
                MergePending(pending, pos, FluidChange.RemoveFluidSentinel);
                return;
            }

            if (recomputed != currentLevel)
            {
                MergePending(pending, pos, recomputed);
                currentLevel = recomputed;
            }
        }

        var belowPos = pos.Offset(Direction.Down);
        var canFlowDown = belowPos.Y >= EngineConstants.WorldMinY && !world.IsSolidBlock(belowPos);
        if (canFlowDown)
        {
            var belowLevel = world.GetFluidLevelOrNegative(belowPos);
            if (belowLevel != SourceLevel && belowLevel != FallingLevel)
            {
                MergePending(pending, belowPos, FallingLevel);
            }

            // Vanilla rule: flowing water (level > 0) with non-solid below falls straight down and NEVER spreads mid-air!
            if (currentLevel != SourceLevel)
            {
                return;
            }
        }

        var spreadBaseLevel = currentLevel >= FallingLevel ? 0 : currentLevel;
        var spreadTargetLevel = spreadBaseLevel + 1;
        if (spreadTargetLevel > MaxHorizontalLevel)
        {
            return;
        }

        var allowedMask = ComputeSlopeFlowMask(world, pos);
        var cardinals = DirectionExtensions.Horizontal;
        for (var d = 0; d < cardinals.Length; d++)
        {
            if ((allowedMask & (1 << d)) == 0)
            {
                continue;
            }

            var dir = cardinals[d];
            var sidePos = pos.Offset(dir);
            if (!world.IsColumnLoaded(sidePos) || world.IsSolidBlock(sidePos))
            {
                continue;
            }

            var sideLevel = world.GetFluidLevelOrNegative(sidePos);
            if (sideLevel == SourceLevel || sideLevel == FallingLevel)
            {
                continue;
            }

            if (sideLevel < 0 || sideLevel > spreadTargetLevel)
            {
                MergePending(pending, sidePos, spreadTargetLevel);
            }
        }
    }

    private static int ComputeSlopeFlowMask(IVoxelCollisionReader world, BlockPos origin)
    {
        var cardinals = DirectionExtensions.Horizontal;
        Span<int> costs = stackalloc int[4];
        var minCost = NoDropSentinel;

        for (var d = 0; d < 4; d++)
        {
            var dir = cardinals[d];
            var next = origin.Offset(dir);
            if (!CanWaterEnter(world, next))
            {
                costs[d] = NoDropSentinel + 1;
                continue;
            }

            var cost = IsDropOff(world, next) ? 0 : SearchSlopeDistance(world, next, dir.Opposite(), depth: 1);
            costs[d] = cost;
            if (cost < minCost)
            {
                minCost = cost;
            }
        }

        var mask = 0;
        for (var d = 0; d < 4; d++)
        {
            if (costs[d] <= minCost && costs[d] <= NoDropSentinel)
            {
                mask |= 1 << d;
            }
        }

        return mask;
    }

    private static int SearchSlopeDistance(IVoxelCollisionReader world, BlockPos current, Direction excludedDir, int depth)
    {
        var best = NoDropSentinel;
        var cardinals = DirectionExtensions.Horizontal;

        for (var d = 0; d < 4; d++)
        {
            var dir = cardinals[d];
            if (dir == excludedDir)
            {
                continue;
            }

            var next = current.Offset(dir);
            if (!CanWaterEnter(world, next))
            {
                continue;
            }

            if (IsDropOff(world, next))
            {
                return depth;
            }

            if (depth < SlopeSearchMaxDepth)
            {
                var sub = SearchSlopeDistance(world, next, dir.Opposite(), depth + 1);
                if (sub < best)
                {
                    best = sub;
                }
            }
        }

        return best;
    }

    private static bool CanWaterEnter(IVoxelCollisionReader world, BlockPos pos) =>
        world.IsColumnLoaded(pos) &&
        !world.IsSolidBlock(pos) &&
        world.GetFluidLevelOrNegative(pos) != SourceLevel;

    private static bool IsDropOff(IVoxelCollisionReader world, BlockPos pos)
    {
        var below = pos.Offset(Direction.Down);
        return below.Y >= EngineConstants.WorldMinY && !world.IsSolidBlock(below);
    }

    private static int ComputeTargetLevel(IVoxelCollisionReader world, BlockPos pos)
    {
        if (CountAdjacentSources(world, pos) >= 2 && HasInfiniteSourceFloor(world, pos))
        {
            return SourceLevel;
        }

        var abovePos = pos.Offset(Direction.Up);
        if (abovePos.Y < EngineConstants.WorldMaxY && world.GetFluidLevelOrNegative(abovePos) >= 0)
        {
            return FallingLevel;
        }

        var minNeighborLevel = int.MaxValue;
        foreach (var dir in DirectionExtensions.Horizontal)
        {
            var nPos = pos.Offset(dir);
            var nLevel = world.GetFluidLevelOrNegative(nPos);
            if (nLevel < 0)
            {
                continue;
            }

            // A falling neighbor only feeds horizontally if it rests on solid ground (or another source)!
            if (nLevel >= FallingLevel)
            {
                var nBelow = nPos.Offset(Direction.Down);
                if (nBelow.Y >= EngineConstants.WorldMinY &&
                    (world.IsSolidBlock(nBelow) || world.GetFluidLevelOrNegative(nBelow) == SourceLevel))
                {
                    minNeighborLevel = 0;
                }

                continue;
            }

            if (nLevel < minNeighborLevel)
            {
                minNeighborLevel = nLevel;
            }
        }

        if (minNeighborLevel == int.MaxValue)
        {
            return -1;
        }

        var target = minNeighborLevel + 1;
        return target <= MaxHorizontalLevel ? target : -1;
    }

    private static bool HasInfiniteSourceFloor(IVoxelCollisionReader world, BlockPos pos)
    {
        var belowPos = pos.Offset(Direction.Down);
        return belowPos.Y >= EngineConstants.WorldMinY &&
            (world.IsSolidBlock(belowPos) || world.GetFluidLevelOrNegative(belowPos) == SourceLevel);
    }

    private static int CountAdjacentSources(IVoxelCollisionReader world, BlockPos pos)
    {
        var count = 0;
        foreach (var dir in DirectionExtensions.Horizontal)
        {
            if (world.GetFluidLevelOrNegative(pos.Offset(dir)) == SourceLevel)
            {
                count++;
            }
        }

        return count;
    }

    private static void MergePending(Dictionary<BlockPos, int> pending, BlockPos pos, int candidateLevel)
    {
        if (!pending.TryGetValue(pos, out var existing))
        {
            pending[pos] = candidateLevel;
            return;
        }

        if (candidateLevel == SourceLevel || existing == SourceLevel)
        {
            pending[pos] = SourceLevel;
            return;
        }

        if (candidateLevel == FallingLevel || existing == FallingLevel)
        {
            pending[pos] = FallingLevel;
            return;
        }

        if (candidateLevel == FluidChange.RemoveFluidSentinel)
        {
            return;
        }

        if (existing == FluidChange.RemoveFluidSentinel || candidateLevel < existing)
        {
            pending[pos] = candidateLevel;
        }
    }

    private static int CompareBlockPos(BlockPos a, BlockPos b)
    {
        var cmpY = a.Y.CompareTo(b.Y);
        if (cmpY != 0)
        {
            return cmpY;
        }

        var cmpZ = a.Z.CompareTo(b.Z);
        return cmpZ != 0 ? cmpZ : a.X.CompareTo(b.X);
    }
}
