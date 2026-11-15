using OverHorizon.Shared.Random;

namespace OverHorizon.World.Generation;

/// <summary>
/// Thread-safe, deterministic 2D simplex lattice gradient noise generator for smooth climate and continental fields.
/// </summary>
public sealed class SimplexNoise2D
{
    private const double SkewFactor2D = 0.3660254037844386; // 0.5 * (sqrt(3) - 1)
    private const double UnskewFactor2D = 0.21132486540518713; // (3 - sqrt(3)) / 6

    private readonly byte[] _perm = new byte[512];

    /// <summary>
    /// Initializes a new instance of the <see cref="SimplexNoise2D"/> class from a 64-bit seed.
    /// </summary>
    /// <param name="seed">Deterministic 64-bit noise seed.</param>
    public SimplexNoise2D(ulong seed)
    {
        var rng = new Xoroshiro128PlusPlus(seed);
        Span<byte> table = stackalloc byte[256];
        for (var i = 0; i < 256; i++)
        {
            table[i] = (byte)i;
        }

        for (var i = 255; i > 0; i--)
        {
            var j = rng.NextInt32(i + 1);
            (table[i], table[j]) = (table[j], table[i]);
        }

        for (var i = 0; i < 256; i++)
        {
            _perm[i] = table[i];
            _perm[i + 256] = table[i];
        }
    }

    /// <summary>
    /// Evaluates single-octave 2D simplex noise in approximately <c>[-1.0, 1.0]</c>.
    /// </summary>
    /// <param name="x">Sample X.</param>
    /// <param name="z">Sample Z.</param>
    /// <returns>Simplex noise value in <c>[-1.0, 1.0]</c>.</returns>
    public double Sample2D(double x, double z)
    {
        var skew = (x + z) * SkewFactor2D;
        var i = (int)Math.Floor(x + skew);
        var j = (int)Math.Floor(z + skew);

        var unskew = (i + j) * UnskewFactor2D;
        var x0 = x - (i - unskew);
        var z0 = z - (j - unskew);

        var i1 = x0 > z0 ? 1 : 0;
        var j1 = x0 > z0 ? 0 : 1;

        var x1 = (x0 - i1) + UnskewFactor2D;
        var z1 = (z0 - j1) + UnskewFactor2D;
        var x2 = (x0 - 1.0) + (2.0 * UnskewFactor2D);
        var z2 = (z0 - 1.0) + (2.0 * UnskewFactor2D);

        var ii = i & 255;
        var jj = j & 255;

        var g0 = _perm[ii + _perm[jj]] & 7;
        var g1 = _perm[ii + i1 + _perm[jj + j1]] & 7;
        var g2 = _perm[ii + 1 + _perm[jj + 1]] & 7;

        var n0 = CornerContribution(g0, x0, z0);
        var n1 = CornerContribution(g1, x1, z1);
        var n2 = CornerContribution(g2, x2, z2);

        return 70.0 * (n0 + n1 + n2);
    }

    /// <summary>
    /// Evaluates multi-octave 2D simplex fBm normalized to <c>[-1.0, 1.0]</c>.
    /// </summary>
    /// <param name="x">Sample X.</param>
    /// <param name="z">Sample Z.</param>
    /// <param name="octaves">Octave count (<c>&gt;= 1</c>).</param>
    /// <param name="lacunarity">Frequency multiplier.</param>
    /// <param name="persistence">Amplitude multiplier.</param>
    /// <returns>Normalized fBm value.</returns>
    public double SampleFractal2D(
        double x,
        double z,
        int octaves = 4,
        double lacunarity = 2.0,
        double persistence = 0.5)
    {
        if (octaves <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(octaves), "Octaves must be positive.");
        }

        var sum = 0.0;
        var amp = 1.0;
        var freq = 1.0;
        var norm = 0.0;

        for (var k = 0; k < octaves; k++)
        {
            sum += Sample2D(x * freq, z * freq) * amp;
            norm += amp;
            amp *= persistence;
            freq *= lacunarity;
        }

        return sum / norm;
    }

    private static double CornerContribution(int gradIndex, double dx, double dz)
    {
        var t = 0.5 - (dx * dx) - (dz * dz);
        if (t <= 0.0)
        {
            return 0.0;
        }

        var t2 = t * t;
        return t2 * t2 * Grad2D(gradIndex, dx, dz);
    }

    private static double Grad2D(int gradIndex, double x, double z) => gradIndex switch
    {
        0 => x + z,
        1 => -x + z,
        2 => x - z,
        3 => -x - z,
        4 => x * 1.41421356237,
        5 => -x * 1.41421356237,
        6 => z * 1.41421356237,
        _ => -z * 1.41421356237,
    };
}
