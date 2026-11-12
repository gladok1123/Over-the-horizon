using OverHorizon.Core;
using OverHorizon.Core.Math;

namespace OverHorizon.Physics;

/// <summary>
/// Clean-room deterministic cellular fluid simulator:
/// level <c>0</c> = source, <c>1..7</c> = horizontal spread, <c>8</c> = downward falling stream.
/// </summary>
public static class FluidSimulator
{
    /// <summary>Maximum horizontal decay level before fluid stops spreading.</summary>
    public const int MaxHorizontalLevel = 7;

    /// <summary>Level representing vertically falling water.</summary>
    public const byte FallingLevel = 8;

    /// <summary>
    /// Evaluates active fluid coordinates in <paramref name="activePositions"/> against read-only <paramref name="world"/>
    /// and returns a deterministically sorted list of <see cref="FluidChange"/> mutations.
    /// </summary>
    /// <param name="world">Read-only collision and fluid state reader.</param>
    /// <param name="activePositions">Candidate fluid or neighbor positions to evaluate.</param>
    /// <returns>Sorted, deduplicated list of <see cref="FluidChange"/> mutations.</returns>
    public static IReadOnlyList<FluidChange> EvaluateStep(
        IVoxelCollisionReader world,
        ReadOnlySpan<BlockPos> activePositions)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (activePositions.IsEmpty)
        {
            return [];
        }

        var proposed = new Dictionary<BlockPos, byte>(activePositions.Length * 2);

        for (var i = 0; i < activePositions.Length; i++)
        {
            var pos = activePositions[i];
            if (pos.Y < EngineConstants.WorldMinY || pos.Y >= EngineConstants.WorldMaxY || !world.IsColumnLoaded(pos))
            {
                continue;
            }

            EvaluateSingleCellAndSpread(world, pos, proposed);
        }

        var changes = new List<FluidChange>(proposed.Count);
        foreach (var (pos, targetLevel) in proposed)
        {
            var current = world.GetFluidLevelOrNegative(pos);
            if (targetLevel == FluidChange.RemoveFluidSentinel)
            {
                if (current >= 0)
                {
                    changes.Add(new FluidChange(pos, FluidChange.RemoveFluidSentinel));
                }
            }
            else if (current != targetLevel)
            {
                changes.Add(new FluidChange(pos, targetLevel));
            }
        }

        changes.Sort();
        return changes;
    }

    private static void EvaluateSingleCellAndSpread(
        IVoxelCollisionReader world,
        BlockPos pos,
        Dictionary<BlockPos, byte> proposed)
    {
        if (world.IsSolidBlock(pos))
        {
            return;
        }

        var currentLevel = world.GetFluidLevelOrNegative(pos);

        // 1. Check infinite source formation: >= 2 horizontal source neighbors over solid or source floor.
        if (currentLevel != 0 && CanFormInfiniteSource(world, pos))
        {
            RecordBestLevel(proposed, pos, 0);
            currentLevel = 0;
        }

        // 2. Recompute non-source cell retention from above or horizontal neighbors.
        if (currentLevel > 0)
        {
            var recalculated = ComputeSupportedLevel(world, pos);
            RecordBestLevel(proposed, pos, recalculated);
            if (recalculated == FluidChange.RemoveFluidSentinel)
            {
                return;
            }

            currentLevel = recalculated;
        }

        if (currentLevel < 0)
        {
            return;
        }

        // 3. Downward flow takes priority if block below is non-solid and within world bounds.
        var below = pos.Offset(Direction.Down);
        if (below.Y >= EngineConstants.WorldMinY && !world.IsSolidBlock(below))
        {
            var belowLevel = world.GetFluidLevelOrNegative(below);
            if (belowLevel != 0)
            {
                RecordBestLevel(proposed, below, FallingLevel);
            }

            // Non-source flowing water falls straight down without spreading sideways unless resting on solid/source.
            if (currentLevel != 0 && belowLevel != 0)
            {
                return;
            }
        }

        // 4. Horizontal spread with +1 level decay up to MaxHorizontalLevel.
        var effectiveCurrent = currentLevel == FallingLevel ? 0 : currentLevel;
        if (effectiveCurrent >= MaxHorizontalLevel)
        {
            return;
        }

        var nextHorizontal = (byte)(effectiveCurrent + 1);
        foreach (var dir in DirectionExtensions.Horizontal)
        {
            var neighbor = pos.Offset(dir);
            if (!world.IsColumnLoaded(neighbor) || world.IsSolidBlock(neighbor))
            {
                continue;
            }

            var existingNeighbor = world.GetFluidLevelOrNegative(neighbor);
            if (existingNeighbor < 0 || (existingNeighbor > nextHorizontal && existingNeighbor != FallingLevel))
            {
                RecordBestLevel(proposed, neighbor, nextHorizontal);
            }
        }
    }

    private static bool CanFormInfiniteSource(IVoxelCollisionReader world, BlockPos pos)
    {
        var below = pos.Offset(Direction.Down);
        if (below.Y < EngineConstants.WorldMinY)
        {
            return false;
        }

        var supportedBelow = world.IsSolidBlock(below) || world.GetFluidLevelOrNegative(below) == 0;
        if (!supportedBelow)
        {
            return false;
        }

        var adjacentSources = 0;
        foreach (var dir in DirectionExtensions.Horizontal)
        {
            if (world.GetFluidLevelOrNegative(pos.Offset(dir)) == 0)
            {
                adjacentSources++;
                if (adjacentSources >= 2)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static byte ComputeSupportedLevel(IVoxelCollisionReader world, BlockPos pos)
    {
        var above = pos.Offset(Direction.Up);
        if (above.Y < EngineConstants.WorldMaxY && world.GetFluidLevelOrNegative(above) >= 0)
        {
            return FallingLevel;
        }

        var minNeighbor = int.MaxValue;
        foreach (var dir in DirectionExtensions.Horizontal)
        {
            var nLevel = world.GetFluidLevelOrNegative(pos.Offset(dir));
            if (nLevel < 0)
            {
                continue;
            }

            var effective = nLevel == FallingLevel ? 0 : nLevel;
            if (effective < minNeighbor)
            {
                minNeighbor = effective;
            }
        }

        if (minNeighbor < MaxHorizontalLevel)
        {
            return (byte)(minNeighbor + 1);
        }

        return FluidChange.RemoveFluidSentinel;
    }

    private static void RecordBestLevel(Dictionary<BlockPos, byte> proposed, BlockPos pos, byte candidate)
    {
        if (!proposed.TryGetValue(pos, out var existing))
        {
            proposed[pos] = candidate;
            return;
        }

        if (candidate == FluidChange.RemoveFluidSentinel)
        {
            return;
        }

        if (existing == FluidChange.RemoveFluidSentinel || FullnessRank(candidate) > FullnessRank(existing))
        {
            proposed[pos] = candidate;
        }
    }

    private static int FullnessRank(byte level) => level switch
    {
        0 => 100,
        FallingLevel => 90,
        _ => 80 - level,
    };
}
