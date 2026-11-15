using OverHorizon.Core.Blocks;
using OverHorizon.Core.Registry;

namespace OverHorizon.Core.Biomes;

/// <summary>
/// Immutable descriptor of a world biome's climate parameters and surface blocks.
/// </summary>
/// <param name="Key">Unique biome identifier.</param>
/// <param name="Temperature">Base climate temperature.</param>
/// <param name="Downfall">Base climate humidity/downfall.</param>
/// <param name="SurfaceState">Topmost surface block state.</param>
/// <param name="UnderSurfaceState">Sub-surface filler block state.</param>
public sealed record BiomeDefinition(
    ResourceKey Key,
    float Temperature,
    float Downfall,
    BlockState SurfaceState,
    BlockState UnderSurfaceState);
