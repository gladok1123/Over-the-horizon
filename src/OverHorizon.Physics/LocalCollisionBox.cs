using OverHorizon.Core.Math;

namespace OverHorizon.Physics;

/// <summary>
/// Compact value-type sub-voxel collision box in block-local <c>[0..1]</c> (or <c>[0..1.5]</c> fence) space.
/// </summary>
/// <param name="MinX">Minimum local X offset.</param>
/// <param name="MinY">Minimum local Y offset.</param>
/// <param name="MinZ">Minimum local Z offset.</param>
/// <param name="MaxX">Maximum local X offset.</param>
/// <param name="MaxY">Maximum local Y offset.</param>
/// <param name="MaxZ">Maximum local Z offset.</param>
/// <param name="HasCollision">Whether this state blocks entity movement.</param>
public readonly record struct LocalCollisionBox(
    float MinX,
    float MinY,
    float MinZ,
    float MaxX,
    float MaxY,
    float MaxZ,
    bool HasCollision)
{
    /// <summary>Empty passable box.</summary>
    public static readonly LocalCollisionBox Empty = new(0f, 0f, 0f, 0f, 0f, 0f, HasCollision: false);

    /// <summary>Full <c>1x1x1</c> solid voxel box.</summary>
    public static readonly LocalCollisionBox FullCube = new(0f, 0f, 0f, 1f, 1f, 1f, HasCollision: true);

    /// <summary>Translates this local box into world-space 64-bit <see cref="BoundingBox"/> coordinates at <paramref name="pos"/>.</summary>
    /// <param name="pos">World-space voxel coordinate.</param>
    public BoundingBox ToWorldBox(BlockPos pos) =>
        new(
            pos.X + MinX,
            pos.Y + MinY,
            pos.Z + MinZ,
            pos.X + MaxX,
            pos.Y + MaxY,
            pos.Z + MaxZ);
}
