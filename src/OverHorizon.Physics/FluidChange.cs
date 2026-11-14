using OverHorizon.Core.Math;

namespace OverHorizon.Physics;

/// <summary>
/// Deterministic fluid cell mutation produced by <see cref="FluidSimulator"/>.
/// A <see cref="NewLevel"/> of <see cref="RemoveFluidSentinel"/> indicates drying back to air.
/// </summary>
/// <param name="Position">Target world block position.</param>
/// <param name="NewLevel">New fluid level (<c>0..8</c>), or <see cref="RemoveFluidSentinel"/> to clear.</param>
public readonly record struct FluidChange(BlockPos Position, byte NewLevel) : IComparable<FluidChange>
{
    /// <summary>Sentinel value indicating that fluid at <see cref="Position"/> dried up to air.</summary>
    public const byte RemoveFluidSentinel = byte.MaxValue;

    /// <summary>Compares two <see cref="FluidChange"/> entries by <see cref="Position"/>.</summary>
    /// <param name="left">Left change.</param>
    /// <param name="right">Right change.</param>
    /// <returns>Signed comparison result.</returns>
    public static int Compare(FluidChange left, FluidChange right) => left.CompareTo(right);

    /// <summary>Determines whether <paramref name="left"/> precedes <paramref name="right"/>.</summary>
    /// <param name="left">Left change.</param>
    /// <param name="right">Right change.</param>
    /// <returns><see langword="true"/> if less than.</returns>
    public static bool operator <(FluidChange left, FluidChange right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether <paramref name="left"/> precedes or equals <paramref name="right"/>.</summary>
    /// <param name="left">Left change.</param>
    /// <param name="right">Right change.</param>
    /// <returns><see langword="true"/> if less than or equal to.</returns>
    public static bool operator <=(FluidChange left, FluidChange right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether <paramref name="left"/> follows <paramref name="right"/>.</summary>
    /// <param name="left">Left change.</param>
    /// <param name="right">Right change.</param>
    /// <returns><see langword="true"/> if greater than.</returns>
    public static bool operator >(FluidChange left, FluidChange right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether <paramref name="left"/> follows or equals <paramref name="right"/>.</summary>
    /// <param name="left">Left change.</param>
    /// <param name="right">Right change.</param>
    /// <returns><see langword="true"/> if greater than or equal to.</returns>
    public static bool operator >=(FluidChange left, FluidChange right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public int CompareTo(FluidChange other) => Position.CompareTo(other.Position);
}
