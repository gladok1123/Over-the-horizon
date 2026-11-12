using System.Text.Json.Serialization;
using OverHorizon.Modding.Manifest;

namespace OverHorizon.Modding.DataPacks;

/// <summary>
/// Self-contained JSON datapack document declaring manifest metadata and custom blocks, items, and biomes.
/// </summary>
/// <param name="Manifest">Pack manifest and dependencies.</param>
/// <param name="Blocks">Optional custom blocks.</param>
/// <param name="Items">Optional custom items.</param>
/// <param name="Biomes">Optional custom biomes.</param>
public sealed record DataPackBundle(
    [property: JsonPropertyName("manifest")] ModManifest Manifest,
    [property: JsonPropertyName("blocks")] IReadOnlyList<DataPackBlockEntry>? Blocks = null,
    [property: JsonPropertyName("items")] IReadOnlyList<DataPackItemEntry>? Items = null,
    [property: JsonPropertyName("biomes")] IReadOnlyList<DataPackBiomeEntry>? Biomes = null);
