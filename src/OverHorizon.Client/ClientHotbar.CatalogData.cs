using System.Numerics;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Registry;

namespace OverHorizon.Client;

/// <summary>
/// Creative Catalog entries for <see cref="ClientHotbar"/> (indices <c>0..116</c> preserve legacy ordering; <c>117+</c> add v0.6.0-alpha blocks &amp; items).
/// </summary>
public sealed partial class ClientHotbar
{
    private static readonly (ResourceKey Key, string DisplayName, ItemCategory Category, Vector3 Color)[] Catalog =
    [
        // Indices 0..21 preserve deterministic block ordering + full 23w13a__or__b sprites
        (BuiltInBlocks.Cobblestone, "Cobblestone", ItemCategory.BuildingBlocks, new Vector3(0.46f, 0.46f, 0.48f)),
        (BuiltInBlocks.Stone, "Stone", ItemCategory.BuildingBlocks, new Vector3(0.56f, 0.56f, 0.58f)),
        (BuiltInBlocks.Dirt, "Dirt", ItemCategory.NaturalBlocks, new Vector3(0.53f, 0.37f, 0.24f)),
        (BuiltInBlocks.GrassBlock, "Grass Block", ItemCategory.NaturalBlocks, new Vector3(0.36f, 0.69f, 0.26f)),
        (BuiltInBlocks.OakPlanks, "Oak Planks", ItemCategory.BuildingBlocks, new Vector3(0.72f, 0.57f, 0.35f)),
        (BuiltInBlocks.OakLog, "Oak Log", ItemCategory.NaturalBlocks, new Vector3(0.42f, 0.31f, 0.19f)),
        (BuiltInBlocks.Sand, "Sand", ItemCategory.NaturalBlocks, new Vector3(0.88f, 0.83f, 0.60f)),
        (BuiltInBlocks.OakLeaves, "Oak Leaves", ItemCategory.NaturalBlocks, new Vector3(0.22f, 0.53f, 0.18f)),
        (BuiltInBlocks.Glowstone, "Glowstone Lamp", ItemCategory.FunctionalBlocks, new Vector3(0.98f, 0.86f, 0.42f)),
        (BuiltInBlocks.StoneBricks, "Stone Bricks", ItemCategory.BuildingBlocks, new Vector3(0.52f, 0.52f, 0.55f)),
        (BuiltInBlocks.Bricks, "Clay Bricks", ItemCategory.BuildingBlocks, new Vector3(0.66f, 0.31f, 0.25f)),
        (BuiltInBlocks.Glass, "Glass Block", ItemCategory.FunctionalBlocks, new Vector3(0.72f, 0.90f, 0.96f)),
        (BuiltInBlocks.Sandstone, "Sandstone", ItemCategory.BuildingBlocks, new Vector3(0.85f, 0.79f, 0.58f)),
        (BuiltInBlocks.Bookshelf, "Bookshelf", ItemCategory.FunctionalBlocks, new Vector3(0.62f, 0.44f, 0.28f)),
        (BuiltInBlocks.CraftingTable, "Crafting Table", ItemCategory.FunctionalBlocks, new Vector3(0.64f, 0.47f, 0.28f)),
        (BuiltInBlocks.MossyCobblestone, "Mossy Cobblestone", ItemCategory.BuildingBlocks, new Vector3(0.36f, 0.52f, 0.34f)),
        (BuiltInBlocks.SnowBlock, "Snow Block", ItemCategory.NaturalBlocks, new Vector3(0.94f, 0.96f, 0.98f)),
        (BuiltInBlocks.CrimsonWool, "Crimson Wool", ItemCategory.BuildingBlocks, new Vector3(0.74f, 0.18f, 0.22f)),
        (BuiltInBlocks.CoalOre, "Coal Ore", ItemCategory.NaturalBlocks, new Vector3(0.24f, 0.24f, 0.26f)),
        (BuiltInBlocks.IronOre, "Iron Ore", ItemCategory.NaturalBlocks, new Vector3(0.78f, 0.66f, 0.56f)),
        (BuiltInBlocks.GoldOre, "Gold Ore", ItemCategory.NaturalBlocks, new Vector3(0.95f, 0.82f, 0.24f)),
        (BuiltInBlocks.DiamondOre, "Diamond Ore", ItemCategory.NaturalBlocks, new Vector3(0.32f, 0.92f, 0.88f)),

        // Indices 22..26
        (BuiltInBlocks.Gravel, "Gravel", ItemCategory.NaturalBlocks, new Vector3(0.54f, 0.52f, 0.52f)),
        (BuiltInBlocks.Obsidian, "Obsidian", ItemCategory.BuildingBlocks, new Vector3(0.16f, 0.12f, 0.24f)),
        (BuiltInBlocks.Tnt, "TNT", ItemCategory.Redstone, new Vector3(0.86f, 0.24f, 0.20f)),
        (BuiltInBlocks.WhiteWool, "White Wool", ItemCategory.BuildingBlocks, new Vector3(0.94f, 0.94f, 0.96f)),
        (BuiltInBlocks.BlueWool, "Blue Wool", ItemCategory.BuildingBlocks, new Vector3(0.22f, 0.34f, 0.78f)),

        // Indices 27..41 (31 == DiamondSword)
        (BuiltInItems.WoodenSword, "Wooden Sword", ItemCategory.CombatAndTools, new Vector3(0.68f, 0.52f, 0.30f)),
        (BuiltInItems.StoneSword, "Stone Sword", ItemCategory.CombatAndTools, new Vector3(0.58f, 0.58f, 0.60f)),
        (BuiltInItems.IronSword, "Iron Sword", ItemCategory.CombatAndTools, new Vector3(0.86f, 0.88f, 0.90f)),
        (BuiltInItems.GoldenSword, "Golden Sword", ItemCategory.CombatAndTools, new Vector3(0.98f, 0.86f, 0.24f)),
        (BuiltInItems.DiamondSword, "Diamond Sword", ItemCategory.CombatAndTools, new Vector3(0.28f, 0.92f, 0.88f)),
        (BuiltInItems.NetheriteSword, "Netherite Sword", ItemCategory.CombatAndTools, new Vector3(0.28f, 0.24f, 0.30f)),
        (BuiltInItems.WoodenPickaxe, "Wooden Pickaxe", ItemCategory.CombatAndTools, new Vector3(0.68f, 0.52f, 0.30f)),
        (BuiltInItems.StonePickaxe, "Stone Pickaxe", ItemCategory.CombatAndTools, new Vector3(0.58f, 0.58f, 0.60f)),
        (BuiltInItems.IronPickaxe, "Iron Pickaxe", ItemCategory.CombatAndTools, new Vector3(0.86f, 0.88f, 0.90f)),
        (BuiltInItems.DiamondPickaxe, "Diamond Pickaxe", ItemCategory.CombatAndTools, new Vector3(0.28f, 0.92f, 0.88f)),
        (BuiltInItems.IronAxe, "Iron Axe", ItemCategory.CombatAndTools, new Vector3(0.86f, 0.88f, 0.90f)),
        (BuiltInItems.DiamondAxe, "Diamond Axe", ItemCategory.CombatAndTools, new Vector3(0.28f, 0.92f, 0.88f)),
        (BuiltInItems.IronShovel, "Iron Shovel", ItemCategory.CombatAndTools, new Vector3(0.86f, 0.88f, 0.90f)),
        (BuiltInItems.Shears, "Shears", ItemCategory.CombatAndTools, new Vector3(0.86f, 0.88f, 0.90f)),
        (BuiltInItems.FlintAndSteel, "Flint and Steel", ItemCategory.CombatAndTools, new Vector3(0.75f, 0.76f, 0.80f)),

        // Indices 42..52
        (BuiltInItems.Bucket, "Bucket", ItemCategory.FoodAndMaterials, new Vector3(0.78f, 0.80f, 0.84f)),
        (BuiltInItems.WaterBucket, "Water Bucket", ItemCategory.FoodAndMaterials, new Vector3(0.22f, 0.52f, 0.96f)),
        (BuiltInItems.Apple, "Apple", ItemCategory.FoodAndMaterials, new Vector3(0.90f, 0.20f, 0.22f)),
        (BuiltInItems.Bread, "Bread", ItemCategory.FoodAndMaterials, new Vector3(0.82f, 0.62f, 0.28f)),
        (BuiltInItems.CookedBeef, "Steak", ItemCategory.FoodAndMaterials, new Vector3(0.58f, 0.28f, 0.18f)),
        (BuiltInItems.GoldenApple, "Golden Apple", ItemCategory.FoodAndMaterials, new Vector3(0.98f, 0.86f, 0.22f)),
        (BuiltInItems.Stick, "Stick", ItemCategory.FoodAndMaterials, new Vector3(0.62f, 0.46f, 0.26f)),
        (BuiltInItems.Coal, "Coal", ItemCategory.FoodAndMaterials, new Vector3(0.18f, 0.18f, 0.20f)),
        (BuiltInItems.IronIngot, "Iron Ingot", ItemCategory.FoodAndMaterials, new Vector3(0.88f, 0.90f, 0.92f)),
        (BuiltInItems.GoldIngot, "Gold Ingot", ItemCategory.FoodAndMaterials, new Vector3(0.98f, 0.84f, 0.22f)),
        (BuiltInItems.Diamond, "Diamond", ItemCategory.FoodAndMaterials, new Vector3(0.30f, 0.94f, 0.90f)),

        // Indices 53..76
        (BuiltInBlocks.Torch, "Torch", ItemCategory.FunctionalBlocks, new Vector3(1.00f, 0.84f, 0.32f)),
        (BuiltInBlocks.Lantern, "Lantern", ItemCategory.FunctionalBlocks, new Vector3(0.98f, 0.78f, 0.30f)),
        (BuiltInBlocks.Furnace, "Furnace", ItemCategory.FunctionalBlocks, new Vector3(0.50f, 0.50f, 0.52f)),
        (BuiltInBlocks.Chest, "Chest", ItemCategory.FunctionalBlocks, new Vector3(0.68f, 0.48f, 0.22f)),
        (BuiltInBlocks.Barrel, "Barrel", ItemCategory.FunctionalBlocks, new Vector3(0.54f, 0.40f, 0.24f)),
        (BuiltInBlocks.Ladder, "Ladder", ItemCategory.FunctionalBlocks, new Vector3(0.68f, 0.52f, 0.28f)),
        (BuiltInBlocks.Rail, "Rail", ItemCategory.Redstone, new Vector3(0.72f, 0.68f, 0.60f)),
        (BuiltInBlocks.Cobweb, "Cobweb", ItemCategory.FunctionalBlocks, new Vector3(0.92f, 0.93f, 0.95f)),
        (BuiltInBlocks.OakFence, "Oak Fence", ItemCategory.FunctionalBlocks, new Vector3(0.72f, 0.57f, 0.35f)),
        (BuiltInBlocks.BirchLog, "Birch Log", ItemCategory.NaturalBlocks, new Vector3(0.88f, 0.86f, 0.82f)),
        (BuiltInBlocks.BirchPlanks, "Birch Planks", ItemCategory.BuildingBlocks, new Vector3(0.86f, 0.80f, 0.58f)),
        (BuiltInBlocks.BirchLeaves, "Birch Leaves", ItemCategory.NaturalBlocks, new Vector3(0.38f, 0.62f, 0.28f)),
        (BuiltInBlocks.Deepslate, "Deepslate", ItemCategory.BuildingBlocks, new Vector3(0.30f, 0.30f, 0.33f)),
        (BuiltInBlocks.CobbledDeepslate, "Cobbled Deepslate", ItemCategory.BuildingBlocks, new Vector3(0.28f, 0.28f, 0.31f)),
        (BuiltInBlocks.CopperOre, "Copper Ore", ItemCategory.NaturalBlocks, new Vector3(0.82f, 0.48f, 0.32f)),
        (BuiltInBlocks.LapisOre, "Lapis Lazuli Ore", ItemCategory.NaturalBlocks, new Vector3(0.20f, 0.38f, 0.86f)),
        (BuiltInBlocks.RedstoneOre, "Redstone Ore", ItemCategory.NaturalBlocks, new Vector3(0.86f, 0.16f, 0.18f)),
        (BuiltInBlocks.EmeraldOre, "Emerald Ore", ItemCategory.NaturalBlocks, new Vector3(0.22f, 0.88f, 0.42f)),
        (BuiltInBlocks.CoalBlock, "Block of Coal", ItemCategory.BuildingBlocks, new Vector3(0.14f, 0.14f, 0.16f)),
        (BuiltInBlocks.IronBlock, "Block of Iron", ItemCategory.BuildingBlocks, new Vector3(0.90f, 0.91f, 0.93f)),
        (BuiltInBlocks.GoldBlock, "Block of Gold", ItemCategory.BuildingBlocks, new Vector3(0.98f, 0.84f, 0.22f)),
        (BuiltInBlocks.DiamondBlock, "Block of Diamond", ItemCategory.BuildingBlocks, new Vector3(0.34f, 0.94f, 0.90f)),
        (BuiltInBlocks.Farmland, "Farmland", ItemCategory.NaturalBlocks, new Vector3(0.42f, 0.28f, 0.16f)),
        (BuiltInBlocks.HayBlock, "Hay Bale", ItemCategory.NaturalBlocks, new Vector3(0.84f, 0.72f, 0.22f)),

        // Indices 77..116
        (BuiltInItems.WoodenHoe, "Wooden Hoe", ItemCategory.CombatAndTools, new Vector3(0.68f, 0.52f, 0.30f)),
        (BuiltInItems.StoneHoe, "Stone Hoe", ItemCategory.CombatAndTools, new Vector3(0.58f, 0.58f, 0.60f)),
        (BuiltInItems.IronHoe, "Iron Hoe", ItemCategory.CombatAndTools, new Vector3(0.86f, 0.88f, 0.90f)),
        (BuiltInItems.DiamondHoe, "Diamond Hoe", ItemCategory.CombatAndTools, new Vector3(0.28f, 0.92f, 0.88f)),
        (BuiltInItems.GoldenPickaxe, "Golden Pickaxe", ItemCategory.CombatAndTools, new Vector3(0.98f, 0.84f, 0.22f)),
        (BuiltInItems.NetheritePickaxe, "Netherite Pickaxe", ItemCategory.CombatAndTools, new Vector3(0.28f, 0.24f, 0.30f)),
        (BuiltInItems.WoodenAxe, "Wooden Axe", ItemCategory.CombatAndTools, new Vector3(0.68f, 0.52f, 0.30f)),
        (BuiltInItems.StoneAxe, "Stone Axe", ItemCategory.CombatAndTools, new Vector3(0.58f, 0.58f, 0.60f)),
        (BuiltInItems.WoodenShovel, "Wooden Shovel", ItemCategory.CombatAndTools, new Vector3(0.68f, 0.52f, 0.30f)),
        (BuiltInItems.StoneShovel, "Stone Shovel", ItemCategory.CombatAndTools, new Vector3(0.58f, 0.58f, 0.60f)),
        (BuiltInItems.DiamondShovel, "Diamond Shovel", ItemCategory.CombatAndTools, new Vector3(0.28f, 0.92f, 0.88f)),
        (BuiltInItems.IronHelmet, "Iron Helmet", ItemCategory.CombatAndTools, new Vector3(0.88f, 0.90f, 0.92f)),
        (BuiltInItems.IronChestplate, "Iron Chestplate", ItemCategory.CombatAndTools, new Vector3(0.88f, 0.90f, 0.92f)),
        (BuiltInItems.IronLeggings, "Iron Leggings", ItemCategory.CombatAndTools, new Vector3(0.88f, 0.90f, 0.92f)),
        (BuiltInItems.IronBoots, "Iron Boots", ItemCategory.CombatAndTools, new Vector3(0.88f, 0.90f, 0.92f)),
        (BuiltInItems.DiamondHelmet, "Diamond Helmet", ItemCategory.CombatAndTools, new Vector3(0.30f, 0.94f, 0.90f)),
        (BuiltInItems.DiamondChestplate, "Diamond Chestplate", ItemCategory.CombatAndTools, new Vector3(0.30f, 0.94f, 0.90f)),
        (BuiltInItems.DiamondLeggings, "Diamond Leggings", ItemCategory.CombatAndTools, new Vector3(0.30f, 0.94f, 0.90f)),
        (BuiltInItems.DiamondBoots, "Diamond Boots", ItemCategory.CombatAndTools, new Vector3(0.30f, 0.94f, 0.90f)),
        (BuiltInItems.RawIron, "Raw Iron", ItemCategory.FoodAndMaterials, new Vector3(0.82f, 0.68f, 0.56f)),
        (BuiltInItems.RawCopper, "Raw Copper", ItemCategory.FoodAndMaterials, new Vector3(0.84f, 0.48f, 0.32f)),
        (BuiltInItems.CopperIngot, "Copper Ingot", ItemCategory.FoodAndMaterials, new Vector3(0.86f, 0.50f, 0.34f)),
        (BuiltInItems.RawGold, "Raw Gold", ItemCategory.FoodAndMaterials, new Vector3(0.96f, 0.80f, 0.24f)),
        (BuiltInItems.Emerald, "Emerald", ItemCategory.FoodAndMaterials, new Vector3(0.22f, 0.88f, 0.44f)),
        (BuiltInItems.LapisLazuli, "Lapis Lazuli", ItemCategory.FoodAndMaterials, new Vector3(0.20f, 0.40f, 0.88f)),
        (BuiltInItems.Redstone, "Redstone Dust", ItemCategory.Redstone, new Vector3(0.88f, 0.14f, 0.16f)),
        (BuiltInItems.Charcoal, "Charcoal", ItemCategory.FoodAndMaterials, new Vector3(0.22f, 0.20f, 0.18f)),
        (BuiltInItems.NetheriteIngot, "Netherite Ingot", ItemCategory.FoodAndMaterials, new Vector3(0.26f, 0.22f, 0.28f)),
        (BuiltInItems.Flint, "Flint", ItemCategory.FoodAndMaterials, new Vector3(0.34f, 0.34f, 0.38f)),
        (BuiltInItems.RawBeef, "Raw Beef", ItemCategory.FoodAndMaterials, new Vector3(0.82f, 0.24f, 0.22f)),
        (BuiltInItems.RawPorkchop, "Raw Porkchop", ItemCategory.FoodAndMaterials, new Vector3(0.92f, 0.52f, 0.50f)),
        (BuiltInItems.CookedPorkchop, "Cooked Porkchop", ItemCategory.FoodAndMaterials, new Vector3(0.84f, 0.62f, 0.44f)),
        (BuiltInItems.RawMutton, "Raw Mutton", ItemCategory.FoodAndMaterials, new Vector3(0.84f, 0.28f, 0.26f)),
        (BuiltInItems.CookedMutton, "Cooked Mutton", ItemCategory.FoodAndMaterials, new Vector3(0.64f, 0.34f, 0.24f)),
        (BuiltInItems.RawChicken, "Raw Chicken", ItemCategory.FoodAndMaterials, new Vector3(0.92f, 0.72f, 0.68f)),
        (BuiltInItems.CookedChicken, "Cooked Chicken", ItemCategory.FoodAndMaterials, new Vector3(0.82f, 0.60f, 0.36f)),
        (BuiltInItems.RottenFlesh, "Rotten Flesh", ItemCategory.FoodAndMaterials, new Vector3(0.56f, 0.32f, 0.22f)),
        (BuiltInItems.Leather, "Leather", ItemCategory.FoodAndMaterials, new Vector3(0.68f, 0.40f, 0.22f)),
        (BuiltInItems.Feather, "Feather", ItemCategory.FoodAndMaterials, new Vector3(0.95f, 0.96f, 0.98f)),
        (BuiltInItems.String, "String", ItemCategory.FoodAndMaterials, new Vector3(0.92f, 0.93f, 0.95f)),
        (BuiltInItems.Bone, "Bone", ItemCategory.FoodAndMaterials, new Vector3(0.92f, 0.90f, 0.84f)),
        (BuiltInItems.Gunpowder, "Gunpowder", ItemCategory.FoodAndMaterials, new Vector3(0.42f, 0.42f, 0.44f)),
        (BuiltInItems.SpiderEye, "Spider Eye", ItemCategory.FoodAndMaterials, new Vector3(0.62f, 0.14f, 0.24f)),

        // v0.6.0-alpha Redstone, Pistons, Bed, Mineral Blocks, Shield, Leather/Gold Armor & Crafting Materials (117+)
        (BuiltInBlocks.RedstoneBlock, "Block of Redstone", ItemCategory.Redstone, new Vector3(0.86f, 0.12f, 0.14f)),
        (BuiltInBlocks.RedstoneTorch, "Redstone Torch", ItemCategory.Redstone, new Vector3(0.94f, 0.22f, 0.18f)),
        (BuiltInBlocks.Lever, "Lever", ItemCategory.Redstone, new Vector3(0.58f, 0.50f, 0.40f)),
        (BuiltInBlocks.StoneButton, "Stone Button", ItemCategory.Redstone, new Vector3(0.56f, 0.56f, 0.58f)),
        (BuiltInBlocks.StonePressurePlate, "Stone Pressure Plate", ItemCategory.Redstone, new Vector3(0.54f, 0.54f, 0.56f)),
        (BuiltInBlocks.RedstoneRepeater, "Redstone Repeater", ItemCategory.Redstone, new Vector3(0.74f, 0.68f, 0.68f)),
        (BuiltInBlocks.RedstoneLamp, "Redstone Lamp", ItemCategory.Redstone, new Vector3(0.64f, 0.44f, 0.24f)),
        (BuiltInBlocks.Piston, "Piston", ItemCategory.Redstone, new Vector3(0.62f, 0.52f, 0.38f)),
        (BuiltInBlocks.StickyPiston, "Sticky Piston", ItemCategory.Redstone, new Vector3(0.48f, 0.72f, 0.36f)),
        (BuiltInBlocks.RedBed, "Red Bed", ItemCategory.FunctionalBlocks, new Vector3(0.82f, 0.18f, 0.20f)),
        (BuiltInBlocks.CopperBlock, "Block of Copper", ItemCategory.BuildingBlocks, new Vector3(0.84f, 0.48f, 0.32f)),
        (BuiltInBlocks.EmeraldBlock, "Block of Emerald", ItemCategory.BuildingBlocks, new Vector3(0.20f, 0.88f, 0.44f)),
        (BuiltInBlocks.LapisBlock, "Block of Lapis Lazuli", ItemCategory.BuildingBlocks, new Vector3(0.18f, 0.36f, 0.84f)),
        (BuiltInItems.Shield, "Shield", ItemCategory.CombatAndTools, new Vector3(0.68f, 0.52f, 0.32f)),
        (BuiltInItems.GoldenAxe, "Golden Axe", ItemCategory.CombatAndTools, new Vector3(0.98f, 0.84f, 0.22f)),
        (BuiltInItems.GoldenShovel, "Golden Shovel", ItemCategory.CombatAndTools, new Vector3(0.98f, 0.84f, 0.22f)),
        (BuiltInItems.GoldenHoe, "Golden Hoe", ItemCategory.CombatAndTools, new Vector3(0.98f, 0.84f, 0.22f)),
        (BuiltInItems.LeatherHelmet, "Leather Cap", ItemCategory.CombatAndTools, new Vector3(0.62f, 0.38f, 0.22f)),
        (BuiltInItems.LeatherChestplate, "Leather Tunic", ItemCategory.CombatAndTools, new Vector3(0.62f, 0.38f, 0.22f)),
        (BuiltInItems.LeatherLeggings, "Leather Pants", ItemCategory.CombatAndTools, new Vector3(0.62f, 0.38f, 0.22f)),
        (BuiltInItems.LeatherBoots, "Leather Boots", ItemCategory.CombatAndTools, new Vector3(0.62f, 0.38f, 0.22f)),
        (BuiltInItems.GoldenHelmet, "Golden Helmet", ItemCategory.CombatAndTools, new Vector3(0.98f, 0.84f, 0.22f)),
        (BuiltInItems.GoldenChestplate, "Golden Chestplate", ItemCategory.CombatAndTools, new Vector3(0.98f, 0.84f, 0.22f)),
        (BuiltInItems.GoldenLeggings, "Golden Leggings", ItemCategory.CombatAndTools, new Vector3(0.98f, 0.84f, 0.22f)),
        (BuiltInItems.GoldenBoots, "Golden Boots", ItemCategory.CombatAndTools, new Vector3(0.98f, 0.84f, 0.22f)),
        (BuiltInItems.Paper, "Paper", ItemCategory.FoodAndMaterials, new Vector3(0.95f, 0.95f, 0.92f)),
        (BuiltInItems.Book, "Book", ItemCategory.FoodAndMaterials, new Vector3(0.58f, 0.36f, 0.20f)),
        (BuiltInItems.SlimeBall, "Slimeball", ItemCategory.FoodAndMaterials, new Vector3(0.46f, 0.84f, 0.38f)),
        (BuiltInItems.Wheat, "Wheat", ItemCategory.FoodAndMaterials, new Vector3(0.86f, 0.76f, 0.32f)),
        (BuiltInItems.SugarCane, "Sugar Cane", ItemCategory.FoodAndMaterials, new Vector3(0.54f, 0.84f, 0.38f)),

        // v0.7.0-alpha Powered Rails, Minecarts, Bows & Arrows (148..151)
        (BuiltInBlocks.PoweredRail, "Powered Rail", ItemCategory.Redstone, new Vector3(0.95f, 0.78f, 0.22f)),
        (BuiltInItems.Minecart, "Minecart", ItemCategory.Redstone, new Vector3(0.76f, 0.78f, 0.82f)),
        (BuiltInItems.Bow, "Bow", ItemCategory.CombatAndTools, new Vector3(0.62f, 0.44f, 0.24f)),
        (BuiltInItems.Arrow, "Arrow", ItemCategory.CombatAndTools, new Vector3(0.82f, 0.82f, 0.85f)),

        // v0.8.0-alpha Comparator, Composter, Trapdoor, Bedrock, Spawner, Geode/Dungeon Blocks, Potions & Spawn Eggs (152+)
        (BuiltInBlocks.RedstoneComparator, "Redstone Comparator", ItemCategory.Redstone, new Vector3(0.76f, 0.70f, 0.70f)),
        (BuiltInBlocks.Composter, "Composter", ItemCategory.FunctionalBlocks, new Vector3(0.62f, 0.45f, 0.26f)),
        (BuiltInBlocks.OakTrapdoor, "Oak Trapdoor", ItemCategory.Redstone, new Vector3(0.70f, 0.54f, 0.32f)),
        (BuiltInBlocks.Bedrock, "Bedrock", ItemCategory.BuildingBlocks, new Vector3(0.28f, 0.28f, 0.30f)),
        (BuiltInBlocks.MonsterSpawner, "Monster Spawner", ItemCategory.FunctionalBlocks, new Vector3(0.24f, 0.32f, 0.40f)),
        (BuiltInBlocks.AmethystBlock, "Block of Amethyst", ItemCategory.NaturalBlocks, new Vector3(0.62f, 0.40f, 0.86f)),
        (BuiltInBlocks.MossyStoneBricks, "Mossy Stone Bricks", ItemCategory.BuildingBlocks, new Vector3(0.42f, 0.54f, 0.40f)),
        (BuiltInBlocks.ChiseledSandstone, "Chiseled Sandstone", ItemCategory.BuildingBlocks, new Vector3(0.86f, 0.80f, 0.58f)),
        (BuiltInItems.Quartz, "Nether Quartz", ItemCategory.FoodAndMaterials, new Vector3(0.94f, 0.92f, 0.88f)),
        (BuiltInItems.BoneMeal, "Bone Meal", ItemCategory.FoodAndMaterials, new Vector3(0.96f, 0.96f, 0.93f)),
        (BuiltInItems.PotionOfSwiftness, "Potion of Swiftness", ItemCategory.FoodAndMaterials, new Vector3(0.48f, 0.72f, 0.94f)),
        (BuiltInItems.PotionOfRegeneration, "Potion of Regeneration", ItemCategory.FoodAndMaterials, new Vector3(0.90f, 0.38f, 0.74f)),
        (BuiltInItems.PotionOfStrength, "Potion of Strength", ItemCategory.FoodAndMaterials, new Vector3(0.74f, 0.18f, 0.18f)),
        (BuiltInItems.PotionOfNightVision, "Potion of Night Vision", ItemCategory.FoodAndMaterials, new Vector3(0.26f, 0.32f, 0.90f)),
        (BuiltInItems.PigSpawnEgg, "Pig Spawn Egg", ItemCategory.FoodAndMaterials, new Vector3(0.94f, 0.65f, 0.64f)),
        (BuiltInItems.SheepSpawnEgg, "Sheep Spawn Egg", ItemCategory.FoodAndMaterials, new Vector3(0.92f, 0.90f, 0.90f)),
        (BuiltInItems.CowSpawnEgg, "Cow Spawn Egg", ItemCategory.FoodAndMaterials, new Vector3(0.42f, 0.34f, 0.25f)),
        (BuiltInItems.ChickenSpawnEgg, "Chicken Spawn Egg", ItemCategory.FoodAndMaterials, new Vector3(0.92f, 0.36f, 0.36f)),
        (BuiltInItems.ZombieSpawnEgg, "Zombie Spawn Egg", ItemCategory.CombatAndTools, new Vector3(0.18f, 0.68f, 0.62f)),
        (BuiltInItems.SkeletonSpawnEgg, "Skeleton Spawn Egg", ItemCategory.CombatAndTools, new Vector3(0.76f, 0.76f, 0.76f)),
        (BuiltInItems.SpiderSpawnEgg, "Spider Spawn Egg", ItemCategory.CombatAndTools, new Vector3(0.32f, 0.24f, 0.22f)),
        (BuiltInItems.CreeperSpawnEgg, "Creeper Spawn Egg", ItemCategory.CombatAndTools, new Vector3(0.12f, 0.72f, 0.12f)),

        // v0.9.0-alpha Doors, Iron Trapdoor, Geode Crystals, Dirt Path, Cherry/Spruce/Badlands/Ice Biomes & Bottles (172+)
        (BuiltInBlocks.OakDoor, "Oak Door", ItemCategory.Redstone, new Vector3(0.70f, 0.54f, 0.33f)),
        (BuiltInBlocks.IronDoor, "Iron Door", ItemCategory.Redstone, new Vector3(0.86f, 0.88f, 0.90f)),
        (BuiltInBlocks.IronTrapdoor, "Iron Trapdoor", ItemCategory.Redstone, new Vector3(0.84f, 0.86f, 0.88f)),
        (BuiltInBlocks.BuddingAmethyst, "Budding Amethyst", ItemCategory.NaturalBlocks, new Vector3(0.68f, 0.44f, 0.92f)),
        (BuiltInBlocks.AmethystCluster, "Amethyst Cluster", ItemCategory.NaturalBlocks, new Vector3(0.78f, 0.56f, 0.98f)),
        (BuiltInBlocks.DirtPath, "Dirt Path", ItemCategory.NaturalBlocks, new Vector3(0.64f, 0.52f, 0.30f)),
        (BuiltInBlocks.CherryLog, "Cherry Log", ItemCategory.NaturalBlocks, new Vector3(0.28f, 0.16f, 0.20f)),
        (BuiltInBlocks.CherryLeaves, "Cherry Leaves", ItemCategory.NaturalBlocks, new Vector3(0.96f, 0.68f, 0.82f)),
        (BuiltInBlocks.SpruceLog, "Spruce Log", ItemCategory.NaturalBlocks, new Vector3(0.26f, 0.18f, 0.11f)),
        (BuiltInBlocks.SpruceLeaves, "Spruce Leaves", ItemCategory.NaturalBlocks, new Vector3(0.20f, 0.38f, 0.24f)),
        (BuiltInBlocks.RedSand, "Red Sand", ItemCategory.NaturalBlocks, new Vector3(0.82f, 0.42f, 0.16f)),
        (BuiltInBlocks.Terracotta, "Terracotta", ItemCategory.NaturalBlocks, new Vector3(0.62f, 0.37f, 0.27f)),
        (BuiltInBlocks.PackedIce, "Packed Ice", ItemCategory.NaturalBlocks, new Vector3(0.56f, 0.74f, 0.96f)),
        (BuiltInItems.FireResistancePotion, "Potion of Fire Resistance", ItemCategory.FoodAndMaterials, new Vector3(0.96f, 0.60f, 0.18f)),
        (BuiltInItems.AmethystShard, "Amethyst Shard", ItemCategory.FoodAndMaterials, new Vector3(0.74f, 0.50f, 0.94f)),
        (BuiltInItems.GlassBottle, "Glass Bottle", ItemCategory.FoodAndMaterials, new Vector3(0.78f, 0.92f, 0.96f)),
        (BuiltInItems.ExperienceBottle, "Bottle o' Enchanting", ItemCategory.FoodAndMaterials, new Vector3(0.68f, 0.96f, 0.34f)),
    ];

