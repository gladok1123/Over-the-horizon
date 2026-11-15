namespace OverHorizon.Shared.Random;

/// <summary>
/// Fast, deterministic 64-bit SplitMix64 pseudo-random generator and seed mixer.
/// Used to initialize <see cref="Xoroshiro128PlusPlus"/> states and derive positional seeds.
/// </summary>
public struct SplitMix64
{
    private ulong _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="SplitMix64"/> struct.
    /// </summary>
    /// <param name="seed">Initial 64-bit seed value.</param>
    public SplitMix64(ulong seed)
    {
        _state = seed;
    }

    /// <summary>
    /// Avalanches a 64-bit integer using the SplitMix64 finalizer (stateless).
    /// </summary>
    /// <param name="value">Input 64-bit value.</param>
    /// <returns>Well-distributed 64-bit hash/seed.</returns>
    public static ulong Mix(ulong value)
    {
        var z = unchecked(value + 0x9E3779B97F4A7C15UL);
        z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
        z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
        return z ^ (z >> 31);
    }

    /// <summary>
    /// Advances the generator state and returns the next 64-bit unsigned integer.
    /// </summary>
    /// <returns>Pseudorandom <see cref="ulong"/>.</returns>
    public ulong NextUInt64()
    {
        var current = _state;
        _state = unchecked(current + 0x9E3779B97F4A7C15UL);
        return Mix(current);
    }
}
