namespace OverHorizon.Core.Math;

/// <summary>
/// Allocation-free directional vector and axis helpers for <see cref="Direction"/>.
/// </summary>
public static class DirectionExtensions
{
    /// <summary>
    /// Gets all six cardinal directions in canonical order without heap allocation.
    /// </summary>
    public static ReadOnlySpan<Direction> All =>
    [
        Direction.Down,
        Direction.Up,
        Direction.North,
        Direction.South,
        Direction.West,
        Direction.East,
    ];

    /// <summary>
    /// Gets the four horizontal cardinal directions without heap allocation.
    /// </summary>
    public static ReadOnlySpan<Direction> Horizontal =>
    [
        Direction.North,
        Direction.South,
        Direction.West,
        Direction.East,
    ];

    /// <summary>
    /// Returns the opposite face direction.
    /// </summary>
    /// <param name="direction">Input direction.</param>
    /// <returns>Opposite <see cref="Direction"/>.</returns>
    public static Direction Opposite(this Direction direction) => (Direction)((byte)direction ^ 1);

    /// <summary>
    /// Returns the orthogonal <see cref="Axis"/> along which <paramref name="direction"/> points.
    /// </summary>
    /// <param name="direction">Input direction.</param>
    /// <returns>Corresponding <see cref="Axis"/>.</returns>
    public static Axis GetAxis(this Direction direction) => direction switch
    {
        Direction.West or Direction.East => Axis.X,
        Direction.Down or Direction.Up => Axis.Y,
        _ => Axis.Z,
    };

    /// <summary>
    /// Returns the unit normal vector for <paramref name="direction"/>.
    /// </summary>
    /// <param name="direction">Input direction.</param>
    /// <returns>Unit <see cref="Vec3i"/> normal.</returns>
    public static Vec3i Normal(this Direction direction) => direction switch
    {
        Direction.Down => new Vec3i(0, -1, 0),
        Direction.Up => new Vec3i(0, 1, 0),
        Direction.North => new Vec3i(0, 0, -1),
        Direction.South => new Vec3i(0, 0, 1),
        Direction.West => new Vec3i(-1, 0, 0),
        Direction.East => new Vec3i(1, 0, 0),
        _ => Vec3i.Zero,
    };
}
