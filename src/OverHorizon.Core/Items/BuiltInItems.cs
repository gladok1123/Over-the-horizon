using OverHorizon.Core.Blocks;
using OverHorizon.Core.Registry;

namespace OverHorizon.Core.Items;

/// <summary>
/// Built-in item definitions covering all 52 blocks plus 50 tools, weapons, armor pieces, buckets, foods, and materials.
/// </summary>
public static class BuiltInItems
{
    /// <summary>Key for <c>overhorizon:air</c> (empty item ID 0).</summary>
    public static readonly ResourceKey Air = BuiltInBlocks.Air;

    /// <summary>Key for <c>overhorizon:stick</c>.</summary>
    public static readonly ResourceKey Stick = ResourceKey.OverHorizon("stick");

    /// <summary>Key for <c>overhorizon:coal</c>.</summary>
    public static readonly ResourceKey Coal = ResourceKey.OverHorizon("coal");

    /// <summary>Key for <c>overhorizon:charcoal</c>.</summary>
    public static readonly ResourceKey Charcoal = ResourceKey.OverHorizon("charcoal");

    /// <summary>Key for <c>overhorizon:raw_iron</c>.</summary>
    public static readonly ResourceKey RawIron = ResourceKey.OverHorizon("raw_iron");

    /// <summary>Key for <c>overhorizon:raw_gold</c>.</summary>
    public static readonly ResourceKey RawGold = ResourceKey.OverHorizon("raw_gold");

    /// <summary>Key for <c>overhorizon:raw_copper</c>.</summary>
    public static readonly ResourceKey RawCopper = ResourceKey.OverHorizon("raw_copper");

    /// <summary>Key for <c>overhorizon:iron_ingot</c>.</summary>
    public static readonly ResourceKey IronIngot = ResourceKey.OverHorizon("iron_ingot");

    /// <summary>Key for <c>overhorizon:gold_ingot</c>.</summary>
    public static readonly ResourceKey GoldIngot = ResourceKey.OverHorizon("gold_ingot");

    /// <summary>Key for <c>overhorizon:copper_ingot</c>.</summary>
    public static readonly ResourceKey CopperIngot = ResourceKey.OverHorizon("copper_ingot");

    /// <summary>Key for <c>overhorizon:diamond</c>.</summary>
    public static readonly ResourceKey Diamond = ResourceKey.OverHorizon("diamond");

    /// <summary>Key for <c>overhorizon:emerald</c>.</summary>
    public static readonly ResourceKey Emerald = ResourceKey.OverHorizon("emerald");

    /// <summary>Key for <c>overhorizon:lapis_lazuli</c>.</summary>
    public static readonly ResourceKey LapisLazuli = ResourceKey.OverHorizon("lapis_lazuli");

    /// <summary>Key for <c>overhorizon:redstone</c>.</summary>
    public static readonly ResourceKey Redstone = ResourceKey.OverHorizon("redstone");

    /// <summary>Key for <c>overhorizon:netherite_ingot</c>.</summary>
    public static readonly ResourceKey NetheriteIngot = ResourceKey.OverHorizon("netherite_ingot");

    /// <summary>Key for <c>overhorizon:flint</c>.</summary>
    public static readonly ResourceKey Flint = ResourceKey.OverHorizon("flint");

    /// <summary>Key for <c>overhorizon:bone</c>.</summary>
    public static readonly ResourceKey Bone = ResourceKey.OverHorizon("bone");

    /// <summary>Key for <c>overhorizon:string</c>.</summary>
    public static readonly ResourceKey String = ResourceKey.OverHorizon("string");

    /// <summary>Key for <c>overhorizon:gunpowder</c>.</summary>
    public static readonly ResourceKey Gunpowder = ResourceKey.OverHorizon("gunpowder");

    /// <summary>Key for <c>overhorizon:spider_eye</c>.</summary>
    public static readonly ResourceKey SpiderEye = ResourceKey.OverHorizon("spider_eye");

    /// <summary>Key for <c>overhorizon:leather</c>.</summary>
    public static readonly ResourceKey Leather = ResourceKey.OverHorizon("leather");

