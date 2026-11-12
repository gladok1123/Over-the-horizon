namespace OverHorizon.Core.Math;

/// <summary>
/// Immutable double-precision 3D vector used for world-space entity positions and velocities.
/// </summary>
/// <param name="X">X coordinate.</param>
/// <param name="Y">Y coordinate.</param>
/// <param name="Z">Z coordinate.</param>
public readonly record struct Vec3d(double X, double Y, double Z)
{
    /// <summary>
    /// Gets the zero vector (0, 0, 0).
    /// </summary>
    public static Vec3d Zero => default;

    /// <summary>
    /// Gets the squared Euclidean length of the vector.
    /// </summary>
    public double LengthSquared => (X * X) + (Y * Y) + (Z * Z);

    /// <summary>
    /// Gets the Euclidean length of the vector.
    /// </summary>
    public double Length => System.Math.Sqrt(LengthSquared);

    /// <summary>
    /// Adds two vectors component-wise.
    /// </summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>Sum vector.</returns>
    public static Vec3d Add(Vec3d left, Vec3d right) =>
        new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    /// <summary>
    /// Subtracts <paramref name="right"/> from <paramref name="left"/> component-wise.
    /// </summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>Difference vector.</returns>
    public static Vec3d Subtract(Vec3d left, Vec3d right) =>
        new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    /// <summary>
    /// Multiplies a vector by a scalar factor.
    /// </summary>
    /// <param name="vector">Input vector.</param>
    /// <param name="scalar">Scalar multiplier.</param>
    /// <returns>Scaled vector.</returns>
    public static Vec3d Multiply(Vec3d vector, double scalar) =>
        new(vector.X * scalar, vector.Y * scalar, vector.Z * scalar);

    /// <summary>
    /// Negates all components of <paramref name="value"/>.
    /// </summary>
    /// <param name="value">Input vector.</param>
    /// <returns>Negated vector.</returns>
    public static Vec3d Negate(Vec3d value) =>
        new(-value.X, -value.Y, -value.Z);

    /// <summary>Adds two vectors.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>Sum vector.</returns>
    public static Vec3d operator +(Vec3d left, Vec3d right) => Add(left, right);

    /// <summary>Subtracts two vectors.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>Difference vector.</returns>
    public static Vec3d operator -(Vec3d left, Vec3d right) => Subtract(left, right);

    /// <summary>Scales a vector by a double-precision factor.</summary>
    /// <param name="vector">Input vector.</param>
    /// <param name="scalar">Scalar multiplier.</param>
    /// <returns>Scaled vector.</returns>
    public static Vec3d operator *(Vec3d vector, double scalar) => Multiply(vector, scalar);

    /// <summary>Negates a vector.</summary>
    /// <param name="value">Input vector.</param>
    /// <returns>Negated vector.</returns>
    public static Vec3d operator -(Vec3d value) => Negate(value);

    /// <summary>
    /// Computes the dot product with <paramref name="other"/>.
    /// </summary>
    /// <param name="other">Other vector.</param>
    /// <returns>Scalar dot product.</returns>
    public double Dot(Vec3d other) => (X * other.X) + (Y * other.Y) + (Z * other.Z);

    /// <summary>
    /// Computes the 3D cross product <c>this x other</c>.
    /// </summary>
    /// <param name="other">Other vector.</param>
    /// <returns>Perpendicular cross-product vector.</returns>
    public Vec3d Cross(Vec3d other) => new(
        (Y * other.Z) - (Z * other.Y),
        (Z * other.X) - (X * other.Z),
        (X * other.Y) - (Y * other.X));

    /// <summary>
    /// Returns a unit-length vector in the same direction, or <see cref="Zero"/> if length is near zero.
    /// </summary>
    /// <returns>Normalized <see cref="Vec3d"/>.</returns>
    public Vec3d Normalize()
    {
        var lenSq = LengthSquared;
        if (lenSq < 1e-16)
        {
            return Zero;
        }

        var invLen = 1.0 / System.Math.Sqrt(lenSq);
        return new Vec3d(X * invLen, Y * invLen, Z * invLen);
    }

    /// <summary>
    /// Computes the squared Euclidean distance to <paramref name="other"/>.
    /// </summary>
    /// <param name="other">Target point.</param>
    /// <returns>Squared distance.</returns>
    public double DistanceSquaredTo(Vec3d other) => (this - other).LengthSquared;

    /// <summary>
    /// Converts world-space coordinates to the containing <see cref="BlockPos"/> via floor rounding.
    /// </summary>
    /// <returns>Containing <see cref="BlockPos"/>.</returns>
    public BlockPos ToBlockPos() => new(
        (int)System.Math.Floor(X),
        (int)System.Math.Floor(Y),
        (int)System.Math.Floor(Z));
}
