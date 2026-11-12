using OverHorizon.Core.Blocks;
using OverHorizon.Core.Registry;

namespace OverHorizon.Core.Items;

/// <summary>
/// Built-in item definitions covering all blocks, PvP swords, mining tools, buckets, food, and crafting materials.
/// </summary>
public static class BuiltInItems
{
    /// <summary>Key for <c>overhorizon:air</c> (empty item ID 0).</summary>
    public static readonly ResourceKey Air = BuiltInBlocks.Air;

    // Crafting materials & ingots

    /// <summary>Key for <c>overhorizon:stick</c>.</summary>
    public static readonly ResourceKey Stick = ResourceKey.OverHorizon("stick");

    /// <summary>Key for <c>overhorizon:coal</c>.</summary>
    public static readonly ResourceKey Coal = ResourceKey.OverHorizon("coal");

    /// <summary>Key for <c>overhorizon:iron_ingot</c>.</summary>
    public static readonly ResourceKey IronIngot = ResourceKey.OverHorizon("iron_ingot");

    /// <summary>Key for <c>overhorizon:gold_ingot</c>.</summary>
    public static readonly ResourceKey GoldIngot = ResourceKey.OverHorizon("gold_ingot");

    /// <summary>Key for <c>overhorizon:diamond</c>.</summary>
    public static readonly ResourceKey Diamond = ResourceKey.OverHorizon("diamond");

    // Swords (PvP melee weapons)

    /// <summary>Key for <c>overhorizon:wooden_sword</c>.</summary>
    public static readonly ResourceKey WoodenSword = ResourceKey.OverHorizon("wooden_sword");

    /// <summary>Key for <c>overhorizon:stone_sword</c>.</summary>
    public static readonly ResourceKey StoneSword = ResourceKey.OverHorizon("stone_sword");

    /// <summary>Key for <c>overhorizon:iron_sword</c>.</summary>
    public static readonly ResourceKey IronSword = ResourceKey.OverHorizon("iron_sword");

    /// <summary>Key for <c>overhorizon:golden_sword</c>.</summary>
    public static readonly ResourceKey GoldenSword = ResourceKey.OverHorizon("golden_sword");

    /// <summary>Key for <c>overhorizon:diamond_sword</c>.</summary>
    public static readonly ResourceKey DiamondSword = ResourceKey.OverHorizon("diamond_sword");

    /// <summary>Key for <c>overhorizon:netherite_sword</c>.</summary>
    public static readonly ResourceKey NetheriteSword = ResourceKey.OverHorizon("netherite_sword");

    // Pickaxes

    /// <summary>Key for <c>overhorizon:wooden_pickaxe</c>.</summary>
    public static readonly ResourceKey WoodenPickaxe = ResourceKey.OverHorizon("wooden_pickaxe");

    /// <summary>Key for <c>overhorizon:stone_pickaxe</c>.</summary>
    public static readonly ResourceKey StonePickaxe = ResourceKey.OverHorizon("stone_pickaxe");

    /// <summary>Key for <c>overhorizon:iron_pickaxe</c>.</summary>
    public static readonly ResourceKey IronPickaxe = ResourceKey.OverHorizon("iron_pickaxe");

    /// <summary>Key for <c>overhorizon:diamond_pickaxe</c>.</summary>
    public static readonly ResourceKey DiamondPickaxe = ResourceKey.OverHorizon("diamond_pickaxe");

    // Axes & Shovels & Utility Tools

    /// <summary>Key for <c>overhorizon:iron_axe</c>.</summary>
    public static readonly ResourceKey IronAxe = ResourceKey.OverHorizon("iron_axe");

    /// <summary>Key for <c>overhorizon:diamond_axe</c>.</summary>
    public static readonly ResourceKey DiamondAxe = ResourceKey.OverHorizon("diamond_axe");

    /// <summary>Key for <c>overhorizon:iron_shovel</c>.</summary>
    public static readonly ResourceKey IronShovel = ResourceKey.OverHorizon("iron_shovel");

    /// <summary>Key for <c>overhorizon:diamond_shovel</c>.</summary>
    public static readonly ResourceKey DiamondShovel = ResourceKey.OverHorizon("diamond_shovel");

    /// <summary>Key for <c>overhorizon:shears</c>.</summary>
    public static readonly ResourceKey Shears = ResourceKey.OverHorizon("shears");

    /// <summary>Key for <c>overhorizon:flint_and_steel</c>.</summary>
    public static readonly ResourceKey FlintAndSteel = ResourceKey.OverHorizon("flint_and_steel");