    /// <summary>Key for <c>overhorizon:feather</c>.</summary>
    public static readonly ResourceKey Feather = ResourceKey.OverHorizon("feather");

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

    /// <summary>Key for <c>overhorizon:wooden_pickaxe</c>.</summary>
    public static readonly ResourceKey WoodenPickaxe = ResourceKey.OverHorizon("wooden_pickaxe");

    /// <summary>Key for <c>overhorizon:stone_pickaxe</c>.</summary>
    public static readonly ResourceKey StonePickaxe = ResourceKey.OverHorizon("stone_pickaxe");

    /// <summary>Key for <c>overhorizon:iron_pickaxe</c>.</summary>
    public static readonly ResourceKey IronPickaxe = ResourceKey.OverHorizon("iron_pickaxe");

    /// <summary>Key for <c>overhorizon:golden_pickaxe</c>.</summary>
    public static readonly ResourceKey GoldenPickaxe = ResourceKey.OverHorizon("golden_pickaxe");

    /// <summary>Key for <c>overhorizon:diamond_pickaxe</c>.</summary>
    public static readonly ResourceKey DiamondPickaxe = ResourceKey.OverHorizon("diamond_pickaxe");

    /// <summary>Key for <c>overhorizon:netherite_pickaxe</c>.</summary>
    public static readonly ResourceKey NetheritePickaxe = ResourceKey.OverHorizon("netherite_pickaxe");

    /// <summary>Key for <c>overhorizon:wooden_axe</c>.</summary>
    public static readonly ResourceKey WoodenAxe = ResourceKey.OverHorizon("wooden_axe");

    /// <summary>Key for <c>overhorizon:stone_axe</c>.</summary>
    public static readonly ResourceKey StoneAxe = ResourceKey.OverHorizon("stone_axe");

    /// <summary>Key for <c>overhorizon:iron_axe</c>.</summary>
    public static readonly ResourceKey IronAxe = ResourceKey.OverHorizon("iron_axe");

    /// <summary>Key for <c>overhorizon:diamond_axe</c>.</summary>
    public static readonly ResourceKey DiamondAxe = ResourceKey.OverHorizon("diamond_axe");

    /// <summary>Key for <c>overhorizon:wooden_shovel</c>.</summary>
    public static readonly ResourceKey WoodenShovel = ResourceKey.OverHorizon("wooden_shovel");

    /// <summary>Key for <c>overhorizon:stone_shovel</c>.</summary>
    public static readonly ResourceKey StoneShovel = ResourceKey.OverHorizon("stone_shovel");

    /// <summary>Key for <c>overhorizon:iron_shovel</c>.</summary>
    public static readonly ResourceKey IronShovel = ResourceKey.OverHorizon("iron_shovel");

    /// <summary>Key for <c>overhorizon:diamond_shovel</c>.</summary>
    public static readonly ResourceKey DiamondShovel = ResourceKey.OverHorizon("diamond_shovel");

    /// <summary>Key for <c>overhorizon:wooden_hoe</c>.</summary>
    public static readonly ResourceKey WoodenHoe = ResourceKey.OverHorizon("wooden_hoe");

    /// <summary>Key for <c>overhorizon:stone_hoe</c>.</summary>
    public static readonly ResourceKey StoneHoe = ResourceKey.OverHorizon("stone_hoe");

    /// <summary>Key for <c>overhorizon:iron_hoe</c>.</summary>
    public static readonly ResourceKey IronHoe = ResourceKey.OverHorizon("iron_hoe");

    /// <summary>Key for <c>overhorizon:diamond_hoe</c>.</summary>
    public static readonly ResourceKey DiamondHoe = ResourceKey.OverHorizon("diamond_hoe");

    /// <summary>Key for <c>overhorizon:shears</c>.</summary>
    public static readonly ResourceKey Shears = ResourceKey.OverHorizon("shears");

    /// <summary>Key for <c>overhorizon:flint_and_steel</c>.</summary>
    public static readonly ResourceKey FlintAndSteel = ResourceKey.OverHorizon("flint_and_steel");

    /// <summary>Key for <c>overhorizon:bucket</c>.</summary>
    public static readonly ResourceKey Bucket = ResourceKey.OverHorizon("bucket");

