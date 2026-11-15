using OverHorizon.Core.Math;

namespace OverHorizon.Physics;

/// <summary>
/// Thread-safe read-only query interface over voxel solidity and fluid levels (backed by immutable chunk snapshots).
/// </summary>
public interface IVoxelCollisionReader
{
    /// <summary>
    /// Determines whether the chunk column containing <paramref name="pos"/> is currently loaded for physics simulation.
    /// </summary>
    /// <param name="pos">World block coordinate.</param>
    /// <returns><see langword="true"/> if loaded.</returns>
    bool IsColumnLoaded(BlockPos pos);

    /// <summary>
    /// Determines whether <paramref name="pos"/> contains a solid 1x1x1 collidable block.
    /// </summary>
    /// <param name="pos">World block coordinate.</param>
    /// <returns><see langword="true"/> if solid.</returns>
    bool IsSolidBlock(BlockPos pos);

    /// <summary>
    /// Returns the fluid level (<c>0..15</c>, where <c>0</c> = source, <c>1..7</c> = horizontal decay, <c>8</c> = falling)
    /// at <paramref name="pos"/>, or <c>-1</c> if the block is not a fluid.
    /// </summary>
    /// <param name="pos">World block coordinate.</param>
    /// <returns>Fluid level in <c>[0, 15]</c>, or <c>-1</c> if non-fluid.</returns>
    int GetFluidLevelOrNegative(BlockPos pos);

    /// <summary>
    /// Resolves the exact world-space collision <see cref="BoundingBox"/> at <paramref name="pos"/> (including sub-voxel Doors, Trapdoors, Fences, Paths, and Beds).
    /// </summary>
    /// <param name="pos">World block coordinate.</param>
    /// <param name="box">Resolved world-space AABB when returning <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="pos"/> opposes movement; otherwise <see langword="false"/>.</returns>
    bool TryGetCollisionBox(BlockPos pos, out BoundingBox box)
    {
        if (IsSolidBlock(pos))
        {
            box = BoundingBox.FromBlock(pos);
            return true;
        }

        box = default;
        return false;
    }
}
