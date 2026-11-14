using OverHorizon.Core.Blocks;
using OverHorizon.Core.Registry;

namespace OverHorizon.Core.Items;

/// <summary>
/// Registration helpers for <see cref="BuiltInItems"/>.
/// </summary>
public static partial class BuiltInItems
{
    /// <summary>Populates all built-in block-items and standalone items into <paramref name="registry"/>.</summary>
    public static void PopulateBuiltIns(MappedRegistry<ItemDefinition> registry, IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(blocks);

        _ = registry.Register(Air, new ItemDefinition(Air, MaxStackSize: 0, DisplayName: "Air"));

        foreach (var block in blocks.Entries)
        {
            if (block.Key == BuiltInBlocks.Air ||
                block.Key == BuiltInBlocks.RedstoneWire ||
                block.Key == BuiltInBlocks.RedstoneTorchOff ||
                block.Key == BuiltInBlocks.LitRedstoneLamp ||
                block.Key == BuiltInBlocks.PistonHead ||
                block.Key == BuiltInBlocks.Fire)
            {
                continue;
            }

            var category = ClassifyBlockTab(block.Key);
            var stackSize = block.Key == BuiltInBlocks.RedBed ? 1 : 64;
            var customDisplayName = string.Empty;
            if (block.Key == BuiltInBlocks.RedstoneRepeater)
            {
                customDisplayName = "Redstone Repeater";
            }
            else if (block.Key == BuiltInBlocks.RedstoneComparator)
            {
                customDisplayName = "Redstone Comparator";
            }
            _ = registry.Register(
                block.Key,
                new ItemDefinition(
                    block.Key,
                    MaxStackSize: stackSize,
                    PlacedBlockKey: block.Key,
                    DisplayName: customDisplayName,
                    Category: category,
                    AttackDamage: 1.0f,
                    UseAction: ItemUseAction.PlaceBlock,
                    HeldLightEmission: block.LightEmission));
        }

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
        _ = registry.Register(
            Redstone,
            new ItemDefinition(
                Redstone,
                MaxStackSize: 64,
                PlacedBlockKey: BuiltInBlocks.RedstoneWire,
                DisplayName: "Redstone Dust",
                Category: ItemCategory.Redstone,
                UseAction: ItemUseAction.PlaceBlock));
        RegisterMaterial(registry, NetheriteIngot, "Netherite Ingot");
        RegisterMaterial(registry, Flint, "Flint");
        RegisterMaterial(registry, Bone, "Bone");
        RegisterMaterial(registry, String, "String");
        RegisterMaterial(registry, Gunpowder, "Gunpowder");
        RegisterMaterial(registry, SpiderEye, "Spider Eye");
        RegisterMaterial(registry, Leather, "Leather");
        RegisterMaterial(registry, Feather, "Feather");
        RegisterMaterial(registry, Paper, "Paper");
        RegisterMaterial(registry, Book, "Book");
        RegisterMaterial(registry, SlimeBall, "Slimeball");
        RegisterMaterial(registry, Wheat, "Wheat");
        _ = registry.Register(WheatSeeds, new ItemDefinition(WheatSeeds, MaxStackSize: 64, DisplayName: "Wheat Seeds", Category: ItemCategory.FoodAndMaterials, UseAction: ItemUseAction.PlantSeed));
        RegisterMaterial(registry, SugarCane, "Sugar Cane");

        RegisterTool(registry, WoodenSword, "Wooden Sword", ToolKind.Sword, damage: 4.0f, speed: 1.5f, tier: 0);
        RegisterTool(registry, StoneSword, "Stone Sword", ToolKind.Sword, damage: 5.0f, speed: 1.5f, tier: 1);
        RegisterTool(registry, IronSword, "Iron Sword", ToolKind.Sword, damage: 6.0f, speed: 1.5f, tier: 2);
        RegisterTool(registry, GoldenSword, "Golden Sword", ToolKind.Sword, damage: 4.0f, speed: 1.5f, tier: 0);
        RegisterTool(registry, DiamondSword, "Diamond Sword", ToolKind.Sword, damage: 7.0f, speed: 1.5f, tier: 3);
        RegisterTool(registry, NetheriteSword, "Netherite Sword", ToolKind.Sword, damage: 8.0f, speed: 1.5f, tier: 4);

        RegisterTool(registry, WoodenPickaxe, "Wooden Pickaxe", ToolKind.Pickaxe, damage: 2.0f, speed: 2.5f, tier: 0);
        RegisterTool(registry, StonePickaxe, "Stone Pickaxe", ToolKind.Pickaxe, damage: 3.0f, speed: 4.5f, tier: 1);
        RegisterTool(registry, IronPickaxe, "Iron Pickaxe", ToolKind.Pickaxe, damage: 4.0f, speed: 6.5f, tier: 2);
        RegisterTool(registry, GoldenPickaxe, "Golden Pickaxe", ToolKind.Pickaxe, damage: 2.0f, speed: 12.0f, tier: 0);
        RegisterTool(registry, DiamondPickaxe, "Diamond Pickaxe", ToolKind.Pickaxe, damage: 5.0f, speed: 9.0f, tier: 3);
        RegisterTool(registry, NetheritePickaxe, "Netherite Pickaxe", ToolKind.Pickaxe, damage: 6.0f, speed: 10.5f, tier: 4);

        RegisterTool(registry, WoodenAxe, "Wooden Axe", ToolKind.Axe, damage: 5.0f, speed: 2.5f, tier: 0);
        RegisterTool(registry, StoneAxe, "Stone Axe", ToolKind.Axe, damage: 6.0f, speed: 4.5f, tier: 1);
        RegisterTool(registry, IronAxe, "Iron Axe", ToolKind.Axe, damage: 6.5f, speed: 6.5f, tier: 2);
        RegisterTool(registry, GoldenAxe, "Golden Axe", ToolKind.Axe, damage: 5.0f, speed: 12.0f, tier: 0);
        RegisterTool(registry, DiamondAxe, "Diamond Axe", ToolKind.Axe, damage: 7.5f, speed: 9.0f, tier: 3);

        RegisterTool(registry, WoodenShovel, "Wooden Shovel", ToolKind.Shovel, damage: 2.5f, speed: 2.5f, tier: 0, action: ItemUseAction.FlattenPath);
        RegisterTool(registry, StoneShovel, "Stone Shovel", ToolKind.Shovel, damage: 3.5f, speed: 4.5f, tier: 1, action: ItemUseAction.FlattenPath);
        RegisterTool(registry, IronShovel, "Iron Shovel", ToolKind.Shovel, damage: 4.5f, speed: 6.5f, tier: 2, action: ItemUseAction.FlattenPath);
        RegisterTool(registry, GoldenShovel, "Golden Shovel", ToolKind.Shovel, damage: 2.5f, speed: 12.0f, tier: 0, action: ItemUseAction.FlattenPath);
        RegisterTool(registry, DiamondShovel, "Diamond Shovel", ToolKind.Shovel, damage: 5.5f, speed: 9.0f, tier: 3, action: ItemUseAction.FlattenPath);

        RegisterTool(registry, WoodenHoe, "Wooden Hoe", ToolKind.Hoe, damage: 1.5f, speed: 2.5f, tier: 0, action: ItemUseAction.TillSoil);
        RegisterTool(registry, StoneHoe, "Stone Hoe", ToolKind.Hoe, damage: 2.0f, speed: 4.5f, tier: 1, action: ItemUseAction.TillSoil);
        RegisterTool(registry, IronHoe, "Iron Hoe", ToolKind.Hoe, damage: 2.5f, speed: 6.5f, tier: 2, action: ItemUseAction.TillSoil);
        RegisterTool(registry, GoldenHoe, "Golden Hoe", ToolKind.Hoe, damage: 1.5f, speed: 12.0f, tier: 0, action: ItemUseAction.TillSoil);
        RegisterTool(registry, DiamondHoe, "Diamond Hoe", ToolKind.Hoe, damage: 3.0f, speed: 9.0f, tier: 3, action: ItemUseAction.TillSoil);

        RegisterTool(registry, Shears, "Shears", ToolKind.Shears, damage: 1.5f, speed: 10.0f, tier: 2);
        _ = registry.Register(FlintAndSteel, new ItemDefinition(FlintAndSteel, MaxStackSize: 1, DisplayName: "Flint and Steel", Category: ItemCategory.CombatAndTools, AttackDamage: 1.5f, UseAction: ItemUseAction.IgniteTnt));
        _ = registry.Register(Bucket, new ItemDefinition(Bucket, MaxStackSize: 16, DisplayName: "Bucket", Category: ItemCategory.FoodAndMaterials, UseAction: ItemUseAction.EmptyBucket));
        _ = registry.Register(WaterBucket, new ItemDefinition(WaterBucket, MaxStackSize: 1, DisplayName: "Water Bucket", Category: ItemCategory.FoodAndMaterials, UseAction: ItemUseAction.WaterBucket));
        _ = registry.Register(Shield, new ItemDefinition(Shield, MaxStackSize: 1, DisplayName: "Shield", Category: ItemCategory.CombatAndTools, AttackDamage: 1.5f, UseAction: ItemUseAction.RaiseShield, ArmorDefense: 2));

        RegisterArmor(registry, LeatherHelmet, "Leather Cap", defense: 1, slotOffset: 0);
        RegisterArmor(registry, LeatherChestplate, "Leather Tunic", defense: 3, slotOffset: 1);
        RegisterArmor(registry, LeatherLeggings, "Leather Pants", defense: 2, slotOffset: 2);
        RegisterArmor(registry, LeatherBoots, "Leather Boots", defense: 1, slotOffset: 3);
        RegisterArmor(registry, IronHelmet, "Iron Helmet", defense: 2, slotOffset: 0);
        RegisterArmor(registry, IronChestplate, "Iron Chestplate", defense: 6, slotOffset: 1);
        RegisterArmor(registry, IronLeggings, "Iron Leggings", defense: 5, slotOffset: 2);
        RegisterArmor(registry, IronBoots, "Iron Boots", defense: 2, slotOffset: 3);
        RegisterArmor(registry, GoldenHelmet, "Golden Helmet", defense: 2, slotOffset: 0);
        RegisterArmor(registry, GoldenChestplate, "Golden Chestplate", defense: 5, slotOffset: 1);
        RegisterArmor(registry, GoldenLeggings, "Golden Leggings", defense: 3, slotOffset: 2);
        RegisterArmor(registry, GoldenBoots, "Golden Boots", defense: 1, slotOffset: 3);
        RegisterArmor(registry, DiamondHelmet, "Diamond Helmet", defense: 3, slotOffset: 0);
        RegisterArmor(registry, DiamondChestplate, "Diamond Chestplate", defense: 8, slotOffset: 1);
        RegisterArmor(registry, DiamondLeggings, "Diamond Leggings", defense: 6, slotOffset: 2);
        RegisterArmor(registry, DiamondBoots, "Diamond Boots", defense: 3, slotOffset: 3);

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

        _ = registry.Register(Minecart, new ItemDefinition(Minecart, MaxStackSize: 1, DisplayName: "Minecart", Category: ItemCategory.Redstone, UseAction: ItemUseAction.PlaceMinecart));
        _ = registry.Register(Bow, new ItemDefinition(Bow, MaxStackSize: 1, DisplayName: "Bow", Category: ItemCategory.CombatAndTools, AttackDamage: 2.0f, UseAction: ItemUseAction.DrawBow));
        _ = registry.Register(Arrow, new ItemDefinition(Arrow, MaxStackSize: 64, DisplayName: "Arrow", Category: ItemCategory.CombatAndTools));

        RegisterMaterial(registry, Quartz, "Nether Quartz");
        _ = registry.Register(BoneMeal, new ItemDefinition(BoneMeal, MaxStackSize: 64, DisplayName: "Bone Meal", Category: ItemCategory.FoodAndMaterials, UseAction: ItemUseAction.ApplyBoneMeal));

        RegisterPotion(registry, SwiftnessPotion, "Potion of Swiftness");
        RegisterPotion(registry, HealingPotion, "Potion of Regeneration");
        RegisterPotion(registry, StrengthPotion, "Potion of Strength");
        RegisterPotion(registry, NightVisionPotion, "Potion of Night Vision");
        RegisterPotion(registry, FireResistancePotion, "Potion of Fire Resistance");

        RegisterSpawnEgg(registry, PigSpawnEgg, "Pig Spawn Egg");
        RegisterSpawnEgg(registry, CowSpawnEgg, "Cow Spawn Egg");
        RegisterSpawnEgg(registry, SheepSpawnEgg, "Sheep Spawn Egg");
        RegisterSpawnEgg(registry, ChickenSpawnEgg, "Chicken Spawn Egg");
        RegisterSpawnEgg(registry, ZombieSpawnEgg, "Zombie Spawn Egg");
        RegisterSpawnEgg(registry, SkeletonSpawnEgg, "Skeleton Spawn Egg");
        RegisterSpawnEgg(registry, CreeperSpawnEgg, "Creeper Spawn Egg");
        RegisterSpawnEgg(registry, SpiderSpawnEgg, "Spider Spawn Egg");

        RegisterMaterial(registry, AmethystShard, "Amethyst Shard");
        RegisterMaterial(registry, GlassBottle, "Glass Bottle");
        RegisterMaterial(registry, ExperienceBottle, "Bottle o' Enchanting");
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
        if (key == BuiltInBlocks.RedstoneBlock || key == BuiltInBlocks.RedstoneTorch ||
            key == BuiltInBlocks.Lever || key == BuiltInBlocks.StoneButton ||
            key == BuiltInBlocks.StonePressurePlate || key == BuiltInBlocks.RedstoneRepeater ||
            key == BuiltInBlocks.RedstoneComparator || key == BuiltInBlocks.OakTrapdoor ||
            key == BuiltInBlocks.IronTrapdoor || key == BuiltInBlocks.OakDoor ||
            key == BuiltInBlocks.IronDoor || key == BuiltInBlocks.RedstoneLamp ||
            key == BuiltInBlocks.Piston || key == BuiltInBlocks.StickyPiston ||
            key == BuiltInBlocks.Tnt || key == BuiltInBlocks.Rail ||
            key == BuiltInBlocks.PoweredRail)
        {
            return ItemCategory.Redstone;
        }

        if (key == BuiltInBlocks.GrassBlock || key == BuiltInBlocks.Dirt || key == BuiltInBlocks.DirtPath ||
            key == BuiltInBlocks.Sand || key == BuiltInBlocks.RedSand || key == BuiltInBlocks.Terracotta ||
            key == BuiltInBlocks.Gravel || key == BuiltInBlocks.OakLog || key == BuiltInBlocks.OakLeaves ||
            key == BuiltInBlocks.BirchLog || key == BuiltInBlocks.BirchLeaves ||
            key == BuiltInBlocks.CherryLog || key == BuiltInBlocks.CherryLeaves ||
            key == BuiltInBlocks.SpruceLog || key == BuiltInBlocks.SpruceLeaves ||
            key == BuiltInBlocks.Deepslate || key == BuiltInBlocks.PackedIce ||
            key == BuiltInBlocks.CoalOre || key == BuiltInBlocks.IronOre || key == BuiltInBlocks.GoldOre ||
            key == BuiltInBlocks.DiamondOre || key == BuiltInBlocks.CopperOre || key == BuiltInBlocks.LapisOre ||
            key == BuiltInBlocks.RedstoneOre || key == BuiltInBlocks.EmeraldOre || key == BuiltInBlocks.SnowBlock ||
            key == BuiltInBlocks.Farmland || key == BuiltInBlocks.HayBlock || key == BuiltInBlocks.AmethystBlock ||
            key == BuiltInBlocks.BuddingAmethyst || key == BuiltInBlocks.AmethystCluster)
        {
            return ItemCategory.NaturalBlocks;
        }

        if (key == BuiltInBlocks.CraftingTable || key == BuiltInBlocks.Furnace || key == BuiltInBlocks.LitFurnace ||
            key == BuiltInBlocks.Chest || key == BuiltInBlocks.Barrel || key == BuiltInBlocks.Torch ||
            key == BuiltInBlocks.Lantern || key == BuiltInBlocks.Ladder || key == BuiltInBlocks.RedBed ||
            key == BuiltInBlocks.Cobweb || key == BuiltInBlocks.Bookshelf || key == BuiltInBlocks.Glowstone ||
            key == BuiltInBlocks.Glass || key == BuiltInBlocks.Composter || key == BuiltInBlocks.MonsterSpawner)
        {
            return ItemCategory.FunctionalBlocks;
        }

        return ItemCategory.BuildingBlocks;
    }

    private static void RegisterPotion(MappedRegistry<ItemDefinition> reg, ResourceKey key, string name) =>
        _ = reg.Register(key, new ItemDefinition(key, MaxStackSize: 16, DisplayName: name, Category: ItemCategory.FoodAndMaterials, UseAction: ItemUseAction.DrinkPotion));

    private static void RegisterSpawnEgg(MappedRegistry<ItemDefinition> reg, ResourceKey key, string name) =>
        _ = reg.Register(key, new ItemDefinition(key, MaxStackSize: 64, DisplayName: name, Category: ItemCategory.CombatAndTools, UseAction: ItemUseAction.SpawnMob));

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