    /// <summary>Key for <c>overhorizon:water_bucket</c>.</summary>
    public static readonly ResourceKey WaterBucket = ResourceKey.OverHorizon("water_bucket");

    /// <summary>Key for <c>overhorizon:iron_helmet</c>.</summary>
    public static readonly ResourceKey IronHelmet = ResourceKey.OverHorizon("iron_helmet");

    /// <summary>Key for <c>overhorizon:iron_chestplate</c>.</summary>
    public static readonly ResourceKey IronChestplate = ResourceKey.OverHorizon("iron_chestplate");

    /// <summary>Key for <c>overhorizon:iron_leggings</c>.</summary>
    public static readonly ResourceKey IronLeggings = ResourceKey.OverHorizon("iron_leggings");

    /// <summary>Key for <c>overhorizon:iron_boots</c>.</summary>
    public static readonly ResourceKey IronBoots = ResourceKey.OverHorizon("iron_boots");

    /// <summary>Key for <c>overhorizon:diamond_helmet</c>.</summary>
    public static readonly ResourceKey DiamondHelmet = ResourceKey.OverHorizon("diamond_helmet");

    /// <summary>Key for <c>overhorizon:diamond_chestplate</c>.</summary>
    public static readonly ResourceKey DiamondChestplate = ResourceKey.OverHorizon("diamond_chestplate");

    /// <summary>Key for <c>overhorizon:diamond_leggings</c>.</summary>
    public static readonly ResourceKey DiamondLeggings = ResourceKey.OverHorizon("diamond_leggings");

    /// <summary>Key for <c>overhorizon:diamond_boots</c>.</summary>
    public static readonly ResourceKey DiamondBoots = ResourceKey.OverHorizon("diamond_boots");

    /// <summary>Key for <c>overhorizon:apple</c>.</summary>
    public static readonly ResourceKey Apple = ResourceKey.OverHorizon("apple");

    /// <summary>Key for <c>overhorizon:bread</c>.</summary>
    public static readonly ResourceKey Bread = ResourceKey.OverHorizon("bread");

    /// <summary>Key for <c>overhorizon:porkchop</c>.</summary>
    public static readonly ResourceKey RawPorkchop = ResourceKey.OverHorizon("porkchop");

    /// <summary>Key for <c>overhorizon:cooked_porkchop</c>.</summary>
    public static readonly ResourceKey CookedPorkchop = ResourceKey.OverHorizon("cooked_porkchop");

    /// <summary>Key for <c>overhorizon:beef</c>.</summary>
    public static readonly ResourceKey RawBeef = ResourceKey.OverHorizon("beef");

    /// <summary>Key for <c>overhorizon:cooked_beef</c>.</summary>
    public static readonly ResourceKey CookedBeef = ResourceKey.OverHorizon("cooked_beef");

    /// <summary>Key for <c>overhorizon:chicken</c>.</summary>
    public static readonly ResourceKey RawChicken = ResourceKey.OverHorizon("chicken");

    /// <summary>Key for <c>overhorizon:cooked_chicken</c>.</summary>
    public static readonly ResourceKey CookedChicken = ResourceKey.OverHorizon("cooked_chicken");

    /// <summary>Key for <c>overhorizon:mutton</c>.</summary>
    public static readonly ResourceKey RawMutton = ResourceKey.OverHorizon("mutton");

    /// <summary>Key for <c>overhorizon:cooked_mutton</c>.</summary>
    public static readonly ResourceKey CookedMutton = ResourceKey.OverHorizon("cooked_mutton");

    /// <summary>Key for <c>overhorizon:rotten_flesh</c>.</summary>
    public static readonly ResourceKey RottenFlesh = ResourceKey.OverHorizon("rotten_flesh");

    /// <summary>Key for <c>overhorizon:golden_apple</c>.</summary>
    public static readonly ResourceKey GoldenApple = ResourceKey.OverHorizon("golden_apple");

