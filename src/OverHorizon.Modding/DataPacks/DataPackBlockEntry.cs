using System.Text.Json.Serialization;

namespace OverHorizon.Modding.DataPacks;

/// <summary>
/// Data-driven JSON block definition entry inside a datapack.
/// </summary>
/// <param name="Key">Full <c>namespace:path</c> block key.</param>
/// <param name="IsSolid">Whether collidable.</param>
/// <param name="IsOpaque">Whether opaque to sky light.</param>
/// <param name="LightEmission">Emitted block light in <c>[0, 15]</c>.</param>
/// <param name="Hardness">Mining hardness.</param>
/// <param name="RegisterItem">Whether to auto-register a matching block-item.</param>
public sealed record DataPackBlockEntry(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("isSolid")] bool IsSolid = true,
    [property: JsonPropertyName("isOpaque")] bool IsOpaque = true,
    [property: JsonPropertyName("lightEmission")] byte LightEmission = 0,
    [property: JsonPropertyName("hardness")] float Hardness = 1.5f,
    [property: JsonPropertyName("registerItem")] bool RegisterItem = true);
