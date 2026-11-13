using OverHorizon.Core.Blocks;
using OverHorizon.Core.Registry;

namespace OverHorizon.Core.Biomes;

/// <summary>
/// Clean-room built-in biome definitions for the OverHorizon MVP.
/// </summary>
public static class BuiltInBiomes
{
    /// <summary>Key for <c>overhorizon:plains</c>.</summary>
    public static readonly ResourceKey Plains = ResourceKey.OverHorizon("plains");

    /// <summary>Key for <c>overhorizon:forest</c>.</summary>
    public static readonly ResourceKey Forest = ResourceKey.OverHorizon("forest");

    /// <summary>Key for <c>overhorizon:desert</c>.</summary>
    public static readonly ResourceKey Desert = ResourceKey.OverHorizon("desert");

    /// <summary>Key for <c>overhorizon:ocean</c>.</summary>
    public static readonly ResourceKey Ocean = ResourceKey.OverHorizon("ocean");

    /// <summary>Key for <c>overhorizon:cherry_grove</c>.</summary>
    public static readonly ResourceKey CherryGrove = ResourceKey.OverHorizon("cherry_grove");

    /// <summary>Key for <c>overhorizon:snowy_taiga</c>.</summary>
    public static readonly ResourceKey SnowyTaiga = ResourceKey.OverHorizon("snowy_taiga");

    /// <summary>Key for <c>overhorizon:birch_forest</c>.</summary>
    public static readonly ResourceKey BirchForest = ResourceKey.OverHorizon("birch_forest");

    /// <summary>Key for <c>overhorizon:badlands</c>.</summary>
    public static readonly ResourceKey Badlands = ResourceKey.OverHorizon("badlands");

    /// <summary>Key for <c>overhorizon:ice_spikes</c>.</summary>
    public static readonly ResourceKey IceSpikes = ResourceKey.OverHorizon("ice_spikes");

    /// <summary>Key for <c>overhorizon:savanna</c>.</summary>
    public static readonly ResourceKey Savanna = ResourceKey.OverHorizon("savanna");

    /// <summary>
    /// Registers built-in biomes into an unfrozen <paramref name="registry"/>.
    /// </summary>
    /// <param name="registry">Target unfrozen biome registry.</param>
    /// <param name="blocks">Block registry providing surface states.</param>
    public static void PopulateBuiltIns(MappedRegistry<BiomeDefinition> registry, IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(blocks);

        var grass = blocks.GetDefaultState(BuiltInBlocks.GrassBlock);
        var dirt = blocks.GetDefaultState(BuiltInBlocks.Dirt);
        var sand = blocks.GetDefaultState(BuiltInBlocks.Sand);
        var snow = blocks.GetDefaultState(BuiltInBlocks.SnowBlock);
        var redSand = blocks.GetDefaultState(BuiltInBlocks.RedSand);
        var terracotta = blocks.GetDefaultState(BuiltInBlocks.Terracotta);

        _ = registry.Register(Plains, new BiomeDefinition(Plains, Temperature: 0.8f, Downfall: 0.4f, SurfaceState: grass, UnderSurfaceState: dirt));
        _ = registry.Register(Forest, new BiomeDefinition(Forest, Temperature: 0.7f, Downfall: 0.8f, SurfaceState: grass, UnderSurfaceState: dirt));
        _ = registry.Register(Desert, new BiomeDefinition(Desert, Temperature: 2.0f, Downfall: 0.0f, SurfaceState: sand, UnderSurfaceState: sand));
        _ = registry.Register(Ocean, new BiomeDefinition(Ocean, Temperature: 0.5f, Downfall: 0.5f, SurfaceState: sand, UnderSurfaceState: dirt));
        _ = registry.Register(CherryGrove, new BiomeDefinition(CherryGrove, Temperature: 0.5f, Downfall: 0.8f, SurfaceState: grass, UnderSurfaceState: dirt));
        _ = registry.Register(SnowyTaiga, new BiomeDefinition(SnowyTaiga, Temperature: -0.5f, Downfall: 0.4f, SurfaceState: snow, UnderSurfaceState: dirt));
        _ = registry.Register(BirchForest, new BiomeDefinition(BirchForest, Temperature: 0.6f, Downfall: 0.6f, SurfaceState: grass, UnderSurfaceState: dirt));
        _ = registry.Register(Badlands, new BiomeDefinition(Badlands, Temperature: 2.0f, Downfall: 0.0f, SurfaceState: redSand, UnderSurfaceState: terracotta));
        _ = registry.Register(IceSpikes, new BiomeDefinition(IceSpikes, Temperature: 0.0f, Downfall: 0.5f, SurfaceState: snow, UnderSurfaceState: snow));
        _ = registry.Register(Savanna, new BiomeDefinition(Savanna, Temperature: 1.2f, Downfall: 0.0f, SurfaceState: grass, UnderSurfaceState: dirt));
    }

    /// <summary>
    /// Creates, populates, and freezes the default MVP biome registry.
    /// </summary>
    /// <param name="blocks">Frozen block registry providing surface states.</param>
    /// <returns>Frozen <see cref="MappedRegistry{T}"/> of <see cref="BiomeDefinition"/>.</returns>
    public static MappedRegistry<BiomeDefinition> CreateFrozen(IBlockRegistry blocks)
    {
        var registry = new MappedRegistry<BiomeDefinition>();
        PopulateBuiltIns(registry, blocks);
        registry.Freeze();
        return registry;
    }
}
