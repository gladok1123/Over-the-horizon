using System;
using System.Buffers;
using OverHorizon.Core.Math;

namespace OverHorizon.Physics;

/// <summary>
/// Deterministic swept axis-aligned bounding box solver against discrete and sub-voxel collision shapes
/// (hinged Doors, Trapdoors, Fences, Dirt Paths, Farmland, and Beds).
/// Resolves Y -> X -> Z sequentially with zero heap allocations via <see cref="ArrayPool{T}"/> + <see cref="ReadOnlySpan{T}"/>.
/// </summary>
public static class VoxelSweptCollider
{
    private const double SkinEpsilon = 1e-7;

    /// <summary>
    /// Number of collision boxes kept on the stack (no <see cref="ArrayPool{T}"/> round-trip) for ordinary entity sweeps.
    /// A player-sized box scans well under this many voxels, so virtually every sweep hits the allocation-free path.
    /// </summary>
    private const int StackObstacleCapacity = 64;

    /// <summary>
    /// Sweeps <paramref name="box"/> by <paramref name="requestedMove"/> against solid/sub-voxel shapes queried from <paramref name="world"/>.
    /// </summary>
    /// <param name="world">Collision query reader.</param>
    /// <param name="box">Initial world-space AABB.</param>
    /// <param name="requestedMove">Desired displacement vector for this step.</param>
    /// <returns>Resolved AABB, actual displacement, grounded contact flag, and horizontal collision flag.</returns>
    public static (BoundingBox Box, Vec3d ActualMove, bool OnGround, bool HitHorizontal) MoveAndCollide(
        IVoxelCollisionReader world,
        BoundingBox box,
        Vec3d requestedMove)
    {
        ArgumentNullException.ThrowIfNull(world);

        // A zero-length sweep can neither ground nor block the entity, so skip the broadphase scan entirely.
        if (requestedMove.X == 0.0 && requestedMove.Y == 0.0 && requestedMove.Z == 0.0)
        {
            return (box, Vec3d.Zero, OnGround: false, HitHorizontal: false);
        }

        // Expand broadphase scan downward by 0.55 blocks so 1.5-tall Fence posts below feet are always collected!
        var expanded = box.ExpandTowards(requestedMove);
        var minX = (int)Math.Floor(expanded.MinX - SkinEpsilon);
        var maxX = (int)Math.Floor(expanded.MaxX + SkinEpsilon);
        var minY = (int)Math.Floor(expanded.MinY - 0.55 - SkinEpsilon);
        var maxY = (int)Math.Floor(expanded.MaxY + SkinEpsilon);
        var minZ = (int)Math.Floor(expanded.MinZ - SkinEpsilon);
        var maxZ = (int)Math.Floor(expanded.MaxZ + SkinEpsilon);

        var maxCells = Math.Max(16, (maxX - minX + 1) * (maxY - minY + 1) * (maxZ - minZ + 1));

        // Fast path: ordinary sweeps fit on the stack, so no pooled array is rented or returned at all.
        Span<BoundingBox> stackObstacles = stackalloc BoundingBox[StackObstacleCapacity];
        BoundingBox[]? rented = maxCells > StackObstacleCapacity
            ? ArrayPool<BoundingBox>.Shared.Rent(Math.Min(maxCells, 4096))
            : null;

        try
        {
            var scratch = rented is null ? stackObstacles : rented.AsSpan();
            var count = CollectObstacles(world, minX, maxX, minY, maxY, minZ, maxZ, scratch);
            ReadOnlySpan<BoundingBox> obstacles = scratch[..count];

            var resolvedY = ResolveAxisY(box, requestedMove.Y, obstacles);
            box = box.Move(new Vec3d(0.0, resolvedY, 0.0));

            var resolvedX = ResolveAxisX(box, requestedMove.X, obstacles);
            box = box.Move(new Vec3d(resolvedX, 0.0, 0.0));

            var resolvedZ = ResolveAxisZ(box, requestedMove.Z, obstacles);
            box = box.Move(new Vec3d(0.0, 0.0, resolvedZ));

            var collidedY = Math.Abs(resolvedY - requestedMove.Y) > SkinEpsilon;
            var collidedX = Math.Abs(resolvedX - requestedMove.X) > SkinEpsilon;
            var collidedZ = Math.Abs(resolvedZ - requestedMove.Z) > SkinEpsilon;
            var grounded = collidedY && requestedMove.Y < 0.0;

            return (
                box,
                new Vec3d(resolvedX, resolvedY, resolvedZ),
                grounded,
                collidedX || collidedZ);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<BoundingBox>.Shared.Return(rented);
            }
        }
    }

    /// <summary>
    /// Gathers every solid or sub-voxel collision box overlapping the swept voxel range into <paramref name="scratch"/>.
    /// </summary>
    /// <param name="world">Collision query reader.</param>
    /// <param name="minX">Inclusive minimum voxel X.</param>
    /// <param name="maxX">Inclusive maximum voxel X.</param>
    /// <param name="minY">Inclusive minimum voxel Y.</param>
    /// <param name="maxY">Inclusive maximum voxel Y.</param>
    /// <param name="minZ">Inclusive minimum voxel Z.</param>
    /// <param name="maxZ">Inclusive maximum voxel Z.</param>
    /// <param name="scratch">Destination span, large enough for the whole range.</param>
    /// <returns>Number of collision boxes written.</returns>
    private static int CollectObstacles(
        IVoxelCollisionReader world,
        int minX,
        int maxX,
        int minY,
        int maxY,
        int minZ,
        int maxZ,
        Span<BoundingBox> scratch)
    {
        var count = 0;
        for (var y = minY; y <= maxY; y++)
        {
            for (var z = minZ; z <= maxZ; z++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    if (!world.TryGetCollisionBox(new BlockPos(x, y, z), out var cellBox))
                    {
                        continue;
                    }

                    scratch[count++] = cellBox;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Determines whether <paramref name="box"/> strictly overlaps any solid or sub-voxel collision box in <paramref name="world"/>.
    /// </summary>
    /// <param name="world">Collision query reader.</param>
    /// <param name="box">Candidate entity or block placement box.</param>
    /// <returns><see langword="true"/> when intersecting any solid shape; otherwise <see langword="false"/>.</returns>
    public static bool IntersectsAnySolid(IVoxelCollisionReader world, BoundingBox box)
    {
        ArgumentNullException.ThrowIfNull(world);

        var shrunk = box.Inflate(-SkinEpsilon, -SkinEpsilon, -SkinEpsilon);
        var minX = (int)Math.Floor(shrunk.MinX);
        var maxX = (int)Math.Floor(shrunk.MaxX);
        var minY = (int)Math.Floor(shrunk.MinY - 0.55);
        var maxY = (int)Math.Floor(shrunk.MaxY);
        var minZ = (int)Math.Floor(shrunk.MinZ);
        var maxZ = (int)Math.Floor(shrunk.MaxZ);

        for (var y = minY; y <= maxY; y++)
        {
            for (var z = minZ; z <= maxZ; z++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    if (world.TryGetCollisionBox(new BlockPos(x, y, z), out var cellBox) &&
                        shrunk.Intersects(cellBox))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static double ResolveAxisY(BoundingBox moving, double dy, ReadOnlySpan<BoundingBox> obstacles)
    {
        if (Math.Abs(dy) <= SkinEpsilon)
        {
            return 0.0;
        }

        for (var i = 0; i < obstacles.Length; i++)
        {
            ref readonly var obs = ref obstacles[i];
            if (moving.MaxX <= obs.MinX + SkinEpsilon || moving.MinX >= obs.MaxX - SkinEpsilon ||
                moving.MaxZ <= obs.MinZ + SkinEpsilon || moving.MinZ >= obs.MaxZ - SkinEpsilon)
            {
                continue;
            }

            if (dy > 0.0 && moving.MaxY <= obs.MinY + SkinEpsilon)
            {
                var maxAllowed = obs.MinY - moving.MaxY;
                if (maxAllowed < dy)
                {
                    dy = Math.Max(0.0, maxAllowed);
                }
            }
            else if (dy < 0.0 && moving.MinY >= obs.MaxY - SkinEpsilon)
            {
                var minAllowed = obs.MaxY - moving.MinY;
                if (minAllowed > dy)
                {
                    dy = Math.Min(0.0, minAllowed);
                }
            }
        }

        return dy;
    }

    private static double ResolveAxisX(BoundingBox moving, double dx, ReadOnlySpan<BoundingBox> obstacles)
    {
        if (Math.Abs(dx) <= SkinEpsilon)
        {
            return 0.0;
        }

        for (var i = 0; i < obstacles.Length; i++)
        {
            ref readonly var obs = ref obstacles[i];
            if (moving.MaxY <= obs.MinY + SkinEpsilon || moving.MinY >= obs.MaxY - SkinEpsilon ||
                moving.MaxZ <= obs.MinZ + SkinEpsilon || moving.MinZ >= obs.MaxZ - SkinEpsilon)
            {
                continue;
            }

            if (dx > 0.0 && moving.MaxX <= obs.MinX + SkinEpsilon)
            {
                var maxAllowed = obs.MinX - moving.MaxX;
                if (maxAllowed < dx)
                {
                    dx = Math.Max(0.0, maxAllowed);
                }
            }
            else if (dx < 0.0 && moving.MinX >= obs.MaxX - SkinEpsilon)
            {
                var minAllowed = obs.MaxX - moving.MinX;
                if (minAllowed > dx)
                {
                    dx = Math.Min(0.0, minAllowed);
                }
            }
        }

        return dx;
    }

    private static double ResolveAxisZ(BoundingBox moving, double dz, ReadOnlySpan<BoundingBox> obstacles)
    {
        if (Math.Abs(dz) <= SkinEpsilon)
        {
            return 0.0;
        }

        for (var i = 0; i < obstacles.Length; i++)
        {
            ref readonly var obs = ref obstacles[i];
            if (moving.MaxX <= obs.MinX + SkinEpsilon || moving.MinX >= obs.MaxX - SkinEpsilon ||
                moving.MaxY <= obs.MinY + SkinEpsilon || moving.MinY >= obs.MaxY - SkinEpsilon)
            {
                continue;
            }

            if (dz > 0.0 && moving.MaxZ <= obs.MinZ + SkinEpsilon)
            {
                var maxAllowed = obs.MinZ - moving.MaxZ;
                if (maxAllowed < dz)
                {
                    dz = Math.Max(0.0, maxAllowed);
                }
            }
            else if (dz < 0.0 && moving.MinZ >= obs.MaxZ - SkinEpsilon)
            {
                var minAllowed = obs.MaxZ - moving.MinZ;
                if (minAllowed > dz)
                {
                    dz = Math.Min(0.0, minAllowed);
                }
            }
        }

        return dz;
    }
}
