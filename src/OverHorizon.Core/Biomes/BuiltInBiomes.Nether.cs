using OverHorizon.Core.Blocks;
using OverHorizon.Core.Registry;

namespace OverHorizon.Core.Biomes;

/// <summary>
/// Nether biome definitions introduced in <c>v0.11.0-alpha</c> (kept in a separate partial file
/// so <see cref="BuiltInBiomes"/> never grows into a God class).
/// </summary>
public static partial class BuiltInBiomes
{
    /// <summary>Key for <c>overhorizon:nether_wastes</c> (the default grey Netherrack wastes).</summary>
    public static readonly ResourceKey NetherWastes = ResourceKey.OverHorizon("nether_wastes");

    /// <summary>Key for <c>overhorizon:crimson_forest</c> (red Crimson Nylium grove).</summary>
    public static readonly ResourceKey CrimsonForest = ResourceKey.OverHorizon("crimson_forest");

    /// <summary>Key for <c>overhorizon:warped_forest</c> (cyan Warped Nylium grove).</summary>
    public static readonly ResourceKey WarpedForest = ResourceKey.OverHorizon("warped_forest");

    /// <summary>Key for <c>overhorizon:soul_sand_valley</c> (ash-covered Soul Sand basin).</summary>
    public static readonly ResourceKey SoulSandValley = ResourceKey.OverHorizon("soul_sand_valley");

    /// <summary>Key for <c>overhorizon:basalt_deltas</c> (jagged Basalt pillars and lava pools).</summary>
    public static readonly ResourceKey BasaltDeltas = ResourceKey.OverHorizon("basalt_deltas");

    /// <summary>
    /// Registers every Nether biome into an unfrozen <paramref name="registry"/>.
    /// </summary>
    /// <param name="registry">Target unfrozen biome registry.</param>
    /// <param name="blocks">Block registry providing surface states.</param>
    public static void PopulateNetherBiomes(MappedRegistry<BiomeDefinition> registry, IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(blocks);

        var netherrack = blocks.GetDefaultState(BuiltInBlocks.Netherrack);
        var crimsonNylium = blocks.GetDefaultState(BuiltInBlocks.CrimsonNylium);
        var warpedNylium = blocks.GetDefaultState(BuiltInBlocks.WarpedNylium);
        var soulSand = blocks.GetDefaultState(BuiltInBlocks.SoulSand);
        var soulSoil = blocks.GetDefaultState(BuiltInBlocks.SoulSoil);
        var basalt = blocks.GetDefaultState(BuiltInBlocks.Basalt);

        _ = registry.Register(NetherWastes, new BiomeDefinition(NetherWastes, Temperature: 2.0f, Downfall: 0.0f, SurfaceState: netherrack, UnderSurfaceState: netherrack));
        _ = registry.Register(CrimsonForest, new BiomeDefinition(CrimsonForest, Temperature: 2.0f, Downfall: 0.3f, SurfaceState: crimsonNylium, UnderSurfaceState: netherrack));
        _ = registry.Register(WarpedForest, new BiomeDefinition(WarpedForest, Temperature: 2.0f, Downfall: 0.3f, SurfaceState: warpedNylium, UnderSurfaceState: netherrack));
        _ = registry.Register(SoulSandValley, new BiomeDefinition(SoulSandValley, Temperature: 2.0f, Downfall: 0.0f, SurfaceState: soulSand, UnderSurfaceState: soulSoil));
        _ = registry.Register(BasaltDeltas, new BiomeDefinition(BasaltDeltas, Temperature: 2.0f, Downfall: 0.0f, SurfaceState: basalt, UnderSurfaceState: basalt));
    }
}
