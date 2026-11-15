using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Registry;

namespace OverHorizon.Rendering.Textures;

/// <summary>
/// Maps standalone items, tools, swords, hoes, armor sets, foods, Redstone items, and cutout block-items
/// to their <c>128..218</c> <c>16x16</c> tile slots and <c>23w13a__or__b</c> PNG filenames.
/// </summary>
internal static class ProceduralItemTileManifest
{
    /// <summary>Sprite tile index for unpulled bow (<c>bow</c>).</summary>
    public const ushort TileBow = 218;

    /// <summary>Sprite tile index for bow draw stage 0 (<c>bow_pulling_0</c>).</summary>
    public const ushort TileBowPulling0 = 220;

    /// <summary>Sprite tile index for bow draw stage 1 (<c>bow_pulling_1</c>).</summary>
    public const ushort TileBowPulling1 = 221;

    /// <summary>Sprite tile index for bow draw stage 2 (<c>bow_pulling_2</c>).</summary>
    public const ushort TileBowPulling2 = 222;

    /// <summary>Sprite tile index for isometric 3D Oak Fence inventory icon.</summary>
    public const ushort TileOakFenceItem = 248;

    public static readonly (ushort Tile, byte BaseR, byte BaseG, byte BaseB, byte SpotR, byte SpotG, byte SpotB)[] SpawnEggOverlays =
    [
        (231, 240, 165, 162, 219, 99, 95),   // Pig
        (232, 231, 231, 231, 255, 181, 181), // Sheep
        (233, 68, 54, 38, 161, 161, 161),    // Cow
        (234, 225, 225, 225, 255, 0, 0),     // Chicken
        (235, 0, 175, 175, 121, 156, 101),   // Zombie
        (236, 193, 193, 193, 73, 73, 73),    // Skeleton
        (237, 52, 45, 39, 168, 14, 14),      // Spider
        (238, 13, 167, 11, 0, 0, 0),         // Creeper
    ];

    public static readonly (ushort Tile, byte LiquidR, byte LiquidG, byte LiquidB)[] PotionOverlays =
    [
        (227, 124, 175, 198), // Swiftness
        (228, 248, 36, 35),   // Healing / Regeneration
        (229, 147, 36, 35),   // Strength
        (230, 31, 31, 161),   // Night Vision
        (240, 228, 154, 58),  // Fire Resistance
    ];

