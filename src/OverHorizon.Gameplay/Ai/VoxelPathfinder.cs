using OverHorizon.Core;
using OverHorizon.Core.Math;
using OverHorizon.Physics;

namespace OverHorizon.Gameplay.Ai;

/// <summary>
/// Deterministic, bounded 3D voxel A* pathfinder supporting flat walking, 1-block jumps, and safe ledge drops.
/// Safe to invoke concurrently from parallel AI threads over <see cref="IVoxelCollisionReader"/> snapshots.
/// </summary>
public static class VoxelPathfinder
{
    /// <summary>
    /// Computes an obstacle-free walkable path of <see cref="BlockPos"/> waypoints from <paramref name="start"/> to <paramref name="goal"/>.
    /// Returns an empty list if <paramref name="goal"/> cannot be reached within <paramref name="maxExpandedNodes"/>.
    /// </summary>
    /// <param name="world">Read-only voxel collision and fluid source.</param>
    /// <param name="start">Starting feet block coordinate.</param>
    /// <param name="goal">Target feet block coordinate.</param>
    /// <param name="entityHeightBlocks">Vertical clearance required in blocks (<c>&gt;= 1</c>).</param>
    /// <param name="maxDropBlocks">Maximum safe downward ledge drop in blocks (<c>&gt;= 0</c>).</param>
    /// <param name="maxExpandedNodes">Hard cap on A* node expansions to bound worst-case CPU cost.</param>
    /// <returns>Ordered list of waypoints including <paramref name="start"/> and <paramref name="goal"/>, or empty if unreachable.</returns>
    public static IReadOnlyList<BlockPos> FindPath(
        IVoxelCollisionReader world,
        BlockPos start,
        BlockPos goal,
        int entityHeightBlocks = 2,
        int maxDropBlocks = 3,
        int maxExpandedNodes = 512)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (entityHeightBlocks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(entityHeightBlocks));
        }

        if (maxDropBlocks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDropBlocks));
        }

        if (maxExpandedNodes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExpandedNodes));
        }

        if (!IsStandable(world, start, entityHeightBlocks))
        {
            return [];
        }

        if (start == goal)
        {
            return [start];
        }

        var nodes = new List<PathNodeRecord>(Math.Min(maxExpandedNodes, 128))
        {
            new(start, ParentIndex: -1, GScore: 0.0),
        };

        var bestG = new Dictionary<BlockPos, double>(128)
        {
            [start] = 0.0,
        };

        var closed = new HashSet<BlockPos>(128);
        var openQueue = new PriorityQueue<int, (double FScore, BlockPos TieBreaker)>(64);
        openQueue.Enqueue(0, (Heuristic(start, goal), start));

        var expansions = 0;
        var horizDirs = DirectionExtensions.Horizontal;

        while (openQueue.Count > 0 && expansions < maxExpandedNodes)
        {
            var currentIdx = openQueue.Dequeue();
            var currentNode = nodes[currentIdx];

            if (!closed.Add(currentNode.Position))
            {
                continue;
            }

            if (currentNode.Position == goal)
            {
                return ReconstructPath(nodes, currentIdx);
            }

            expansions++;

            for (var d = 0; d < horizDirs.Length; d++)
            {
                var normal = horizDirs[d].Normal();
                if (!TryResolveNeighborStep(
                        world,
                        currentNode.Position,
                        normal.X,
                        normal.Z,
                        entityHeightBlocks,
                        maxDropBlocks,
                        out var neighborPos,
                        out var stepCost))
                {
                    continue;
                }

                if (closed.Contains(neighborPos))
                {
                    continue;
                }

                var tentativeG = currentNode.GScore + stepCost;
                if (bestG.TryGetValue(neighborPos, out var existingG) && tentativeG >= existingG)
                {
                    continue;
                }

                bestG[neighborPos] = tentativeG;
                var nextIdx = nodes.Count;
                nodes.Add(new PathNodeRecord(neighborPos, currentIdx, tentativeG));
                openQueue.Enqueue(nextIdx, (tentativeG + Heuristic(neighborPos, goal), neighborPos));
            }
        }

        return [];
    }

    private static bool TryResolveNeighborStep(
        IVoxelCollisionReader world,
        BlockPos from,
        int dx,
        int dz,
        int height,
        int maxDrop,
        out BlockPos resolved,
        out double cost)
    {
        var nx = from.X + dx;
        var nz = from.Z + dz;

        // 1. Flat horizontal walk.
        var flat = new BlockPos(nx, from.Y, nz);
        if (IsStandable(world, flat, height))
        {
            resolved = flat;
            cost = 1.0;
            return true;
        }

        // 2. 1-block jump / step-up (requires overhead clearance above 'from').
        var up = new BlockPos(nx, from.Y + 1, nz);
        if (HasHeadroom(world, new BlockPos(from.X, from.Y + height, from.Z), 1) &&
            IsStandable(world, up, height))
        {
            resolved = up;
            cost = 1.4;
            return true;
        }

        // 3. Ledge drop up to maxDrop blocks (requires horizontal clearance at ledge height first).
        if (maxDrop > 0 && HasHeadroom(world, flat, height))
        {
            for (var drop = 1; drop <= maxDrop; drop++)
            {
                var dropY = from.Y - drop;
                if (dropY <= EngineConstants.WorldMinY)
                {
                    break;
                }

                var candidate = new BlockPos(nx, dropY, nz);
                if (!HasHeadroom(world, candidate, 1))
                {
                    // Hit an obstacle mid-drop before finding standable floor.
                    break;
                }

                if (IsStandable(world, candidate, height))
                {
                    resolved = candidate;
                    cost = 1.0 + (drop * 0.5);
                    return true;
                }
            }
        }

        resolved = default;
        cost = 0.0;
        return false;
    }

    private static bool IsStandable(IVoxelCollisionReader world, BlockPos feet, int height)
    {
        if (feet.Y <= EngineConstants.WorldMinY || feet.Y + height > EngineConstants.WorldMaxY || !world.IsColumnLoaded(feet))
        {
            return false;
        }

        var ground = feet.Offset(Direction.Down);
        if (!world.IsSolidBlock(ground))
        {
            return false;
        }

        return HasHeadroom(world, feet, height);
    }

    private static bool HasHeadroom(IVoxelCollisionReader world, BlockPos basePos, int clearanceBlocks)
    {
        for (var dy = 0; dy < clearanceBlocks; dy++)
        {
            var check = basePos.Offset(0, dy, 0);
            if (check.Y < EngineConstants.WorldMinY || check.Y >= EngineConstants.WorldMaxY)
            {
                return false;
            }

            if (world.IsSolidBlock(check) || world.GetFluidLevelOrNegative(check) >= 0)
            {
                return false;
            }
        }

        return true;
    }

    private static double Heuristic(BlockPos a, BlockPos b) =>
        Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z);

    private static BlockPos[] ReconstructPath(List<PathNodeRecord> nodes, int goalIndex)
    {
        var count = 0;
        for (var cur = goalIndex; cur >= 0; cur = nodes[cur].ParentIndex)
        {
            count++;
        }

        var path = new BlockPos[count];
        var writeIdx = count - 1;
        for (var cur = goalIndex; cur >= 0; cur = nodes[cur].ParentIndex)
        {
            path[writeIdx--] = nodes[cur].Position;
        }

        return path;
    }
}