    // Fluid Buckets

    /// <summary>Key for <c>overhorizon:bucket</c>.</summary>
    public static readonly ResourceKey Bucket = ResourceKey.OverHorizon("bucket");

    /// <summary>Key for <c>overhorizon:water_bucket</c>.</summary>
    public static readonly ResourceKey WaterBucket = ResourceKey.OverHorizon("water_bucket");

    // Edible Foods

    /// <summary>Key for <c>overhorizon:apple</c>.</summary>
    public static readonly ResourceKey Apple = ResourceKey.OverHorizon("apple");

    /// <summary>Key for <c>overhorizon:bread</c>.</summary>
    public static readonly ResourceKey Bread = ResourceKey.OverHorizon("bread");

    /// <summary>Key for <c>overhorizon:cooked_beef</c>.</summary>
    public static readonly ResourceKey CookedBeef = ResourceKey.OverHorizon("cooked_beef");

    /// <summary>Key for <c>overhorizon:golden_apple</c>.</summary>
    public static readonly ResourceKey GoldenApple = ResourceKey.OverHorizon("golden_apple");

    /// <summary>
    /// Registers air, block-items for all non-fluid blocks in <paramref name="blocks"/>, and all weapons, tools,
    /// buckets, foods, and materials into <paramref name="registry"/>.
    /// </summary>
    /// <param name="registry">Target unfrozen item registry.</param>
    /// <param name="blocks">Block registry providing block-item entries.</param>
    public static void PopulateBuiltIns(MappedRegistry<ItemDefinition> registry, IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(blocks);

        _ = registry.Register(Air, new ItemDefinition(Air, MaxStackSize: 0, DisplayName: "Empty"));

        foreach (var block in blocks.Entries)
        {
            if (block.Key == BuiltInBlocks.Air || block.IsFluid || registry.GetId(block.Key) >= 0)
            {
                continue;
            }

            _ = registry.Register(
                block.Key,
                new ItemDefinition(
                    block.Key,
                    MaxStackSize: 64,
                    PlacedBlockKey: block.Key,
                    Category: ClassifyBlockTab(block.Key),
                    UseAction: ItemUseAction.PlaceBlock));
        }

        // Materials
        RegisterMaterial(registry, Stick, "Stick");
        RegisterMaterial(registry, Coal, "Coal");
        RegisterMaterial(registry, IronIngot, "Iron Ingot");
        RegisterMaterial(registry, GoldIngot, "Gold Ingot");
        RegisterMaterial(registry, Diamond, "Diamond");

        // Swords (PvP)
        RegisterTool(registry, WoodenSword, "Wooden Sword", ToolKind.Sword, damage: 4.0f, speed: 1.5f);
        RegisterTool(registry, StoneSword, "Stone Sword", ToolKind.Sword, damage: 5.0f, speed: 1.5f);
        RegisterTool(registry, GoldenSword, "Golden Sword", ToolKind.Sword, damage: 4.0f, speed: 1.5f);
        RegisterTool(registry, IronSword, "Iron Sword", ToolKind.Sword, damage: 6.0f, speed: 1.5f);
        RegisterTool(registry, DiamondSword, "Diamond Sword", ToolKind.Sword, damage: 7.0f, speed: 1.5f);
        RegisterTool(registry, NetheriteSword, "Netherite Sword", ToolKind.Sword, damage: 8.0f, speed: 1.5f);

        // Pickaxes
        RegisterTool(registry, WoodenPickaxe, "Wooden Pickaxe", ToolKind.Pickaxe, damage: 2.0f, speed: 2.5f);
        RegisterTool(registry, StonePickaxe, "Stone Pickaxe", ToolKind.Pickaxe, damage: 3.0f, speed: 4.5f);
        RegisterTool(registry, IronPickaxe, "Iron Pickaxe", ToolKind.Pickaxe, damage: 4.0f, speed: 6.5f);
        RegisterTool(registry, DiamondPickaxe, "Diamond Pickaxe", ToolKind.Pickaxe, damage: 5.0f, speed: 9.0f);

        // Axes, Shovels & Utility
        RegisterTool(registry, IronAxe, "Iron Axe", ToolKind.Axe, damage: 7.0f, speed: 6.5f);
        RegisterTool(registry, DiamondAxe, "Diamond Axe", ToolKind.Axe, damage: 8.0f, speed: 9.0f);
        RegisterTool(registry, IronShovel, "Iron Shovel", ToolKind.Shovel, damage: 3.5f, speed: 6.5f);
        RegisterTool(registry, DiamondShovel, "Diamond Shovel", ToolKind.Shovel, damage: 4.5f, speed: 9.0f);
        RegisterTool(registry, Shears, "Shears", ToolKind.Shears, damage: 1.5f, speed: 10.0f);

        _ = registry.Register(
            FlintAndSteel,
            new ItemDefinition(
                FlintAndSteel,
                MaxStackSize: 1,
                DisplayName: "Flint and Steel",
                Category: ItemCategory.CombatAndTools,
                AttackDamage: 1.5f,
                UseAction: ItemUseAction.IgniteTnt));

        // Buckets
        _ = registry.Register(
            Bucket,
            new ItemDefinition(
                Bucket,
                MaxStackSize: 16,
                DisplayName: "Bucket",
                Category: ItemCategory.FoodAndMaterials,
                UseAction: ItemUseAction.EmptyBucket));

        _ = registry.Register(
            WaterBucket,
            new ItemDefinition(
                WaterBucket,
                MaxStackSize: 1,
                DisplayName: "Water Bucket",
                Category: ItemCategory.FoodAndMaterials,
                UseAction: ItemUseAction.WaterBucket));

        // Edible Foods
        RegisterFood(registry, Apple, "Apple", nutrition: 4, saturation: 2.4f, heal: 0.0f);
        RegisterFood(registry, Bread, "Bread", nutrition: 5, saturation: 6.0f, heal: 0.0f);
        RegisterFood(registry, CookedBeef, "Steak", nutrition: 8, saturation: 12.8f, heal: 0.0f);
        RegisterFood(registry, GoldenApple, "Golden Apple", nutrition: 4, saturation: 9.6f, heal: 8.0f);
    }

