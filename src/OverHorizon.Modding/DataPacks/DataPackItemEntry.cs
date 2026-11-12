using System.Text.Json.Serialization;

namespace OverHorizon.Modding.DataPacks;

/// <summary>
/// Data-driven JSON item definition entry inside a datapack.
/// </summary>
/// <param name="Key">Full <c>namespace:path</c> item key.</param>
/// <param name="MaxStackSize">Maximum stack count (<c>1..64</c>).</param>
/// <param name="PlacedBlockKey">Optional <c>namespace:path</c> block placed on right-click.</param>
public sealed record DataPackItemEntry(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("maxStackSize")] int MaxStackSize = 64,
    [property: JsonPropertyName("placedBlockKey")] string? PlacedBlockKey = null);
