using System.Numerics;
using OverHorizon.Core.Math;

namespace OverHorizon.Physics;

/// <summary>
/// Exact Amanatides &amp; Woo 3D DDA voxel raycaster with sub-voxel AABB selection support (<see cref="IVoxelSelectionReader"/>)
/// for block breaking, placement, Torches, Ladders, Redstone components, and Beds.
/// </summary>
public static class VoxelRaycaster
{
    /// <summary>
    /// Casts a ray from <paramref name="origin"/> along <paramref name="direction"/> up to <paramref name="maxDistance"/>
    /// blocks and returns the first selectable voxel intersected.
    /// </summary>
    public static bool TryRaycast(
        IVoxelCollisionReader world,
        Vec3d origin,
        Vec3d direction,
        double maxDistance,
        out VoxelRaycastHit hit)
    {
        ArgumentNullException.ThrowIfNull(world);

        var lenSq = direction.LengthSquared;
        if (lenSq < 1e-12 || maxDistance <= 0.0)
        {
            hit = default;
            return false;
        }

        var dir = direction.Normalize();
        var selectionReader = world as IVoxelSelectionReader;
        var currentPos = origin.ToBlockPos();

        var stepX = Math.Sign(dir.X);
        var stepY = Math.Sign(dir.Y);
        var stepZ = Math.Sign(dir.Z);

        var tDeltaX = stepX != 0 ? Math.Abs(1.0 / dir.X) : double.PositiveInfinity;
        var tDeltaY = stepY != 0 ? Math.Abs(1.0 / dir.Y) : double.PositiveInfinity;
        var tDeltaZ = stepZ != 0 ? Math.Abs(1.0 / dir.Z) : double.PositiveInfinity;

        var nextBoundaryX = stepX > 0 ? currentPos.X + 1.0 : currentPos.X;
        var nextBoundaryY = stepY > 0 ? currentPos.Y + 1.0 : currentPos.Y;
        var nextBoundaryZ = stepZ > 0 ? currentPos.Z + 1.0 : currentPos.Z;

        var tMaxX = stepX != 0 ? (nextBoundaryX - origin.X) / dir.X : double.PositiveInfinity;
        var tMaxY = stepY != 0 ? (nextBoundaryY - origin.Y) / dir.Y : double.PositiveInfinity;
        var tMaxZ = stepZ != 0 ? (nextBoundaryZ - origin.Z) / dir.Z : double.PositiveInfinity;

        var traveled = 0.0;
        var entryFace = DominantOppositeDirection(dir);

        for (var step = 0; step < 512 && traveled <= maxDistance; step++)
        {
            if (world.IsColumnLoaded(currentPos))
            {
                if (selectionReader is not null)
                {
                    if (selectionReader.TryGetSelectionBox(currentPos, out var localBox))
                    {
                        var worldBox = new BoundingBox(
                            currentPos.X + localBox.MinX,
                            currentPos.Y + localBox.MinY,
                            currentPos.Z + localBox.MinZ,
                            currentPos.X + localBox.MaxX,
                            currentPos.Y + localBox.MaxY,
                            currentPos.Z + localBox.MaxZ);

                        if (TryIntersectRayBox(origin, dir, maxDistance, worldBox, entryFace, out var boxT, out var boxFace))
                        {
                            var hitPoint = origin + (dir * boxT);
                            hit = new VoxelRaycastHit(currentPos, currentPos.Offset(boxFace), boxFace, hitPoint, boxT);
                            return true;
                        }
                    }
                }
                else if (world.IsSolidBlock(currentPos))
                {
                    var hitPoint = origin + (dir * traveled);
                    var adjacent = currentPos.Offset(entryFace);
                    hit = new VoxelRaycastHit(currentPos, adjacent, entryFace, hitPoint, traveled);
                    return true;
                }
            }

            if (tMaxX < tMaxY)
            {
                if (tMaxX < tMaxZ)
                {
                    traveled = tMaxX;
                    tMaxX += tDeltaX;
                    currentPos = new BlockPos(currentPos.X + stepX, currentPos.Y, currentPos.Z);
                    entryFace = stepX > 0 ? Direction.West : Direction.East;
                }
                else
                {
                    traveled = tMaxZ;
                    tMaxZ += tDeltaZ;
                    currentPos = new BlockPos(currentPos.X, currentPos.Y, currentPos.Z + stepZ);
                    entryFace = stepZ > 0 ? Direction.North : Direction.South;
                }
            }
            else if (tMaxY < tMaxZ)
            {
                traveled = tMaxY;
                tMaxY += tDeltaY;
                currentPos = new BlockPos(currentPos.X, currentPos.Y + stepY, currentPos.Z);
                entryFace = stepY > 0 ? Direction.Down : Direction.Up;
            }
            else
            {
                traveled = tMaxZ;
                tMaxZ += tDeltaZ;
                currentPos = new BlockPos(currentPos.X, currentPos.Y, currentPos.Z + stepZ);
                entryFace = stepZ > 0 ? Direction.North : Direction.South;
            }
        }

        hit = default;
        return false;
    }

