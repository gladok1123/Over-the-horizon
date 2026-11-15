using OverHorizon.Core.Math;

namespace OverHorizon.Physics;

/// <summary>
/// Result of a 3D DDA voxel raycast against solid world blocks.
/// </summary>
/// <param name="BlockPosition">Solid block intersected by the ray.</param>
/// <param name="AdjacentPosition">Empty/replaceable neighbor block immediately preceding <paramref name="BlockPosition"/> along the ray.</param>
/// <param name="HitFace">Outward normal face of <paramref name="BlockPosition"/> struck by the ray.</param>
/// <param name="Distance">Parametric distance along the unit ray direction in blocks.</param>
public readonly record struct VoxelRaycastHit(
    BlockPos BlockPosition,
    BlockPos AdjacentPosition,
    Direction HitFace,
    double Distance);
