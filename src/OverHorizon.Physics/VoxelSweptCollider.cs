using OverHorizon.Core.Math;

namespace OverHorizon.Physics;

/// <summary>
/// Allocation-free axis-by-axis swept AABB voxel collision solver with anti-tunneling sub-stepping.
/// </summary>
public static class VoxelSweptCollider
{
    /// <summary>
    /// Moves <paramref name="initialBox"/> by <paramref name="displacement"/> while resolving solid voxel collisions
    /// along Y, then X, then Z. Automatically sub-steps large displacements to prevent tunneling.
    /// </summary>
    /// <param name="world">Read-only voxel collision source.</param>
    /// <param name="initialBox">Starting entity bounding box.</param>
    /// <param name="displacement">Requested motion vector for this step.</param>
    /// <returns>Tuple of resolved <see cref="BoundingBox"/>, actual applied motion, <c>OnGround</c>, and <c>CollidedHorizontally</c>.</returns>
    public static (BoundingBox Box, Vec3d ActualMove, bool OnGround, bool CollidedHorizontally) MoveAndCollide(
        IVoxelCollisionReader world,
        BoundingBox initialBox,
        Vec3d displacement)
    {
        ArgumentNullException.ThrowIfNull(world);

        var distance = displacement.Length;
        if (distance <= 0.0)
        {
            return (initialBox, Vec3d.Zero, false, false);
        }

        var substeps = Math.Max(1, (int)Math.Ceiling(distance / PhysicsConstants.MaxSubstepDisplacement));
        var stepDelta = displacement * (1.0 / substeps);

        var currentBox = initialBox;
        var onGround = false;
        var hitHorizontal = false;
        var remainingStep = stepDelta;

        for (var s = 0; s < substeps; s++)
        {
            var resolvedY = ResolveAxisY(world, currentBox, remainingStep.Y);
            if (Math.Abs(resolvedY - remainingStep.Y) > 1e-9)
            {
                if (remainingStep.Y < 0.0)
                {
                    onGround = true;
                }

                remainingStep = new Vec3d(remainingStep.X, 0.0, remainingStep.Z);
            }

            currentBox = currentBox.Move(new Vec3d(0.0, resolvedY, 0.0));

            var resolvedX = ResolveAxisX(world, currentBox, remainingStep.X);
            if (Math.Abs(resolvedX - remainingStep.X) > 1e-9)
            {
                hitHorizontal = true;
                remainingStep = new Vec3d(0.0, remainingStep.Y, remainingStep.Z);
            }

            currentBox = currentBox.Move(new Vec3d(resolvedX, 0.0, 0.0));

            var resolvedZ = ResolveAxisZ(world, currentBox, remainingStep.Z);
            if (Math.Abs(resolvedZ - remainingStep.Z) > 1e-9)
            {
                hitHorizontal = true;
                remainingStep = new Vec3d(remainingStep.X, remainingStep.Y, 0.0);
            }

            currentBox = currentBox.Move(new Vec3d(0.0, 0.0, resolvedZ));
        }

        var actualMove = new Vec3d(
            currentBox.MinX - initialBox.MinX,
            currentBox.MinY - initialBox.MinY,
            currentBox.MinZ - initialBox.MinZ);

        return (currentBox, actualMove, onGround, hitHorizontal);
    }