    /// <summary>
    /// Overload accepting a single-precision <see cref="Vector3"/> camera forward vector.
    /// </summary>
    public static bool TryRaycast(
        IVoxelCollisionReader world,
        Vec3d origin,
        Vector3 direction,
        double maxDistance,
        out VoxelRaycastHit hit) =>
        TryRaycast(world, origin, new Vec3d(direction.X, direction.Y, direction.Z), maxDistance, out hit);

    private static bool TryIntersectRayBox(
        Vec3d origin,
        Vec3d dir,
        double maxDistance,
        BoundingBox box,
        Direction fallbackFace,
        out double hitT,
        out Direction hitFace)
    {
        var tMin = 0.0;
        var tMax = maxDistance;
        var bestFace = fallbackFace;

        if (!ClipSlab(origin.X, dir.X, box.MinX, box.MaxX, Direction.West, Direction.East, ref tMin, ref tMax, ref bestFace) ||
            !ClipSlab(origin.Y, dir.Y, box.MinY, box.MaxY, Direction.Down, Direction.Up, ref tMin, ref tMax, ref bestFace) ||
            !ClipSlab(origin.Z, dir.Z, box.MinZ, box.MaxZ, Direction.North, Direction.South, ref tMin, ref tMax, ref bestFace))
        {
            hitT = 0.0;
            hitFace = fallbackFace;
            return false;
        }

        hitT = tMin;
        hitFace = bestFace;
        return hitT <= maxDistance;
    }

    private static bool ClipSlab(
        double ro,
        double rd,
        double bMin,
        double bMax,
        Direction minFace,
        Direction maxFace,
        ref double tMin,
        ref double tMax,
        ref Direction enterFace)
    {
        if (Math.Abs(rd) < 1e-9)
        {
            return ro >= bMin && ro <= bMax;
        }

        var inv = 1.0 / rd;
        var t1 = (bMin - ro) * inv;
        var t2 = (bMax - ro) * inv;
        var face = minFace;
        if (t1 > t2)
        {
            (t1, t2) = (t2, t1);
            face = maxFace;
        }

        if (t1 > tMin)
        {
            tMin = t1;
            enterFace = face;
        }

        if (t2 < tMax)
        {
            tMax = t2;
        }

        return tMin <= tMax;
    }

    private static Direction DominantOppositeDirection(Vec3d dir)
    {
        var ax = Math.Abs(dir.X);
        var ay = Math.Abs(dir.Y);
        var az = Math.Abs(dir.Z);

        if (ax >= ay && ax >= az)
        {
            return dir.X >= 0.0 ? Direction.West : Direction.East;
        }

        if (ay >= ax && ay >= az)
        {
            return dir.Y >= 0.0 ? Direction.Down : Direction.Up;
        }

        return dir.Z >= 0.0 ? Direction.North : Direction.South;
    }
}