    /// <summary>Builds a concise gameplay stats line for hover tooltips.</summary>
    public string BuildTooltipDetail(ResourceKey key)
    {
        if (!_items.TryGet(key, out var def)) return string.Empty;
        if (key == BuiltInItems.Shield) return "Right-Click to Block melee, Arrows & Explosions! (Mainhand or Offhand F)";
        if (def.ArmorDefense > 0) return $"+{def.ArmorDefense} Armor Defense (Equip in Armor Slot or Right-Click)";
        if (def.ToolType == ToolKind.Sword) return $"+{def.AttackDamage:F0} Melee Damage (Jump Crit: {def.AttackDamage * 1.5f:F1})";
        if (def.ToolType != ToolKind.None) return $"{def.ToolType} Tier {def.ToolTier} (Speed x{def.MiningSpeedMultiplier:F1}, +{def.AttackDamage:F1} Dmg)";
        if (def.HeldLightEmission > 0) return $"Emits Light Level {def.HeldLightEmission} (Dynamic Held + Offhand Illumination!)";

        return def.UseAction switch
        {
            ItemUseAction.EatFood => $"Food: +{def.FoodNutrition} Hunger, +{def.FoodSaturation:F1} Sat",
            ItemUseAction.DrinkPotion => "Right-Click: Drink Potion for timed Status Effect!",
            ItemUseAction.SpawnMob => "Right-Click on Block: Spawn 3D Mob (Singleplayer & Multiplayer!)",
            ItemUseAction.ApplyBoneMeal => "Right-Click Dirt to regrow Grass or use in Crafting!",
            ItemUseAction.WaterBucket => "Right-Click: Pour flowing Water source",
            ItemUseAction.EmptyBucket => "Right-Click: Scoop Water source",
            ItemUseAction.IgniteTnt => "Right-Click: Spark Fire or ignite physics TNT",
            ItemUseAction.DrawBow => "Hold Right-Click to draw & release 3D ballistic Arrows!",
            ItemUseAction.PlaceMinecart => "Right-Click on Rail / Powered Rail to place & ride!",
            ItemUseAction.FlattenPath => "Right-Click Grass/Dirt to flatten into 15/16 Dirt Path!",
            ItemUseAction.PlaceBlock => "Placeable Voxel Block (Right-Click / Middle-Click Pick)",
            _ => ResolveFallbackTooltip(key),
        };
    }

    private static string ResolveFallbackTooltip(ResourceKey key)
    {
        if (key == BuiltInItems.Arrow) return "Ammunition for Bow (consumed in Survival)";
        if (key == BuiltInItems.ExperienceBottle) return "Right-Click: Shatter Bottle o' Enchanting for XP!";
        return "Crafting & Smelting Material";
    }
}
