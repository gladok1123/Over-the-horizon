using OverHorizon.Core.Registry;

namespace OverHorizon.Core.Blocks;

/// <summary>
/// Clean-room built-in block definitions for Over-the-Horizon: C# Edition (52 blocks).
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

    /// <summary>Key for <c>overhorizon:gravel</c>.</summary>
    public static readonly ResourceKey Gravel = ResourceKey.OverHorizon("gravel");

    /// <summary>Key for <c>overhorizon:obsidian</c>.</summary>
    public static readonly ResourceKey Obsidian = ResourceKey.OverHorizon("obsidian");

    /// <summary>Key for <c>overhorizon:tnt</c>.</summary>
    public static readonly ResourceKey Tnt = ResourceKey.OverHorizon("tnt");

    /// <summary>Key for <c>overhorizon:white_wool</c>.</summary>
    public static readonly ResourceKey WhiteWool = ResourceKey.OverHorizon("white_wool");

    /// <summary>Key for <c>overhorizon:blue_wool</c>.</summary>
    public static readonly ResourceKey BlueWool = ResourceKey.OverHorizon("blue_wool");

    /// <summary>Key for <c>overhorizon:torch</c> (emits block light 14).</summary>
    public static readonly ResourceKey Torch = ResourceKey.OverHorizon("torch");

    /// <summary>Key for <c>overhorizon:lantern</c> (emits block light 15).</summary>
    public static readonly ResourceKey Lantern = ResourceKey.OverHorizon("lantern");

    /// <summary>Key for <c>overhorizon:furnace</c>.</summary>
    public static readonly ResourceKey Furnace = ResourceKey.OverHorizon("furnace");

    /// <summary>Key for <c>overhorizon:lit_furnace</c> (emits block light 13).</summary>
    public static readonly ResourceKey LitFurnace = ResourceKey.OverHorizon("lit_furnace");

    /// <summary>Key for <c>overhorizon:chest</c>.</summary>
    public static readonly ResourceKey Chest = ResourceKey.OverHorizon("chest");

    /// <summary>Key for <c>overhorizon:barrel</c>.</summary>
    public static readonly ResourceKey Barrel = ResourceKey.OverHorizon("barrel");

    /// <summary>Key for <c>overhorizon:ladder</c> (climbable).</summary>
    public static readonly ResourceKey Ladder = ResourceKey.OverHorizon("ladder");

    /// <summary>Key for <c>overhorizon:rail</c> (mineshaft track).</summary>
    public static readonly ResourceKey Rail = ResourceKey.OverHorizon("rail");

    /// <summary>Key for <c>overhorizon:cobweb</c> (mineshaft web).</summary>
    public static readonly ResourceKey Cobweb = ResourceKey.OverHorizon("cobweb");

    /// <summary>Key for <c>overhorizon:oak_fence</c> (mineshaft support post).</summary>
    public static readonly ResourceKey OakFence = ResourceKey.OverHorizon("oak_fence");

    /// <summary>Key for <c>overhorizon:farmland</c> (tilled soil).</summary>
    public static readonly ResourceKey Farmland = ResourceKey.OverHorizon("farmland");

    /// <summary>Key for <c>overhorizon:hay_block</c>.</summary>
    public static readonly ResourceKey HayBlock = ResourceKey.OverHorizon("hay_block");

    /// <summary>Key for <c>overhorizon:deepslate</c>.</summary>
    public static readonly ResourceKey Deepslate = ResourceKey.OverHorizon("deepslate");

    /// <summary>Key for <c>overhorizon:cobbled_deepslate</c>.</summary>
    public static readonly ResourceKey CobbledDeepslate = ResourceKey.OverHorizon("cobbled_deepslate");

    /// <summary>Key for <c>overhorizon:copper_ore</c>.</summary>
    public static readonly ResourceKey CopperOre = ResourceKey.OverHorizon("copper_ore");

    /// <summary>Key for <c>overhorizon:lapis_ore</c>.</summary>
    public static readonly ResourceKey LapisOre = ResourceKey.OverHorizon("lapis_ore");

    /// <summary>Key for <c>overhorizon:redstone_ore</c>.</summary>
    public static readonly ResourceKey RedstoneOre = ResourceKey.OverHorizon("redstone_ore");

    /// <summary>Key for <c>overhorizon:emerald_ore</c>.</summary>
    public static readonly ResourceKey EmeraldOre = ResourceKey.OverHorizon("emerald_ore");

    /// <summary>Key for <c>overhorizon:birch_log</c>.</summary>
    public static readonly ResourceKey BirchLog = ResourceKey.OverHorizon("birch_log");

    /// <summary>Key for <c>overhorizon:birch_leaves</c>.</summary>
    public static readonly ResourceKey BirchLeaves = ResourceKey.OverHorizon("birch_leaves");

    /// <summary>Key for <c>overhorizon:birch_planks</c>.</summary>
    public static readonly ResourceKey BirchPlanks = ResourceKey.OverHorizon("birch_planks");

    /// <summary>Key for <c>overhorizon:coal_block</c>.</summary>
    public static readonly ResourceKey CoalBlock = ResourceKey.OverHorizon("coal_block");

    /// <summary>Key for <c>overhorizon:iron_block</c>.</summary>
    public static readonly ResourceKey IronBlock = ResourceKey.OverHorizon("iron_block");

    /// <summary>Key for <c>overhorizon:gold_block</c>.</summary>
    public static readonly ResourceKey GoldBlock = ResourceKey.OverHorizon("gold_block");

    /// <summary>Key for <c>overhorizon:diamond_block</c>.</summary>
    public static readonly ResourceKey DiamondBlock = ResourceKey.OverHorizon("diamond_block");

    /// <summary>
    /// Registers all built-in blocks into an unfrozen <see cref="BlockRegistry"/>.
    /// </summary>
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
        _ = registry.Register(new BlockDefinition(Gravel, hardness: 0.6f));
        _ = registry.Register(new BlockDefinition(Obsidian, hardness: 12.0f));
        _ = registry.Register(new BlockDefinition(Tnt, hardness: 0.1f));
        _ = registry.Register(new BlockDefinition(WhiteWool, hardness: 0.8f));
        _ = registry.Register(new BlockDefinition(BlueWool, hardness: 0.8f));

        // v0.5.0-alpha Survival, Mineshaft, Lighting & Ore Expansion
        _ = registry.Register(new BlockDefinition(Torch, isSolid: false, isOpaque: false, lightEmission: 14, hardness: 0.05f));
        _ = registry.Register(new BlockDefinition(Lantern, isSolid: true, isOpaque: false, lightEmission: 15, hardness: 0.5f));
        _ = registry.Register(new BlockDefinition(Furnace, hardness: 3.5f));
        _ = registry.Register(new BlockDefinition(LitFurnace, lightEmission: 13, hardness: 3.5f));
        _ = registry.Register(new BlockDefinition(Chest, hardness: 2.5f));
        _ = registry.Register(new BlockDefinition(Barrel, hardness: 2.5f));
        _ = registry.Register(new BlockDefinition(Ladder, isSolid: false, isOpaque: false, hardness: 0.4f));
        _ = registry.Register(new BlockDefinition(Rail, isSolid: false, isOpaque: false, hardness: 0.5f));
        _ = registry.Register(new BlockDefinition(Cobweb, isSolid: false, isOpaque: false, hardness: 2.5f));
        _ = registry.Register(new BlockDefinition(OakFence, isSolid: true, isOpaque: false, hardness: 2.0f));
        _ = registry.Register(new BlockDefinition(Farmland, hardness: 0.6f));
        _ = registry.Register(new BlockDefinition(HayBlock, hardness: 0.5f));

        _ = registry.Register(new BlockDefinition(Deepslate, hardness: 3.0f));
        _ = registry.Register(new BlockDefinition(CobbledDeepslate, hardness: 3.5f));
        _ = registry.Register(new BlockDefinition(CopperOre, hardness: 3.0f));
        _ = registry.Register(new BlockDefinition(LapisOre, hardness: 3.0f));
        _ = registry.Register(new BlockDefinition(RedstoneOre, lightEmission: 7, hardness: 3.0f));
        _ = registry.Register(new BlockDefinition(EmeraldOre, hardness: 3.0f));
        _ = registry.Register(new BlockDefinition(BirchLog, hardness: 2.0f));
        _ = registry.Register(new BlockDefinition(BirchLeaves, isSolid: true, isOpaque: false, hardness: 0.2f));
        _ = registry.Register(new BlockDefinition(BirchPlanks, hardness: 2.0f));
        _ = registry.Register(new BlockDefinition(CoalBlock, hardness: 5.0f));
        _ = registry.Register(new BlockDefinition(IronBlock, hardness: 5.0f));
        _ = registry.Register(new BlockDefinition(GoldBlock, hardness: 3.0f));
        _ = registry.Register(new BlockDefinition(DiamondBlock, hardness: 5.0f));
    }

    /// <summary>Creates, populates, and freezes the default <see cref="BlockRegistry"/>.</summary>
    public static BlockRegistry CreateFrozen()
    {
        var registry = new BlockRegistry();
        PopulateBuiltIns(registry);
        registry.Freeze();
        return registry;
    }
}
