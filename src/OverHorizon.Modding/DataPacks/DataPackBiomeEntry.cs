using System.Text.Json.Serialization;

namespace OverHorizon.Modding.DataPacks;

/// <summary>
/// Data-driven JSON biome definition entry inside a datapack.
/// </summary>
/// <param name="Key">Full <c>namespace:path</c> biome key.</param>
/// <param name="Temperature">Climate temperature.</param>
/// <param name="Downfall">Climate rainfall/humidity.</param>
/// <param name="SurfaceBlockKey">Top surface block key.</param>
/// <param name="UnderSurfaceBlockKey">Subsurface filler block key.</param>
public sealed record DataPackBiomeEntry(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("temperature")] float Temperature,
    [property: JsonPropertyName("downfall")] float Downfall,
    [property: JsonPropertyName("surfaceBlock")] string SurfaceBlockKey,
    [property: JsonPropertyName("underSurfaceBlock")] string UnderSurfaceBlockKey);
