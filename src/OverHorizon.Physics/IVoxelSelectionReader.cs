using OverHorizon.Core.Math;

namespace OverHorizon.Physics;

/// <summary>
/// Extends <see cref="IVoxelCollisionReader"/> with sub-voxel selection bounding boxes so non-solid blocks
/// (such as Torches, Ladders, Rails, Cobwebs, Redstone components, Levers, Buttons, and Fire) can be raycast-targeted and mined.
/// </summary>
public interface IVoxelSelectionReader : IVoxelCollisionReader
{
    /// <summary>
    /// Gets the world-space selection bounding box for the block at <paramref name="pos"/>, if targetable.
    /// </summary>
    /// <param name="pos">World-space voxel coordinate.</param>
    /// <param name="worldBox">World-space selection box when selectable.</param>
    /// <returns><see langword="true"/> if the block at <paramref name="pos"/> can be targeted by the player's crosshair.</returns>
    bool TryGetSelectionBox(BlockPos pos, out BoundingBox worldBox);
}
