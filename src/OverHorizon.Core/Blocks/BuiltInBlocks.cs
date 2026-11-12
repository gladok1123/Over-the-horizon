using OverHorizon.Core.Registry;

namespace OverHorizon.Core.Blocks;

/// <summary>
/// Clean-room built-in block definitions for Over-the-Horizon: C# Edition.
/// </summary>
public static class BuiltInBlocks
{
    /// <summary>Key for <c>overhorizon:air</c> (always ID 0).</summary>
    public static readonly ResourceKey Air = ResourceKey.OverHorizon("air");

    /// <summary>Key for <c>overhorizon:stone</c>.</summary>
    public static readonly ResourceKey Stone = ResourceKey.OverHorizon("stone");

    /// <summary>Key for <c>overhorizon:dirt</c>.</summary>
    public static readonly ResourceKey Dirt = ResourceKey.OverHorizon("dirt");

    /// <summary>Key for <c>overhorizon:grass_block</c>.</summary>
    public static readonly ResourceKey GrassBlock = ResourceKey.OverHorizon("grass_block");

    /// <summary>Key for <c>overhorizon:cobblestone</c>.</summary>
    public static readonly ResourceKey Cobblestone = ResourceKey.OverHorizon("cobblestone");

    /// <summary>Key for <c>overhorizon:oak_log</c>.</summary>
    public static readonly ResourceKey OakLog = ResourceKey.OverHorizon("oak_log");

    /// <summary>Key for <c>overhorizon:oak_leaves</c>.</summary>
    public static readonly ResourceKey OakLeaves = ResourceKey.OverHorizon("oak_leaves");

    /// <summary>Key for <c>overhorizon:sand</c>.</summary>
    public static readonly ResourceKey Sand = ResourceKey.OverHorizon("sand");

    /// <summary>Key for <c>overhorizon:water</c>.</summary>
    public static readonly ResourceKey Water = ResourceKey.OverHorizon("water");

    /// <summary>Key for <c>overhorizon:glowstone</c>.</summary>
    public static readonly ResourceKey Glowstone = ResourceKey.OverHorizon("glowstone");

    /// <summary>Key for <c>overhorizon:bedrock</c>.</summary>
    public static readonly ResourceKey Bedrock = ResourceKey.OverHorizon("bedrock");

    /// <summary>Key for <c>overhorizon:oak_planks</c>.</summary>
    public static readonly ResourceKey OakPlanks = ResourceKey.OverHorizon("oak_planks");

    /// <summary>Key for <c>overhorizon:crafting_table</c>.</summary>
    public static readonly ResourceKey CraftingTable = ResourceKey.OverHorizon("crafting_table");

    /// <summary>Key for <c>overhorizon:stone_bricks</c>.</summary>
    public static readonly ResourceKey StoneBricks = ResourceKey.OverHorizon("stone_bricks");

    /// <summary>Key for <c>overhorizon:bricks</c>.</summary>
    public static readonly ResourceKey Bricks = ResourceKey.OverHorizon("bricks");

    /// <summary>Key for <c>overhorizon:glass</c>.</summary>
    public static readonly ResourceKey Glass = ResourceKey.OverHorizon("glass");

    /// <summary>Key for <c>overhorizon:sandstone</c>.</summary>
    public static readonly ResourceKey Sandstone = ResourceKey.OverHorizon("sandstone");

    /// <summary>Key for <c>overhorizon:bookshelf</c>.</summary>
    public static readonly ResourceKey Bookshelf = ResourceKey.OverHorizon("bookshelf");

    /// <summary>Key for <c>overhorizon:coal_ore</c>.</summary>
    public static readonly ResourceKey CoalOre = ResourceKey.OverHorizon("coal_ore");

    /// <summary>Key for <c>overhorizon:iron_ore</c>.</summary>
    public static readonly ResourceKey IronOre = ResourceKey.OverHorizon("iron_ore");

    /// <summary>Key for <c>overhorizon:gold_ore</c>.</summary>
    public static readonly ResourceKey GoldOre = ResourceKey.OverHorizon("gold_ore");

