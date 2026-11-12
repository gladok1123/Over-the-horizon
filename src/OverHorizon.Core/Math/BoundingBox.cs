namespace OverHorizon.Core.Math;

/// <summary>
/// Immutable 3D axis-aligned bounding box in double-precision world coordinates.
/// </summary>
/// <param name="MinX">Minimum X bound.</param>
/// <param name="MinY">Minimum Y bound.</param>
/// <param name="MinZ">Minimum Z bound.</param>
/// <param name="MaxX">Maximum X bound.</param>
/// <param name="MaxY">Maximum Y bound.</param>
/// <param name="MaxZ">Maximum Z bound.</param>
public readonly record struct BoundingBox(
    double MinX,
    double MinY,
    double MinZ,
    double MaxX,
    double MaxY,
    double MaxZ)
{
    /// <summary>Gets the width along the X axis.</summary>
    public double SizeX => MaxX - MinX;

    /// <summary>Gets the height along the Y axis.</summary>
    public double SizeY => MaxY - MinY;

    /// <summary>Gets the depth along the Z axis.</summary>
    public double SizeZ => MaxZ - MinZ;

    /// <summary>Gets the geometric center of the bounding box.</summary>
    public Vec3d Center => new((MinX + MaxX) * 0.5, (MinY + MaxY) * 0.5, (MinZ + MaxZ) * 0.5);

    /// <summary>
    /// Creates a normalized <see cref="BoundingBox"/> ensuring min/max ordering on all axes.
    /// </summary>
    /// <param name="cornerA">First corner.</param>
    /// <param name="cornerB">Second corner.</param>
    /// <returns>Normalized <see cref="BoundingBox"/>.</returns>
    public static BoundingBox FromCorners(Vec3d cornerA, Vec3d cornerB) => new(
        System.Math.Min(cornerA.X, cornerB.X),
        System.Math.Min(cornerA.Y, cornerB.Y),
        System.Math.Min(cornerA.Z, cornerB.Z),
        System.Math.Max(cornerA.X, cornerB.X),
        System.Math.Max(cornerA.Y, cornerB.Y),
        System.Math.Max(cornerA.Z, cornerB.Z));

    /// <summary>
    /// Creates a unit <see cref="BoundingBox"/> occupying a full 1x1x1 voxel block.
    /// </summary>
    /// <param name="pos">Block position.</param>
    /// <returns>Unit block <see cref="BoundingBox"/>.</returns>
    public static BoundingBox FromBlock(BlockPos pos) =>
        new(pos.X, pos.Y, pos.Z, pos.X + 1.0, pos.Y + 1.0, pos.Z + 1.0);

    /// <summary>
    /// Creates an entity <see cref="BoundingBox"/> centered horizontally at <paramref name="feetPosition"/>.
    /// </summary>
    /// <param name="feetPosition">Bottom-center world position.</param>
    /// <param name="width">Horizontal width (X and Z).</param>
    /// <param name="height">Vertical height (Y).</param>
    /// <returns>Entity <see cref="BoundingBox"/>.</returns>
    public static BoundingBox FromEntityFeet(Vec3d feetPosition, double width, double height)
    {
        var halfW = width * 0.5;
        return new BoundingBox(
            feetPosition.X - halfW,
            feetPosition.Y,
            feetPosition.Z - halfW,
            feetPosition.X + halfW,
            feetPosition.Y + height,
            feetPosition.Z + halfW);
    }

    /// <summary>
    /// Determines whether this box strictly overlaps <paramref name="other"/> on all three axes.
    /// </summary>
    /// <param name="other">Other bounding box.</param>
    /// <returns><see langword="true"/> if the interiors intersect.</returns>
    public bool Intersects(BoundingBox other) =>
        MinX < other.MaxX && MaxX > other.MinX &&
        MinY < other.MaxY && MaxY > other.MinY &&
        MinZ < other.MaxZ && MaxZ > other.MinZ;

    /// <summary>
    /// Determines whether <paramref name="point"/> lies within <c>[Min, Max)</c> on all axes.
    /// </summary>
    /// <param name="point">World-space point.</param>
    /// <returns><see langword="true"/> if contained.</returns>
    public bool Contains(Vec3d point) =>
        point.X >= MinX && point.X < MaxX &&
        point.Y >= MinY && point.Y < MaxY &&
        point.Z >= MinZ && point.Z < MaxZ;

    /// <summary>
    /// Translates the bounding box by <paramref name="delta"/>.
    /// </summary>
    /// <param name="delta">Translation vector.</param>
    /// <returns>Translated <see cref="BoundingBox"/>.</returns>
    public BoundingBox Move(Vec3d delta) => new(
        MinX + delta.X,
        MinY + delta.Y,
        MinZ + delta.Z,
        MaxX + delta.X,
        MaxY + delta.Y,
        MaxZ + delta.Z);

    /// <summary>
    /// Symmetrically expands (or shrinks if negative) the box on each axis.
    /// </summary>
    /// <param name="dx">Expansion along X.</param>
    /// <param name="dy">Expansion along Y.</param>
    /// <param name="dz">Expansion along Z.</param>
    /// <returns>Inflated <see cref="BoundingBox"/>.</returns>
    public BoundingBox Inflate(double dx, double dy, double dz) => new(
        MinX - dx,
        MinY - dy,
        MinZ - dz,
        MaxX + dx,
        MaxY + dy,
        MaxZ + dz);

    /// <summary>
    /// Expands the box only along the signed direction of <paramref name="delta"/> (swept broadphase box).
    /// </summary>
    /// <param name="delta">Motion vector.</param>
    /// <returns>Swept <see cref="BoundingBox"/>.</returns>
    public BoundingBox ExpandTowards(Vec3d delta) => new(
        delta.X < 0.0 ? MinX + delta.X : MinX,
        delta.Y < 0.0 ? MinY + delta.Y : MinY,
        delta.Z < 0.0 ? MinZ + delta.Z : MinZ,
        delta.X > 0.0 ? MaxX + delta.X : MaxX,
        delta.Y > 0.0 ? MaxY + delta.Y : MaxY,
        delta.Z > 0.0 ? MaxZ + delta.Z : MaxZ);
}