    /// <summary>Populates all built-in block-items and standalone items into <paramref name="registry"/>.</summary>
    public static void PopulateBuiltIns(MappedRegistry<ItemDefinition> registry, IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(blocks);

        _ = registry.Register(Air, new ItemDefinition(Air, MaxStackSize: 0, DisplayName: "Air"));

        foreach (var block in blocks.Entries)
        {
            if (block.Key == BuiltInBlocks.Air)
            {
                continue;
            }

            var category = ClassifyBlockTab(block.Key);
            _ = registry.Register(
                block.Key,
                new ItemDefinition(
                    block.Key,
                    MaxStackSize: 64,
                    PlacedBlockKey: block.Key,
                    Category: category,
                    AttackDamage: 1.0f,
                    UseAction: ItemUseAction.PlaceBlock,
                    HeldLightEmission: block.LightEmission));
        }

        // Materials & Drops
        RegisterMaterial(registry, Stick, "Stick");
        RegisterMaterial(registry, Coal, "Coal");
        RegisterMaterial(registry, Charcoal, "Charcoal");
        RegisterMaterial(registry, RawIron, "Raw Iron");
        RegisterMaterial(registry, RawGold, "Raw Gold");
        RegisterMaterial(registry, RawCopper, "Raw Copper");
        RegisterMaterial(registry, IronIngot, "Iron Ingot");
        RegisterMaterial(registry, GoldIngot, "Gold Ingot");
        RegisterMaterial(registry, CopperIngot, "Copper Ingot");
        RegisterMaterial(registry, Diamond, "Diamond");
        RegisterMaterial(registry, Emerald, "Emerald");
        RegisterMaterial(registry, LapisLazuli, "Lapis Lazuli");
        RegisterMaterial(registry, Redstone, "Redstone Dust");
        RegisterMaterial(registry, NetheriteIngot, "Netherite Ingot");
        RegisterMaterial(registry, Flint, "Flint");
        RegisterMaterial(registry, Bone, "Bone");
        RegisterMaterial(registry, String, "String");
        RegisterMaterial(registry, Gunpowder, "Gunpowder");
        RegisterMaterial(registry, SpiderEye, "Spider Eye");
        RegisterMaterial(registry, Leather, "Leather");
        RegisterMaterial(registry, Feather, "Feather");

        // Swords
        RegisterTool(registry, WoodenSword, "Wooden Sword", ToolKind.Sword, damage: 4.0f, speed: 1.5f, tier: 0);
        RegisterTool(registry, StoneSword, "Stone Sword", ToolKind.Sword, damage: 5.0f, speed: 1.5f, tier: 1);
        RegisterTool(registry, IronSword, "Iron Sword", ToolKind.Sword, damage: 6.0f, speed: 1.5f, tier: 2);
        RegisterTool(registry, GoldenSword, "Golden Sword", ToolKind.Sword, damage: 4.0f, speed: 1.5f, tier: 0);
        RegisterTool(registry, DiamondSword, "Diamond Sword", ToolKind.Sword, damage: 7.0f, speed: 1.5f, tier: 3);
        RegisterTool(registry, NetheriteSword, "Netherite Sword", ToolKind.Sword, damage: 8.0f, speed: 1.5f, tier: 4);

        // Pickaxes
        RegisterTool(registry, WoodenPickaxe, "Wooden Pickaxe", ToolKind.Pickaxe, damage: 2.0f, speed: 2.5f, tier: 0);
        RegisterTool(registry, StonePickaxe, "Stone Pickaxe", ToolKind.Pickaxe, damage: 3.0f, speed: 4.5f, tier: 1);
        RegisterTool(registry, IronPickaxe, "Iron Pickaxe", ToolKind.Pickaxe, damage: 4.0f, speed: 6.5f, tier: 2);
        RegisterTool(registry, GoldenPickaxe, "Golden Pickaxe", ToolKind.Pickaxe, damage: 2.0f, speed: 12.0f, tier: 0);
        RegisterTool(registry, DiamondPickaxe, "Diamond Pickaxe", ToolKind.Pickaxe, damage: 5.0f, speed: 9.0f, tier: 3);
        RegisterTool(registry, NetheritePickaxe, "Netherite Pickaxe", ToolKind.Pickaxe, damage: 6.0f, speed: 10.5f, tier: 4);

        // Axes
        RegisterTool(registry, WoodenAxe, "Wooden Axe", ToolKind.Axe, damage: 5.0f, speed: 2.5f, tier: 0);
        RegisterTool(registry, StoneAxe, "Stone Axe", ToolKind.Axe, damage: 6.0f, speed: 4.5f, tier: 1);
        RegisterTool(registry, IronAxe, "Iron Axe", ToolKind.Axe, damage: 6.5f, speed: 6.5f, tier: 2);
        RegisterTool(registry, DiamondAxe, "Diamond Axe", ToolKind.Axe, damage: 7.5f, speed: 9.0f, tier: 3);

        // Shovels
        RegisterTool(registry, WoodenShovel, "Wooden Shovel", ToolKind.Shovel, damage: 2.5f, speed: 2.5f, tier: 0);
        RegisterTool(registry, StoneShovel, "Stone Shovel", ToolKind.Shovel, damage: 3.5f, speed: 4.5f, tier: 1);
        RegisterTool(registry, IronShovel, "Iron Shovel", ToolKind.Shovel, damage: 4.5f, speed: 6.5f, tier: 2);
        RegisterTool(registry, DiamondShovel, "Diamond Shovel", ToolKind.Shovel, damage: 5.5f, speed: 9.0f, tier: 3);

        // Hoes (right-click tills Dirt/Grass into Farmland!)
        RegisterTool(registry, WoodenHoe, "Wooden Hoe", ToolKind.Hoe, damage: 1.5f, speed: 2.5f, tier: 0, action: ItemUseAction.TillSoil);
        RegisterTool(registry, StoneHoe, "Stone Hoe", ToolKind.Hoe, damage: 2.0f, speed: 4.5f, tier: 1, action: ItemUseAction.TillSoil);
        RegisterTool(registry, IronHoe, "Iron Hoe", ToolKind.Hoe, damage: 2.5f, speed: 6.5f, tier: 2, action: ItemUseAction.TillSoil);
        RegisterTool(registry, DiamondHoe, "Diamond Hoe", ToolKind.Hoe, damage: 3.0f, speed: 9.0f, tier: 3, action: ItemUseAction.TillSoil);

        // Utility Tools & Buckets
        RegisterTool(registry, Shears, "Shears", ToolKind.Shears, damage: 1.5f, speed: 10.0f, tier: 2);
        _ = registry.Register(FlintAndSteel, new ItemDefinition(FlintAndSteel, MaxStackSize: 1, DisplayName: "Flint and Steel", Category: ItemCategory.CombatAndTools, AttackDamage: 1.5f, UseAction: ItemUseAction.IgniteTnt));
        _ = registry.Register(Bucket, new ItemDefinition(Bucket, MaxStackSize: 16, DisplayName: "Bucket", Category: ItemCategory.FoodAndMaterials, UseAction: ItemUseAction.EmptyBucket));
        _ = registry.Register(WaterBucket, new ItemDefinition(WaterBucket, MaxStackSize: 1, DisplayName: "Water Bucket", Category: ItemCategory.FoodAndMaterials, UseAction: ItemUseAction.WaterBucket));

        // Armor
        RegisterArmor(registry, IronHelmet, "Iron Helmet", defense: 2, slotOffset: 0);
        RegisterArmor(registry, IronChestplate, "Iron Chestplate", defense: 6, slotOffset: 1);
        RegisterArmor(registry, IronLeggings, "Iron Leggings", defense: 5, slotOffset: 2);
        RegisterArmor(registry, IronBoots, "Iron Boots", defense: 2, slotOffset: 3);
        RegisterArmor(registry, DiamondHelmet, "Diamond Helmet", defense: 3, slotOffset: 0);
        RegisterArmor(registry, DiamondChestplate, "Diamond Chestplate", defense: 8, slotOffset: 1);
        RegisterArmor(registry, DiamondLeggings, "Diamond Leggings", defense: 6, slotOffset: 2);
        RegisterArmor(registry, DiamondBoots, "Diamond Boots", defense: 3, slotOffset: 3);

        // Foods
        RegisterFood(registry, Apple, "Apple", nutrition: 4, saturation: 2.4f, heal: 0.0f);
        RegisterFood(registry, Bread, "Bread", nutrition: 5, saturation: 6.0f, heal: 0.0f);
        RegisterFood(registry, RawPorkchop, "Raw Porkchop", nutrition: 3, saturation: 1.8f, heal: 0.0f);
        RegisterFood(registry, CookedPorkchop, "Cooked Porkchop", nutrition: 8, saturation: 12.8f, heal: 1.0f);
        RegisterFood(registry, RawBeef, "Raw Beef", nutrition: 3, saturation: 1.8f, heal: 0.0f);
        RegisterFood(registry, CookedBeef, "Steak", nutrition: 8, saturation: 12.8f, heal: 1.0f);
        RegisterFood(registry, RawChicken, "Raw Chicken", nutrition: 2, saturation: 1.2f, heal: 0.0f);
        RegisterFood(registry, CookedChicken, "Cooked Chicken", nutrition: 6, saturation: 7.2f, heal: 0.0f);
        RegisterFood(registry, RawMutton, "Raw Mutton", nutrition: 2, saturation: 1.2f, heal: 0.0f);
        RegisterFood(registry, CookedMutton, "Cooked Mutton", nutrition: 6, saturation: 9.6f, heal: 0.0f);
        RegisterFood(registry, RottenFlesh, "Rotten Flesh", nutrition: 4, saturation: 0.8f, heal: 0.0f);
        RegisterFood(registry, GoldenApple, "Golden Apple", nutrition: 4, saturation: 9.6f, heal: 8.0f);
    }

