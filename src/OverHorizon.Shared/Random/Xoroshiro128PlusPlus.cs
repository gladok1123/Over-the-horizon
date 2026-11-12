using System.Numerics;

namespace OverHorizon.Shared.Random;

/// <summary>
/// Deterministic, per-thread/per-task Xoroshiro128++ pseudo-random number generator.
/// Never shared across threads concurrently; seeded deterministically per chunk or entity.
/// </summary>
public struct Xoroshiro128PlusPlus
{
    private ulong _s0;
    private ulong _s1;

    /// <summary>
    /// Initializes a new instance of the <see cref="Xoroshiro128PlusPlus"/> struct from a 64-bit seed.
    /// </summary>
    /// <param name="seed">64-bit seed expanded through <see cref="SplitMix64"/>.</param>
    public Xoroshiro128PlusPlus(ulong seed)
    {
        var mixer = new SplitMix64(seed);
        _s0 = mixer.NextUInt64();
        _s1 = mixer.NextUInt64();
        if (_s0 == 0UL && _s1 == 0UL)
        {
            _s0 = 0x9E3779B97F4A7C15UL;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Xoroshiro128PlusPlus"/> struct from explicit 128-bit state.
    /// </summary>
    /// <param name="state0">Low 64 bits of state.</param>
    /// <param name="state1">High 64 bits of state.</param>
    public Xoroshiro128PlusPlus(ulong state0, ulong state1)
    {
        _s0 = state0;
        _s1 = state1;
        if (_s0 == 0UL && _s1 == 0UL)
        {
            _s0 = 0x9E3779B97F4A7C15UL;
        }
    }

    /// <summary>
    /// Creates an isolated, deterministic generator for a 3D block or chunk coordinate.
    /// </summary>
    /// <param name="worldSeed">Global 64-bit world seed.</param>
    /// <param name="x">X coordinate.</param>
    /// <param name="y">Y coordinate.</param>
    /// <param name="z">Z coordinate.</param>
    /// <returns>Initialized <see cref="Xoroshiro128PlusPlus"/> instance.</returns>
    public static Xoroshiro128PlusPlus FromPosition(long worldSeed, int x, int y, int z)
    {
        var coordHash = unchecked(
            ((ulong)(uint)x * 0x9E3779B185EBCA87UL) ^
            ((ulong)(uint)y * 0xC2B2AE3D27D4EB4FUL) ^
            ((ulong)(uint)z * 0x165667B19E3779F9UL));
        var s0 = SplitMix64.Mix(unchecked((ulong)worldSeed ^ coordHash));
        var s1 = SplitMix64.Mix(unchecked(s0 + 0x9E3779B97F4A7C15UL));
        return new Xoroshiro128PlusPlus(s0, s1);
    }

    /// <summary>
    /// Creates an isolated, deterministic generator for a specific entity on a specific simulation tick.
    /// </summary>
    /// <param name="worldSeed">Global 64-bit world seed.</param>
    /// <param name="tickNumber">Current simulation tick number.</param>
    /// <param name="entityId">Stable 64-bit entity identifier.</param>
    /// <returns>Initialized <see cref="Xoroshiro128PlusPlus"/> instance.</returns>
    public static Xoroshiro128PlusPlus FromEntityTick(long worldSeed, long tickNumber, long entityId)
    {
        var mixed = SplitMix64.Mix(unchecked((ulong)worldSeed ^ ((ulong)tickNumber * 0xBF58476D1CE4E5B9UL)));
        var s0 = SplitMix64.Mix(unchecked(mixed ^ (ulong)entityId));
        var s1 = SplitMix64.Mix(unchecked(s0 + (ulong)entityId + 1UL));
        return new Xoroshiro128PlusPlus(s0, s1);
    }

    /// <summary>
    /// Generates the next 64-bit unsigned integer.
    /// </summary>
    /// <returns>Pseudorandom <see cref="ulong"/>.</returns>
    public ulong NextUInt64()
    {
        var s0 = _s0;
        var s1 = _s1;
        var result = unchecked(BitOperations.RotateLeft(unchecked(s0 + s1), 17) + s0);

        s1 ^= s0;
        _s0 = BitOperations.RotateLeft(s0, 49) ^ s1 ^ (s1 << 21);
        _s1 = BitOperations.RotateLeft(s1, 28);

        return result;
    }

    /// <summary>
    /// Generates the next 32-bit unsigned integer from the upper bits.
    /// </summary>
    /// <returns>Pseudorandom <see cref="uint"/>.</returns>
    public uint NextUInt32() => (uint)(NextUInt64() >> 32);

    /// <summary>
    /// Generates an unbiased integer in <c>[0, maxExclusive)</c>.
    /// </summary>
    /// <param name="maxExclusive">Strict upper bound (must be positive).</param>
    /// <returns>Uniform integer in <c>[0, maxExclusive)</c>.</returns>
    public int NextInt32(int maxExclusive)
    {
        if (maxExclusive <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Upper bound must be positive.");
        }

        var bound = (uint)maxExclusive;
        var threshold = unchecked(0U - bound) % bound;

        while (true)
        {
            var r = NextUInt32();
            var product = (ulong)r * bound;
            if ((uint)product >= threshold)
            {
                return (int)(product >> 32);
            }
        }
    }

    /// <summary>
    /// Generates a uniform double-precision float in <c>[0.0, 1.0)</c> with 53 bits of precision.
    /// </summary>
    /// <returns>Uniform <see cref="double"/> in <c>[0.0, 1.0)</c>.</returns>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    /// <summary>
    /// Generates a uniform single-precision float in <c>[0.0f, 1.0f)</c> with 24 bits of precision.
    /// </summary>
    /// <returns>Uniform <see cref="float"/> in <c>[0.0f, 1.0f)</c>.</returns>
    public float NextSingle() => (NextUInt32() >> 8) * (1.0f / (1U << 24));

    /// <summary>
    /// Generates a uniform boolean value.
    /// </summary>
    /// <returns>Pseudorandom <see cref="bool"/>.</returns>
    public bool NextBoolean() => (NextUInt64() & 1UL) != 0UL;
}
