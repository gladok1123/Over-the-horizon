using OverHorizon.Shared.Random;

namespace OverHorizon.World.Generation;

/// <summary>
/// Thread-safe, deterministic 2D and 3D gradient Perlin noise generator with fractal Brownian motion (fBm).
/// </summary>
public sealed class PerlinNoise
{
    private readonly byte[] _perm = new byte[512];

    /// <summary>
    /// Initializes a new instance of the <see cref="PerlinNoise"/> class from a 64-bit seed.
    /// </summary>
    /// <param name="seed">Deterministic 64-bit noise seed.</param>
    public PerlinNoise(ulong seed)
    {
        var rng = new Xoroshiro128PlusPlus(seed);
        Span<byte> table = stackalloc byte[256];
        for (var i = 0; i < 256; i++)
        {
            table[i] = (byte)i;
        }

        for (var i = 255; i > 0; i--)
        {
            var swapIndex = rng.NextInt32(i + 1);
            (table[i], table[swapIndex]) = (table[swapIndex], table[i]);
        }

        for (var i = 0; i < 256; i++)
        {
            _perm[i] = table[i];
            _perm[i + 256] = table[i];
        }
    }

    /// <summary>
    /// Evaluates single-octave 3D gradient Perlin noise in approximately <c>[-1.0, 1.0]</c>.
    /// </summary>
    /// <param name="x">Sample X.</param>
    /// <param name="y">Sample Y.</param>
    /// <param name="z">Sample Z.</param>
    /// <returns>Noise value in <c>[-1.0, 1.0]</c>.</returns>
    public double Sample3D(double x, double y, double z)
    {
        var floorX = Math.Floor(x);
        var floorY = Math.Floor(y);
        var floorZ = Math.Floor(z);

        var xi = ((int)floorX) & 255;
        var yi = ((int)floorY) & 255;
        var zi = ((int)floorZ) & 255;

        var xf = x - floorX;
        var yf = y - floorY;
        var zf = z - floorZ;

        var u = Fade(xf);
        var v = Fade(yf);
        var w = Fade(zf);

        var p = _perm;
        var a = p[xi] + yi;
        var aa = p[a] + zi;
        var ab = p[a + 1] + zi;
        var b = p[xi + 1] + yi;
        var ba = p[b] + zi;
        var bb = p[b + 1] + zi;

        var x1 = Lerp(Grad(p[aa], xf, yf, zf), Grad(p[ba], xf - 1.0, yf, zf), u);
        var x2 = Lerp(Grad(p[ab], xf, yf - 1.0, zf), Grad(p[bb], xf - 1.0, yf - 1.0, zf), u);
        var y1 = Lerp(x1, x2, v);

        var x3 = Lerp(Grad(p[aa + 1], xf, yf, zf - 1.0), Grad(p[ba + 1], xf - 1.0, yf, zf - 1.0), u);
        var x4 = Lerp(Grad(p[ab + 1], xf, yf - 1.0, zf - 1.0), Grad(p[bb + 1], xf - 1.0, yf - 1.0, zf - 1.0), u);
        var y2 = Lerp(x3, x4, v);

        return Lerp(y1, y2, w);
    }

    /// <summary>
    /// Evaluates multi-octave 3D fractal Brownian motion (fBm) normalized to <c>[-1.0, 1.0]</c>.
    /// </summary>
    /// <param name="x">Sample X.</param>
    /// <param name="y">Sample Y.</param>
    /// <param name="z">Sample Z.</param>
    /// <param name="octaves">Number of octaves (<c>&gt;= 1</c>).</param>
    /// <param name="lacunarity">Frequency multiplier per octave.</param>
    /// <param name="persistence">Amplitude multiplier per octave.</param>
    /// <returns>Normalized fBm value.</returns>
    public double SampleFractal3D(
        double x,
        double y,
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
        var amplitude = 1.0;
        var frequency = 1.0;
        var norm = 0.0;

        for (var i = 0; i < octaves; i++)
        {
            sum += Sample3D(x * frequency, y * frequency, z * frequency) * amplitude;
            norm += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return sum / norm;
    }

    private static double Fade(double t) => t * t * t * ((t * ((t * 6.0) - 15.0)) + 10.0);

    private static double Lerp(double a, double b, double t) => a + (t * (b - a));

    private static double Grad(int hash, double x, double y, double z) => (hash & 15) switch
    {
        0 => x + y,
        1 => -x + y,
        2 => x - y,
        3 => -x - y,
        4 => x + z,
        5 => -x + z,
        6 => x - z,
        7 => -x - z,
        8 => y + z,
        9 => -y + z,
        10 => y - z,
        11 => -y - z,
        12 => y + x,
        13 => -y + z,
        14 => y - x,
        _ => -y - z,
    };
}