    /// <summary>Creates, populates, and freezes the default item registry.</summary>
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
            key == BuiltInBlocks.BirchLog || key == BuiltInBlocks.BirchLeaves || key == BuiltInBlocks.Deepslate ||
            key == BuiltInBlocks.CoalOre || key == BuiltInBlocks.IronOre || key == BuiltInBlocks.GoldOre ||
            key == BuiltInBlocks.DiamondOre || key == BuiltInBlocks.CopperOre || key == BuiltInBlocks.LapisOre ||
            key == BuiltInBlocks.RedstoneOre || key == BuiltInBlocks.EmeraldOre || key == BuiltInBlocks.SnowBlock ||
            key == BuiltInBlocks.Farmland || key == BuiltInBlocks.HayBlock)
        {
            return ItemCategory.NaturalBlocks;
        }

        if (key == BuiltInBlocks.CraftingTable || key == BuiltInBlocks.Furnace || key == BuiltInBlocks.LitFurnace ||
            key == BuiltInBlocks.Chest || key == BuiltInBlocks.Barrel || key == BuiltInBlocks.Torch ||
            key == BuiltInBlocks.Lantern || key == BuiltInBlocks.Ladder || key == BuiltInBlocks.Rail ||
            key == BuiltInBlocks.Cobweb || key == BuiltInBlocks.Bookshelf || key == BuiltInBlocks.Glowstone ||
            key == BuiltInBlocks.Glass || key == BuiltInBlocks.Tnt)
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
        float speed,
        int tier,
        ItemUseAction action = ItemUseAction.None) =>
        _ = reg.Register(
            key,
            new ItemDefinition(
                key,
                MaxStackSize: 1,
                DisplayName: name,
                Category: ItemCategory.CombatAndTools,
                ToolType: kind,
                AttackDamage: damage,
                MiningSpeedMultiplier: speed,
                UseAction: action,
                ToolTier: tier));

    private static void RegisterArmor(MappedRegistry<ItemDefinition> reg, ResourceKey key, string name, int defense, int slotOffset) =>
        _ = reg.Register(
            key,
            new ItemDefinition(
                key,
                MaxStackSize: 1,
                DisplayName: name,
                Category: ItemCategory.CombatAndTools,
                UseAction: ItemUseAction.EquipArmor,
                ArmorDefense: defense,
                ArmorSlotOffset: slotOffset));

    private static void RegisterFood(MappedRegistry<ItemDefinition> reg, ResourceKey key, string name, int nutrition, float saturation, float heal) =>
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
