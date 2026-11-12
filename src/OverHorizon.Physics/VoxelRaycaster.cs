using System.Numerics;
using OverHorizon.Core.Math;

namespace OverHorizon.Physics;

/// <summary>
/// Allocation-free Amanatides &amp; Woo 3D DDA voxel raycaster for block breaking, placement, and selection highlighting.
/// </summary>
public static class VoxelRaycaster
{
    /// <summary>
    /// Marches a ray from <paramref name="origin"/> along <paramref name="direction"/> up to <paramref name="maxDistance"/> blocks
    /// and returns the first solid block encountered in <paramref name="world"/>.
    /// </summary>
    /// <param name="world">Read-only voxel solidity reader.</param>
    /// <param name="origin">64-bit world-space ray origin (typically player eye position).</param>
    /// <param name="direction">Ray direction vector.</param>
    /// <param name="maxDistance">Maximum reach distance in blocks.</param>
    /// <param name="hit">Populated hit details when a solid block is intersected.</param>
    /// <returns><see langword="true"/> if a solid block was hit within <paramref name="maxDistance"/>; otherwise <see langword="false"/>.</returns>
    public static bool TryRaycast(
        IVoxelCollisionReader world,
        Vec3d origin,
        Vector3 direction,
        double maxDistance,
        out VoxelRaycastHit hit)
    {
        ArgumentNullException.ThrowIfNull(world);

        var dirLen = direction.Length();
        if (dirLen <= 1e-6f || maxDistance <= 0.0)
        {
            hit = default;
            return false;
        }

        double dx = direction.X / dirLen;
        double dy = direction.Y / dirLen;
        double dz = direction.Z / dirLen;

        var x = (int)Math.Floor(origin.X);
        var y = (int)Math.Floor(origin.Y);
        var z = (int)Math.Floor(origin.Z);

        var stepX = dx > 0.0 ? 1 : (dx < 0.0 ? -1 : 0);
        var stepY = dy > 0.0 ? 1 : (dy < 0.0 ? -1 : 0);
        var stepZ = dz > 0.0 ? 1 : (dz < 0.0 ? -1 : 0);

        var tDeltaX = stepX != 0 ? Math.Abs(1.0 / dx) : double.PositiveInfinity;
        var tDeltaY = stepY != 0 ? Math.Abs(1.0 / dy) : double.PositiveInfinity;
        var tDeltaZ = stepZ != 0 ? Math.Abs(1.0 / dz) : double.PositiveInfinity;

        var tMaxX = ComputeInitialBoundaryT(origin.X, x, stepX, dx);
        var tMaxY = ComputeInitialBoundaryT(origin.Y, y, stepY, dy);
        var tMaxZ = ComputeInitialBoundaryT(origin.Z, z, stepZ, dz);

        var distance = 0.0;
        var lastFace = Direction.Up;
        var prevPos = new BlockPos(x, y, z);

        // Guard with an upper bound on grid steps in addition to parametric distance.
        var maxSteps = (int)Math.Ceiling(maxDistance * 3.5) + 4;
        for (var i = 0; i < maxSteps && distance <= maxDistance; i++)
        {
            var currentPos = new BlockPos(x, y, z);
            if (i > 0 && world.IsSolidBlock(currentPos))
            {
                hit = new VoxelRaycastHit(currentPos, prevPos, lastFace, distance);
                return true;
            }

            prevPos = currentPos;

            if (tMaxX < tMaxY)
            {
                if (tMaxX < tMaxZ)
                {
                    distance = tMaxX;
                    tMaxX += tDeltaX;
                    x += stepX;
                    lastFace = stepX > 0 ? Direction.West : Direction.East;
                }
                else
                {
                    distance = tMaxZ;
                    tMaxZ += tDeltaZ;
                    z += stepZ;
                    lastFace = stepZ > 0 ? Direction.North : Direction.South;
                }
            }
            else if (tMaxY < tMaxZ)
            {
                distance = tMaxY;
                tMaxY += tDeltaY;
                y += stepY;
                lastFace = stepY > 0 ? Direction.Down : Direction.Up;
            }
            else
            {
                distance = tMaxZ;
                tMaxZ += tDeltaZ;
                z += stepZ;
                lastFace = stepZ > 0 ? Direction.North : Direction.South;
            }
        }

        hit = default;
        return false;
    }

    private static double ComputeInitialBoundaryT(double coord, int cell, int step, double dir)
    {
        if (step > 0)
        {
            return ((cell + 1.0) - coord) / dir;
        }

        if (step < 0)
        {
            return (cell - coord) / dir;
        }

        return double.PositiveInfinity;
    }
}
