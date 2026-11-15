using OverHorizon.Core.Biomes;
using OverHorizon.Core.Registry;
using OverHorizon.Shared.Random;

namespace OverHorizon.World.Generation;

/// <summary>
/// Thread-safe deterministic Nether biome selector resolving all 5 built-in Nether biomes
/// (<c>NetherWastes</c>, <c>CrimsonForest</c>, <c>WarpedForest</c>, <c>SoulSandValley</c>, and <c>BasaltDeltas</c>)
/// from temperature and humidity noise fields.
/// </summary>
public sealed class NetherBiomeSampler
{
    private readonly SimplexNoise2D _temperatureNoise;
    private readonly SimplexNoise2D _humidityNoise;
    private readonly IRegistry<BiomeDefinition> _biomes;

    private readonly ushort _wastesId;
    private readonly ushort _crimsonId;
    private readonly ushort _warpedId;
    private readonly ushort _soulSandValleyId;
    private readonly ushort _basaltDeltasId;

    /// <summary>
    /// Initializes a new instance of the <see cref="NetherBiomeSampler"/> class.
    /// </summary>
    /// <param name="worldSeed">64-bit world seed.</param>
    /// <param name="biomes">Frozen biome registry.</param>
    public NetherBiomeSampler(long worldSeed, IRegistry<BiomeDefinition> biomes)
    {
        ArgumentNullException.ThrowIfNull(biomes);

        _biomes = biomes;
        var seed = unchecked((ulong)worldSeed);
        _temperatureNoise = new SimplexNoise2D(SplitMix64.Mix(seed ^ 0x5EED_0001UL));
        _humidityNoise = new SimplexNoise2D(SplitMix64.Mix(seed ^ 0x5EED_0002UL));

        _wastesId = biomes.GetId(BuiltInBiomes.NetherWastes) >= 0
            ? (ushort)biomes.GetId(BuiltInBiomes.NetherWastes)
            : (ushort)0;
        _crimsonId = ResolveOrFallback(biomes, BuiltInBiomes.CrimsonForest, _wastesId);
        _warpedId = ResolveOrFallback(biomes, BuiltInBiomes.WarpedForest, _wastesId);
        _soulSandValleyId = ResolveOrFallback(biomes, BuiltInBiomes.SoulSandValley, _wastesId);
        _basaltDeltasId = ResolveOrFallback(biomes, BuiltInBiomes.BasaltDeltas, _wastesId);
    }

    /// <summary>
    /// Samples the Nether temperature value in <c>[-1.0, 1.0]</c> at world block coordinates (X, Z).
    /// </summary>
    /// <param name="worldX">World X block coordinate.</param>
    /// <param name="worldZ">World Z block coordinate.</param>
    /// <returns>Temperature value.</returns>
    public double SampleTemperature(int worldX, int worldZ) =>
        _temperatureNoise.SampleFractal2D(worldX * 0.0075, worldZ * 0.0075, octaves: 2);

    /// <summary>
    /// Samples the dense biome raw ID for world block coordinates (X, Z).
    /// </summary>
    /// <param name="worldX">World X block coordinate.</param>
    /// <param name="worldZ">World Z block coordinate.</param>
    /// <returns>Dense biome registry ID.</returns>
    public ushort SampleBiomeId(int worldX, int worldZ)
    {
        var temp = SampleTemperature(worldX, worldZ);
        var humid = _humidityNoise.SampleFractal2D(worldX * 0.0075, worldZ * 0.0075, octaves: 2);

        // Frozen/ash band: Soul Sand Valley takes over the coldest Nether corners.
        if (temp < -0.34)
        {
            return _soulSandValleyId;
        }

        // Volcanic band: Basalt Deltas dominate the hottest, driest corners.
        if (temp > 0.34 && humid < 0.05)
        {
            return _basaltDeltasId;
        }

        // Lush fungal band: Crimson (warm) and Warped (cool) forests split on humidity.
        if (humid > 0.22)
        {
            return temp > 0.0 ? _crimsonId : _warpedId;
        }

        return _wastesId;
    }

    /// <summary>
    /// Resolves the <see cref="BiomeDefinition"/> for <paramref name="biomeId"/>.
    /// </summary>
    /// <param name="biomeId">Dense biome ID.</param>
    /// <returns>Registered <see cref="BiomeDefinition"/>.</returns>
    public BiomeDefinition GetBiome(ushort biomeId) => _biomes.GetById(biomeId);

    private static ushort ResolveOrFallback(IRegistry<BiomeDefinition> biomes, ResourceKey key, ushort fallback)
    {
        var id = biomes.GetId(key);
        return id >= 0 ? (ushort)id : fallback;
    }
}
