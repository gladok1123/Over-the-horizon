using OverHorizon.Core.Biomes;
using OverHorizon.Core.Registry;
using OverHorizon.Shared.Random;

namespace OverHorizon.World.Generation;

/// <summary>
/// Thread-safe climate and continentalness biome selector backed by <see cref="SimplexNoise2D"/>.
/// </summary>
public sealed class BiomeSampler
{
    private readonly SimplexNoise2D _continentalNoise;
    private readonly SimplexNoise2D _temperatureNoise;
    private readonly SimplexNoise2D _humidityNoise;
    private readonly IRegistry<BiomeDefinition> _biomes;

    private readonly ushort _oceanId;
    private readonly ushort _desertId;
    private readonly ushort _forestId;
    private readonly ushort _plainsId;

    /// <summary>
    /// Initializes a new instance of the <see cref="BiomeSampler"/> class.
    /// </summary>
    /// <param name="worldSeed">64-bit world seed.</param>
    /// <param name="biomes">Frozen biome registry.</param>
    public BiomeSampler(long worldSeed, IRegistry<BiomeDefinition> biomes)
    {
        ArgumentNullException.ThrowIfNull(biomes);

        _biomes = biomes;
        var seed = unchecked((ulong)worldSeed);
        _continentalNoise = new SimplexNoise2D(SplitMix64.Mix(seed ^ 0x1001UL));
        _temperatureNoise = new SimplexNoise2D(SplitMix64.Mix(seed ^ 0x2002UL));
        _humidityNoise = new SimplexNoise2D(SplitMix64.Mix(seed ^ 0x3003UL));

        _oceanId = ResolveOrDefault(biomes, BuiltInBiomes.Ocean);
        _desertId = ResolveOrDefault(biomes, BuiltInBiomes.Desert);
        _forestId = ResolveOrDefault(biomes, BuiltInBiomes.Forest);
        _plainsId = ResolveOrDefault(biomes, BuiltInBiomes.Plains);
    }

    /// <summary>
    /// Samples the continentalness value in <c>[-1.0, 1.0]</c> at world block coordinates (X, Z).
    /// </summary>
    /// <param name="worldX">World X block coordinate.</param>
    /// <param name="worldZ">World Z block coordinate.</param>
    /// <returns>Continentalness value.</returns>
    public double SampleContinentalness(int worldX, int worldZ) =>
        _continentalNoise.SampleFractal2D(worldX * 0.0025, worldZ * 0.0025, octaves: 3);

    /// <summary>
    /// Samples the dense biome raw ID for world block coordinates (X, Z).
    /// </summary>
    /// <param name="worldX">World X block coordinate.</param>
    /// <param name="worldZ">World Z block coordinate.</param>
    /// <returns>Dense biome registry ID.</returns>
    public ushort SampleBiomeId(int worldX, int worldZ)
    {
        var continental = SampleContinentalness(worldX, worldZ);
        if (continental < -0.22)
        {
            return _oceanId;
        }

        var temp = _temperatureNoise.SampleFractal2D(worldX * 0.002, worldZ * 0.002, octaves: 2);
        var humid = _humidityNoise.SampleFractal2D(worldX * 0.002, worldZ * 0.002, octaves: 2);

        if (temp > 0.28 && humid < -0.10)
        {
            return _desertId;
        }

        if (humid > 0.15)
        {
            return _forestId;
        }

        return _plainsId;
    }

    /// <summary>
    /// Resolves the <see cref="BiomeDefinition"/> for <paramref name="biomeId"/>.
    /// </summary>
    /// <param name="biomeId">Dense biome ID.</param>
    /// <returns>Registered <see cref="BiomeDefinition"/>.</returns>
    public BiomeDefinition GetBiome(ushort biomeId) => _biomes.GetById(biomeId);

    private static ushort ResolveOrDefault(IRegistry<BiomeDefinition> biomes, ResourceKey key)
    {
        var id = biomes.GetId(key);
        return id >= 0 ? (ushort)id : (ushort)0;
    }
}
