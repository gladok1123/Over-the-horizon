namespace OverHorizon.Core.Math;

/// <summary>
/// Immutable 3D integer vector.
/// </summary>
/// <param name="X">X component.</param>
/// <param name="Y">Y component.</param>
/// <param name="Z">Z component.</param>
public readonly record struct Vec3i(int X, int Y, int Z)
{
    /// <summary>
    /// Gets the zero vector (0, 0, 0).
    /// </summary>
    public static Vec3i Zero => default;

    /// <summary>
    /// Gets the squared Euclidean length of the vector as a 64-bit integer.
    /// </summary>
    public long LengthSquared => ((long)X * X) + ((long)Y * Y) + ((long)Z * Z);

    /// <summary>
    /// Adds two integer vectors component-wise.
    /// </summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>Component-wise sum.</returns>
    public static Vec3i Add(Vec3i left, Vec3i right) =>
        new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    /// <summary>
    /// Subtracts <paramref name="right"/> from <paramref name="left"/> component-wise.
    /// </summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>Component-wise difference.</returns>
    public static Vec3i Subtract(Vec3i left, Vec3i right) =>
        new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    /// <summary>
    /// Multiplies a vector by an integer scalar.
    /// </summary>
    /// <param name="vector">Input vector.</param>
    /// <param name="scalar">Scalar factor.</param>
    /// <returns>Scaled vector.</returns>
    public static Vec3i Multiply(Vec3i vector, int scalar) =>
        new(vector.X * scalar, vector.Y * scalar, vector.Z * scalar);

    /// <summary>
    /// Negates all components of <paramref name="value"/>.
    /// </summary>
    /// <param name="value">Input vector.</param>
    /// <returns>Negated vector.</returns>
    public static Vec3i Negate(Vec3i value) =>
        new(-value.X, -value.Y, -value.Z);

    /// <summary>Adds two vectors.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>Sum vector.</returns>
    public static Vec3i operator +(Vec3i left, Vec3i right) => Add(left, right);

    /// <summary>Subtracts two vectors.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>Difference vector.</returns>
    public static Vec3i operator -(Vec3i left, Vec3i right) => Subtract(left, right);

    /// <summary>Scales a vector by an integer factor.</summary>
    /// <param name="vector">Input vector.</param>
    /// <param name="scalar">Scalar factor.</param>
    /// <returns>Scaled vector.</returns>
    public static Vec3i operator *(Vec3i vector, int scalar) => Multiply(vector, scalar);

    /// <summary>Negates a vector.</summary>
    /// <param name="value">Input vector.</param>
    /// <returns>Negated vector.</returns>
    public static Vec3i operator -(Vec3i value) => Negate(value);

    /// <summary>
    /// Computes the L1 (Manhattan) distance to <paramref name="other"/>.
    /// </summary>
    /// <param name="other">Target vector.</param>
    /// <returns>Manhattan distance as 64-bit integer.</returns>
    public long ManhattanDistance(Vec3i other) =>
        System.Math.Abs((long)X - other.X) +
        System.Math.Abs((long)Y - other.Y) +
        System.Math.Abs((long)Z - other.Z);
}