    /// <summary>
    /// Creates, populates, and freezes the default item registry.
    /// </summary>
    /// <param name="blocks">Registered block registry used to create block-items.</param>
    /// <returns>Frozen <see cref="MappedRegistry{T}"/> of <see cref="ItemDefinition"/>.</returns>
    public static MappedRegistry<ItemDefinition> CreateFrozen(IBlockRegistry blocks)
    {
        var registry = new MappedRegistry<ItemDefinition>();
        PopulateBuiltIns(registry, blocks);
        registry.Freeze();
        return registry;
    }

    private static ItemCategory ClassifyBlockTab(ResourceKey key)
    {
        if (key == BuiltInBlocks.GrassBlock || key == BuiltInBlocks.Dirt || key == BuiltInBlocks.Sand ||
            key == BuiltInBlocks.Gravel || key == BuiltInBlocks.OakLog || key == BuiltInBlocks.OakLeaves ||
            key == BuiltInBlocks.CoalOre || key == BuiltInBlocks.IronOre || key == BuiltInBlocks.GoldOre ||
            key == BuiltInBlocks.DiamondOre || key == BuiltInBlocks.SnowBlock)
        {
            return ItemCategory.NaturalBlocks;
        }

        if (key == BuiltInBlocks.CraftingTable || key == BuiltInBlocks.Bookshelf ||
            key == BuiltInBlocks.Glowstone || key == BuiltInBlocks.Glass || key == BuiltInBlocks.Tnt)
        {
            return ItemCategory.FunctionalBlocks;
        }

        return ItemCategory.BuildingBlocks;
    }

    private static void RegisterMaterial(MappedRegistry<ItemDefinition> reg, ResourceKey key, string name) =>
        _ = reg.Register(key, new ItemDefinition(key, MaxStackSize: 64, DisplayName: name, Category: ItemCategory.FoodAndMaterials));

    private static void RegisterTool(
        MappedRegistry<ItemDefinition> reg,
        ResourceKey key,
        string name,
        ToolKind kind,
        float damage,
        float speed) =>
        _ = reg.Register(
            key,
            new ItemDefinition(
                key,
                MaxStackSize: 1,
                DisplayName: name,
                Category: ItemCategory.CombatAndTools,
                ToolType: kind,
                AttackDamage: damage,
                MiningSpeedMultiplier: speed));

    private static void RegisterFood(
        MappedRegistry<ItemDefinition> reg,
        ResourceKey key,
        string name,
        int nutrition,
        float saturation,
        float heal) =>
        _ = reg.Register(
            key,
            new ItemDefinition(
                key,
                MaxStackSize: 64,
                DisplayName: name,
                Category: ItemCategory.FoodAndMaterials,
                FoodNutrition: nutrition,
                FoodSaturation: saturation,
                BonusHealPoints: heal,
                UseAction: ItemUseAction.EatFood));
}
