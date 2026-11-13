using OverHorizon.Core.Math;

namespace OverHorizon.Physics;

/// <summary>
/// Extends <see cref="IVoxelCollisionReader"/> with sub-voxel selection bounding boxes so non-solid blocks
/// (such as Torches, Ladders, Rails, Cobwebs, Redstone components, Levers, Buttons, and Fire) can be raycast-targeted and mined.
/// </summary>
public interface IVoxelSelectionReader : IVoxelCollisionReader
{
    /// <summary>
    /// Gets the block-local (<c>[0..1]^3</c>) selection bounding box for the block at <paramref name="position"/>, if targetable.
    /// </summary>
    /// <param name="position">World-space voxel coordinate.</param>
    /// <param name="localBounds">Block-local selection box on <c>[0, 1]^3</c> when selectable.</param>
    /// <returns><see langword="true"/> if the block at <paramref name="position"/> can be targeted by the player's crosshair.</returns>
    bool TryGetSelectionBox(BlockPos position, out BoundingBox localBounds);
}