    public static readonly (ResourceKey ItemKey, ushort Tile, string PngStem, byte TintR, byte TintG, byte TintB)[] BuiltInItemTiles =
    [
        (BuiltInItems.Stick, 128, "stick", 255, 255, 255),
        (BuiltInItems.Coal, 129, "coal", 255, 255, 255),
        (BuiltInItems.IronIngot, 130, "iron_ingot", 255, 255, 255),
        (BuiltInItems.GoldIngot, 131, "gold_ingot", 255, 255, 255),
        (BuiltInItems.Diamond, 132, "diamond", 255, 255, 255),
        (BuiltInItems.WoodenSword, 133, "wooden_sword", 255, 255, 255),
        (BuiltInItems.StoneSword, 134, "stone_sword", 255, 255, 255),
        (BuiltInItems.GoldenSword, 135, "golden_sword", 255, 255, 255),
        (BuiltInItems.IronSword, 136, "iron_sword", 255, 255, 255),
        (BuiltInItems.DiamondSword, 137, "diamond_sword", 255, 255, 255),
        (BuiltInItems.NetheriteSword, 138, "netherite_sword", 255, 255, 255),
        (BuiltInItems.WoodenPickaxe, 139, "wooden_pickaxe", 255, 255, 255),
        (BuiltInItems.StonePickaxe, 140, "stone_pickaxe", 255, 255, 255),
        (BuiltInItems.IronPickaxe, 141, "iron_pickaxe", 255, 255, 255),
        (BuiltInItems.DiamondPickaxe, 142, "diamond_pickaxe", 255, 255, 255),
        (BuiltInItems.IronAxe, 143, "iron_axe", 255, 255, 255),
        (BuiltInItems.DiamondAxe, 144, "diamond_axe", 255, 255, 255),
        (BuiltInItems.IronShovel, 145, "iron_shovel", 255, 255, 255),
        (BuiltInItems.DiamondShovel, 146, "diamond_shovel", 255, 255, 255),
        (BuiltInItems.Shears, 147, "shears", 255, 255, 255),
        (BuiltInItems.FlintAndSteel, 148, "flint_and_steel", 255, 255, 255),
        (BuiltInItems.Bucket, 149, "bucket", 255, 255, 255),
        (BuiltInItems.WaterBucket, 150, "water_bucket", 255, 255, 255),
        (BuiltInItems.Apple, 151, "apple", 255, 255, 255),
        (BuiltInItems.Bread, 152, "bread", 255, 255, 255),
        (BuiltInItems.CookedBeef, 153, "cooked_beef", 255, 255, 255),
        (BuiltInItems.GoldenApple, 154, "golden_apple", 255, 255, 255),
        (BuiltInItems.Charcoal, 155, "charcoal", 255, 255, 255),
        (BuiltInItems.RawIron, 156, "raw_iron", 255, 255, 255),
        (BuiltInItems.RawGold, 157, "raw_gold", 255, 255, 255),
        (BuiltInItems.RawCopper, 158, "raw_copper", 255, 255, 255),
        (BuiltInItems.CopperIngot, 159, "copper_ingot", 255, 255, 255),
        (BuiltInItems.Emerald, 160, "emerald", 255, 255, 255),
        (BuiltInItems.LapisLazuli, 161, "lapis_lazuli", 255, 255, 255),
        (BuiltInItems.Redstone, 162, "redstone", 255, 255, 255),
        (BuiltInItems.NetheriteIngot, 163, "netherite_ingot", 255, 255, 255),
        (BuiltInItems.Flint, 164, "flint", 255, 255, 255),
        (BuiltInItems.Bone, 165, "bone", 255, 255, 255),
        (BuiltInItems.String, 166, "string", 255, 255, 255),
        (BuiltInItems.Gunpowder, 167, "gunpowder", 255, 255, 255),
        (BuiltInItems.SpiderEye, 168, "spider_eye", 255, 255, 255),
        (BuiltInItems.Leather, 169, "leather", 255, 255, 255),
        (BuiltInItems.Feather, 170, "feather", 255, 255, 255),
        (BuiltInItems.GoldenPickaxe, 171, "golden_pickaxe", 255, 255, 255),
        (BuiltInItems.NetheritePickaxe, 172, "netherite_pickaxe", 255, 255, 255),
        (BuiltInItems.WoodenAxe, 173, "wooden_axe", 255, 255, 255),
        (BuiltInItems.StoneAxe, 174, "stone_axe", 255, 255, 255),
        (BuiltInItems.WoodenShovel, 175, "wooden_shovel", 255, 255, 255),
        (BuiltInItems.StoneShovel, 176, "stone_shovel", 255, 255, 255),
        (BuiltInItems.WoodenHoe, 177, "wooden_hoe", 255, 255, 255),
        (BuiltInItems.StoneHoe, 178, "stone_hoe", 255, 255, 255),
        (BuiltInItems.IronHoe, 179, "iron_hoe", 255, 255, 255),
        (BuiltInItems.DiamondHoe, 180, "diamond_hoe", 255, 255, 255),
        (BuiltInItems.IronHelmet, 181, "iron_helmet", 255, 255, 255),
        (BuiltInItems.IronChestplate, 182, "iron_chestplate", 255, 255, 255),
        (BuiltInItems.IronLeggings, 183, "iron_leggings", 255, 255, 255),
        (BuiltInItems.IronBoots, 184, "iron_boots", 255, 255, 255),
        (BuiltInItems.DiamondHelmet, 185, "diamond_helmet", 255, 255, 255),
        (BuiltInItems.DiamondChestplate, 186, "diamond_chestplate", 255, 255, 255),
        (BuiltInItems.DiamondLeggings, 187, "diamond_leggings", 255, 255, 255),
        (BuiltInItems.DiamondBoots, 188, "diamond_boots", 255, 255, 255),
        (BuiltInItems.RawPorkchop, 189, "porkchop", 255, 255, 255),
        (BuiltInItems.CookedPorkchop, 190, "cooked_porkchop", 255, 255, 255),
        (BuiltInItems.RawBeef, 191, "beef", 255, 255, 255),
        (BuiltInItems.RawChicken, 192, "chicken", 255, 255, 255),
        (BuiltInItems.CookedChicken, 193, "cooked_chicken", 255, 255, 255),
        (BuiltInItems.RawMutton, 194, "mutton", 255, 255, 255),
        (BuiltInItems.CookedMutton, 195, "cooked_mutton", 255, 255, 255),
        (BuiltInItems.RottenFlesh, 196, "rotten_flesh", 255, 255, 255),
        (BuiltInBlocks.Lantern, 197, "lantern", 255, 255, 255),
        (BuiltInItems.Paper, 198, "paper", 255, 255, 255),
        (BuiltInItems.Book, 199, "book", 255, 255, 255),
        (BuiltInItems.SlimeBall, 200, "slime_ball", 255, 255, 255),
        (BuiltInItems.Wheat, 201, "wheat", 255, 255, 255),
        (BuiltInItems.WheatSeeds, 220, "wheat_seeds", 255, 255, 255),
        (BuiltInItems.SugarCane, 202, "sugar_cane", 255, 255, 255),
        (BuiltInBlocks.RedstoneRepeater, 203, "repeater", 255, 255, 255),
        (BuiltInItems.GoldenAxe, 204, "golden_axe", 255, 255, 255),
        (BuiltInItems.GoldenShovel, 205, "golden_shovel", 255, 255, 255),
        (BuiltInItems.GoldenHoe, 206, "golden_hoe", 255, 255, 255),
        (BuiltInItems.LeatherHelmet, 207, "leather_helmet", 160, 101, 64),
        (BuiltInItems.LeatherChestplate, 208, "leather_chestplate", 160, 101, 64),
        (BuiltInItems.LeatherLeggings, 209, "leather_leggings", 160, 101, 64),
        (BuiltInItems.LeatherBoots, 210, "leather_boots", 160, 101, 64),
        (BuiltInItems.GoldenHelmet, 211, "golden_helmet", 255, 255, 255),
        (BuiltInItems.GoldenChestplate, 212, "golden_chestplate", 255, 255, 255),
        (BuiltInItems.GoldenLeggings, 213, "golden_leggings", 255, 255, 255),
        (BuiltInItems.GoldenBoots, 214, "golden_boots", 255, 255, 255),
        (BuiltInItems.Shield, 215, "shield", 255, 255, 255),
        (BuiltInBlocks.RedBed, 216, "red_bed", 255, 255, 255),
        (BuiltInItems.Minecart, 217, "minecart", 255, 255, 255),
        (BuiltInItems.Bow, 218, "bow", 255, 255, 255),
        (BuiltInItems.Arrow, 219, "arrow", 255, 255, 255),
        (BuiltInBlocks.PoweredRail, 223, "powered_rail", 255, 255, 255),
        (BuiltInBlocks.RedstoneComparator, 224, "comparator", 255, 255, 255),
        (BuiltInItems.Quartz, 225, "quartz", 255, 255, 255),
        (BuiltInItems.BoneMeal, 226, "bone_meal", 255, 255, 255),
        (BuiltInItems.PotionOfSwiftness, 227, "potion", 124, 185, 240),
        (BuiltInItems.PotionOfRegeneration, 228, "potion", 228, 98, 186),
        (BuiltInItems.PotionOfStrength, 229, "potion", 184, 42, 40),
        (BuiltInItems.PotionOfNightVision, 230, "potion", 68, 84, 232),
        (BuiltInItems.PigSpawnEgg, 231, "spawn_egg", 240, 165, 162),
        (BuiltInItems.SheepSpawnEgg, 232, "spawn_egg", 231, 231, 231),
        (BuiltInItems.CowSpawnEgg, 233, "spawn_egg", 92, 74, 54),
        (BuiltInItems.ChickenSpawnEgg, 234, "spawn_egg", 225, 225, 225),
        (BuiltInItems.ZombieSpawnEgg, 235, "spawn_egg", 42, 175, 165),
        (BuiltInItems.SkeletonSpawnEgg, 236, "spawn_egg", 193, 193, 193),
        (BuiltInItems.SpiderSpawnEgg, 237, "spawn_egg", 68, 56, 48),
        (BuiltInItems.CreeperSpawnEgg, 238, "spawn_egg", 22, 172, 18),
        (BuiltInBlocks.OakTrapdoor, 239, "oak_trapdoor", 255, 255, 255),
        (BuiltInItems.FireResistancePotion, 240, "potion", 228, 154, 58),
        (BuiltInItems.GlassBottle, 241, "glass_bottle", 255, 255, 255),
        (BuiltInItems.ExperienceBottle, 242, "experience_bottle", 255, 255, 255),
        (BuiltInItems.AmethystShard, 243, "amethyst_shard", 255, 255, 255),
        (BuiltInBlocks.OakDoor, 244, "oak_door", 255, 255, 255),
        (BuiltInBlocks.IronDoor, 245, "iron_door", 255, 255, 255),
        (BuiltInBlocks.IronTrapdoor, 246, "iron_trapdoor", 255, 255, 255),
        (BuiltInBlocks.AmethystCluster, 247, "amethyst_cluster", 255, 255, 255),
    ];
}