    /// <summary>
    /// Determines whether <paramref name="box"/> intersects any solid block in <paramref name="world"/>.
    /// </summary>
    /// <param name="world">Collision reader.</param>
    /// <param name="box">Query bounding box.</param>
    /// <returns><see langword="true"/> if overlapping at least one solid block.</returns>
    public static bool IntersectsAnySolid(IVoxelCollisionReader world, BoundingBox box)
    {
        ArgumentNullException.ThrowIfNull(world);

        var minX = (int)Math.Floor(box.MinX + PhysicsConstants.CollisionEpsilon);
        var maxX = (int)Math.Floor(box.MaxX - PhysicsConstants.CollisionEpsilon);
        var minY = (int)Math.Floor(box.MinY + PhysicsConstants.CollisionEpsilon);
        var maxY = (int)Math.Floor(box.MaxY - PhysicsConstants.CollisionEpsilon);
        var minZ = (int)Math.Floor(box.MinZ + PhysicsConstants.CollisionEpsilon);
        var maxZ = (int)Math.Floor(box.MaxZ - PhysicsConstants.CollisionEpsilon);

        for (var y = minY; y <= maxY; y++)
        {
            for (var z = minZ; z <= maxZ; z++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var pos = new BlockPos(x, y, z);
                    if (world.IsSolidBlock(pos) && box.Intersects(BoundingBox.FromBlock(pos)))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static double ResolveAxisY(IVoxelCollisionReader world, BoundingBox box, double dy)
    {
        if (Math.Abs(dy) < 1e-12)
        {
            return 0.0;
        }

        var swept = box.ExpandTowards(new Vec3d(0.0, dy, 0.0));
        var minX = (int)Math.Floor(swept.MinX + PhysicsConstants.CollisionEpsilon);
        var maxX = (int)Math.Floor(swept.MaxX - PhysicsConstants.CollisionEpsilon);
        var minY = (int)Math.Floor(swept.MinY + PhysicsConstants.CollisionEpsilon);
        var maxY = (int)Math.Floor(swept.MaxY - PhysicsConstants.CollisionEpsilon);
        var minZ = (int)Math.Floor(swept.MinZ + PhysicsConstants.CollisionEpsilon);
        var maxZ = (int)Math.Floor(swept.MaxZ - PhysicsConstants.CollisionEpsilon);

        for (var y = minY; y <= maxY; y++)
        {
            for (var z = minZ; z <= maxZ; z++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    if (!world.IsSolidBlock(new BlockPos(x, y, z)))
                    {
                        continue;
                    }

                    var bMinX = (double)x;
                    var bMaxX = x + 1.0;
                    var bMinZ = (double)z;
                    var bMaxZ = z + 1.0;

                    if (box.MaxX <= bMinX + PhysicsConstants.CollisionEpsilon ||
                        box.MinX >= bMaxX - PhysicsConstants.CollisionEpsilon ||
                        box.MaxZ <= bMinZ + PhysicsConstants.CollisionEpsilon ||
                        box.MinZ >= bMaxZ - PhysicsConstants.CollisionEpsilon)
                    {
                        continue;
                    }

                    var bMinY = (double)y;
                    var bMaxY = y + 1.0;

                    if (dy > 0.0 && box.MaxY <= bMinY + PhysicsConstants.CollisionEpsilon)
                    {
                        var gap = bMinY - box.MaxY;
                        if (gap < dy)
                        {
                            dy = Math.Max(0.0, gap);
                        }
                    }
                    else if (dy < 0.0 && box.MinY >= bMaxY - PhysicsConstants.CollisionEpsilon)
                    {
                        var gap = bMaxY - box.MinY;
                        if (gap > dy)
                        {
                            dy = Math.Min(0.0, gap);
                        }
                    }
                }
            }
        }

        return dy;
    }

    private static double ResolveAxisX(IVoxelCollisionReader world, BoundingBox box, double dx)
    {
        if (Math.Abs(dx) < 1e-12)
        {
            return 0.0;
        }

        var swept = box.ExpandTowards(new Vec3d(dx, 0.0, 0.0));
        var minX = (int)Math.Floor(swept.MinX + PhysicsConstants.CollisionEpsilon);
        var maxX = (int)Math.Floor(swept.MaxX - PhysicsConstants.CollisionEpsilon);
        var minY = (int)Math.Floor(swept.MinY + PhysicsConstants.CollisionEpsilon);
        var maxY = (int)Math.Floor(swept.MaxY - PhysicsConstants.CollisionEpsilon);
        var minZ = (int)Math.Floor(swept.MinZ + PhysicsConstants.CollisionEpsilon);
        var maxZ = (int)Math.Floor(swept.MaxZ - PhysicsConstants.CollisionEpsilon);

        for (var y = minY; y <= maxY; y++)
        {
            for (var z = minZ; z <= maxZ; z++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    if (!world.IsSolidBlock(new BlockPos(x, y, z)))
                    {
                        continue;
                    }

                    var bMinY = (double)y;
                    var bMaxY = y + 1.0;
                    var bMinZ = (double)z;
                    var bMaxZ = z + 1.0;

                    if (box.MaxY <= bMinY + PhysicsConstants.CollisionEpsilon ||
                        box.MinY >= bMaxY - PhysicsConstants.CollisionEpsilon ||
                        box.MaxZ <= bMinZ + PhysicsConstants.CollisionEpsilon ||
                        box.MinZ >= bMaxZ - PhysicsConstants.CollisionEpsilon)
                    {
                        continue;
                    }

                    var bMinX = (double)x;
                    var bMaxX = x + 1.0;

                    if (dx > 0.0 && box.MaxX <= bMinX + PhysicsConstants.CollisionEpsilon)
                    {
                        var gap = bMinX - box.MaxX;
                        if (gap < dx)
                        {
                            dx = Math.Max(0.0, gap);
                        }
                    }
                    else if (dx < 0.0 && box.MinX >= bMaxX - PhysicsConstants.CollisionEpsilon)
                    {
                        var gap = bMaxX - box.MinX;
                        if (gap > dx)
                        {
                            dx = Math.Min(0.0, gap);
                        }
                    }
                }
            }
        }

        return dx;
    }

    private static double ResolveAxisZ(IVoxelCollisionReader world, BoundingBox box, double dz)
    {
        if (Math.Abs(dz) < 1e-12)
        {
            return 0.0;
        }

        var swept = box.ExpandTowards(new Vec3d(0.0, 0.0, dz));
        var minX = (int)Math.Floor(swept.MinX + PhysicsConstants.CollisionEpsilon);
        var maxX = (int)Math.Floor(swept.MaxX - PhysicsConstants.CollisionEpsilon);
        var minY = (int)Math.Floor(swept.MinY + PhysicsConstants.CollisionEpsilon);
        var maxY = (int)Math.Floor(swept.MaxY - PhysicsConstants.CollisionEpsilon);
        var minZ = (int)Math.Floor(swept.MinZ + PhysicsConstants.CollisionEpsilon);
        var maxZ = (int)Math.Floor(swept.MaxZ - PhysicsConstants.CollisionEpsilon);

        for (var y = minY; y <= maxY; y++)
        {
            for (var z = minZ; z <= maxZ; z++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    if (!world.IsSolidBlock(new BlockPos(x, y, z)))
                    {
                        continue;
                    }

                    var bMinX = (double)x;
                    var bMaxX = x + 1.0;
                    var bMinY = (double)y;
                    var bMaxY = y + 1.0;

                    if (box.MaxX <= bMinX + PhysicsConstants.CollisionEpsilon ||
                        box.MinX >= bMaxX - PhysicsConstants.CollisionEpsilon ||
                        box.MaxY <= bMinY + PhysicsConstants.CollisionEpsilon ||
                        box.MinY >= bMaxY - PhysicsConstants.CollisionEpsilon)
                    {
                        continue;
                    }

                    var bMinZ = (double)z;
                    var bMaxZ = z + 1.0;

                    if (dz > 0.0 && box.MaxZ <= bMinZ + PhysicsConstants.CollisionEpsilon)
                    {
                        var gap = bMinZ - box.MaxZ;
                        if (gap < dz)
                        {
                            dz = Math.Max(0.0, gap);
                        }
                    }
                    else if (dz < 0.0 && box.MinZ >= bMaxZ - PhysicsConstants.CollisionEpsilon)
                    {
                        var gap = bMaxZ - box.MinZ;
                        if (gap > dz)
                        {
                            dz = Math.Min(0.0, gap);
                        }
                    }
                }
            }
        }

        return dz;
    }
}
