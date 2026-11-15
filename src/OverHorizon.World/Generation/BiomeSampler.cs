using OverHorizon.Core.Biomes;
using OverHorizon.Core.Registry;
using OverHorizon.Shared.Random;

namespace OverHorizon.World.Generation;

/// <summary>
/// Thread-safe multi-parameter climate, continentalness, and weirdness biome selector backed by <see cref="SimplexNoise2D"/>
/// resolving all 10 built-in Overworld biomes (<c>Ocean</c>, <c>Plains</c>, <c>Forest</c>, <c>Desert</c>,
/// <c>CherryGrove</c>, <c>SnowyTaiga</c>, <c>BirchForest</c>, <c>Badlands</c>, <c>IceSpikes</c>, and <c>Savanna</c>).
/// </summary>
public sealed class BiomeSampler
{
    private readonly SimplexNoise2D _continentalNoise;
    private readonly SimplexNoise2D _temperatureNoise;
    private readonly SimplexNoise2D _humidityNoise;
    private readonly SimplexNoise2D _weirdnessNoise;
    private readonly IRegistry<BiomeDefinition> _biomes;

    private readonly ushort _oceanId;
    private readonly ushort _plainsId;
    private readonly ushort _forestId;
    private readonly ushort _desertId;
    private readonly ushort _cherryGroveId;
    private readonly ushort _snowyTaigaId;
    private readonly ushort _birchForestId;
    private readonly ushort _badlandsId;
    private readonly ushort _iceSpikesId;
    private readonly ushort _savannaId;

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
        _weirdnessNoise = new SimplexNoise2D(SplitMix64.Mix(seed ^ 0x4004UL));

        _oceanId = ResolveOrDefault(biomes, BuiltInBiomes.Ocean);
        _plainsId = ResolveOrDefault(biomes, BuiltInBiomes.Plains);
        _forestId = ResolveOrDefault(biomes, BuiltInBiomes.Forest);
        _desertId = ResolveOrDefault(biomes, BuiltInBiomes.Desert);
        _cherryGroveId = ResolveOrFallback(biomes, BuiltInBiomes.CherryGrove, _forestId);
        _snowyTaigaId = ResolveOrFallback(biomes, BuiltInBiomes.SnowyTaiga, _plainsId);
        _birchForestId = ResolveOrFallback(biomes, BuiltInBiomes.BirchForest, _forestId);
        _badlandsId = ResolveOrFallback(biomes, BuiltInBiomes.Badlands, _desertId);
        _iceSpikesId = ResolveOrFallback(biomes, BuiltInBiomes.IceSpikes, _plainsId);
        _savannaId = ResolveOrFallback(biomes, BuiltInBiomes.Savanna, _plainsId);
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

        var temp = _temperatureNoise.SampleFractal2D(worldX * 0.0022, worldZ * 0.0022, octaves: 2);
        var humid = _humidityNoise.SampleFractal2D(worldX * 0.0022, worldZ * 0.0022, octaves: 2);
        var weird = _weirdnessNoise.SampleFractal2D(worldX * 0.0031, worldZ * 0.0031, octaves: 2);

        // Cold band: Snowy Taiga & Ice Spikes
        if (temp < -0.28)
        {
            return weird > 0.22 ? _iceSpikesId : _snowyTaigaId;
        }

        // Hot & arid band: Badlands, Desert, and Savanna
        if (temp > 0.24)
        {
            if (humid < -0.08)
            {
                return weird > 0.18 ? _badlandsId : _desertId;
            }

            if (humid < 0.18)
            {
                return _savannaId;
            }
        }

        // Highland & lush band: Cherry Grove, Birch Forest, Forest, and Plains
        if (continental > 0.24 && weird > 0.08)
        {
            return _cherryGroveId;
        }

        if (humid > 0.14)
        {
            return weird > 0.06 ? _birchForestId : _forestId;
        }

        return weird < -0.32 ? _cherryGroveId : _plainsId;
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

    private static ushort ResolveOrFallback(IRegistry<BiomeDefinition> biomes, ResourceKey key, ushort fallback)
    {
        var id = biomes.GetId(key);
        return id >= 0 ? (ushort)id : fallback;
    }
}
