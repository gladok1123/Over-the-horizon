using System.Text.Json.Serialization;

namespace OverHorizon.Modding.Manifest;

/// <summary>
/// Metadata descriptor for a code plugin or data-driven content pack.
/// </summary>
/// <param name="Id">Unique snake_case mod namespace/identifier (e.g. <c>"crystal_caverns"</c>).</param>
/// <param name="Name">Human-readable mod title.</param>
/// <param name="Version">Semantic version string (e.g. <c>"1.0.0"</c>).</param>
/// <param name="Description">Optional summary description.</param>
/// <param name="Dependencies">Mod IDs that must initialize before this mod.</param>
public sealed record ModManifest(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("description")] string Description = "",
    [property: JsonPropertyName("dependencies")] IReadOnlyList<string>? Dependencies = null);