    /// <summary>Key for <c>overhorizon:diamond_ore</c>.</summary>
    public static readonly ResourceKey DiamondOre = ResourceKey.OverHorizon("diamond_ore");

    /// <summary>Key for <c>overhorizon:mossy_cobblestone</c>.</summary>
    public static readonly ResourceKey MossyCobblestone = ResourceKey.OverHorizon("mossy_cobblestone");

    /// <summary>Key for <c>overhorizon:snow_block</c>.</summary>
    public static readonly ResourceKey SnowBlock = ResourceKey.OverHorizon("snow_block");

    /// <summary>Key for <c>overhorizon:crimson_wool</c>.</summary>
    public static readonly ResourceKey CrimsonWool = ResourceKey.OverHorizon("crimson_wool");

    /// <summary>
    /// Registers all built-in blocks into an unfrozen <see cref="BlockRegistry"/>.
    /// </summary>
    /// <param name="registry">Target unfrozen block registry.</param>
    public static void PopulateBuiltIns(BlockRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        _ = registry.Register(new BlockDefinition(Air, isSolid: false, isOpaque: false, hardness: 0.0f));
        _ = registry.Register(new BlockDefinition(Stone, hardness: 1.5f));
        _ = registry.Register(new BlockDefinition(Dirt, hardness: 0.5f));
        _ = registry.Register(new BlockDefinition(GrassBlock, hardness: 0.6f, properties: [BlockProperty.Boolean("snowy")]));
        _ = registry.Register(new BlockDefinition(Cobblestone, hardness: 2.0f));
        _ = registry.Register(new BlockDefinition(OakLog, hardness: 2.0f, properties: [new BlockProperty("axis", ["y", "x", "z"])]));
        _ = registry.Register(new BlockDefinition(OakLeaves, isSolid: true, isOpaque: false, hardness: 0.2f));
        _ = registry.Register(new BlockDefinition(Sand, hardness: 0.5f));
        _ = registry.Register(new BlockDefinition(
            Water,
            isSolid: false,
            isOpaque: false,
            isFluid: true,
            hardness: 100.0f,
            properties: [BlockProperty.IntegerRange("level", 0, 15)]));
        _ = registry.Register(new BlockDefinition(Glowstone, lightEmission: 15, hardness: 0.3f));
        _ = registry.Register(new BlockDefinition(Bedrock, hardness: -1.0f));
        _ = registry.Register(new BlockDefinition(OakPlanks, hardness: 2.0f));
        _ = registry.Register(new BlockDefinition(CraftingTable, hardness: 2.5f));

        // Extended clean-room creative & building palette
        _ = registry.Register(new BlockDefinition(StoneBricks, hardness: 1.5f));
        _ = registry.Register(new BlockDefinition(Bricks, hardness: 2.0f));
        _ = registry.Register(new BlockDefinition(Glass, isSolid: true, isOpaque: false, hardness: 0.3f));
        _ = registry.Register(new BlockDefinition(Sandstone, hardness: 0.8f));
        _ = registry.Register(new BlockDefinition(Bookshelf, hardness: 1.5f));
        _ = registry.Register(new BlockDefinition(CoalOre, hardness: 3.0f));
        _ = registry.Register(new BlockDefinition(IronOre, hardness: 3.0f));
        _ = registry.Register(new BlockDefinition(GoldOre, hardness: 3.0f));
        _ = registry.Register(new BlockDefinition(DiamondOre, hardness: 3.0f));
        _ = registry.Register(new BlockDefinition(MossyCobblestone, hardness: 2.0f));
        _ = registry.Register(new BlockDefinition(SnowBlock, hardness: 0.2f));
        _ = registry.Register(new BlockDefinition(CrimsonWool, hardness: 0.8f));
    }

    /// <summary>
    /// Creates, populates, and freezes the default <see cref="BlockRegistry"/>.
    /// </summary>
    /// <returns>Frozen <see cref="BlockRegistry"/> instance.</returns>
    public static BlockRegistry CreateFrozen()
    {
        var registry = new BlockRegistry();
        PopulateBuiltIns(registry);
        registry.Freeze();
        return registry;
    }
}
