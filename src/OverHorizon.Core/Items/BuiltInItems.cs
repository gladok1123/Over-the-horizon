using OverHorizon.Core.Blocks;
using OverHorizon.Core.Registry;

namespace OverHorizon.Core.Items;

/// <summary>
/// Built-in item definitions covering all blocks plus tools, weapons, armor sets, shields, Redstone items, buckets, foods, and materials.
/// </summary>
public static partial class BuiltInItems
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

    /// <summary>Key for <c>overhorizon:redstone</c> (places <see cref="BuiltInBlocks.RedstoneWire"/>).</summary>
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

    /// <summary>Key for <c>overhorizon:paper</c>.</summary>
    public static readonly ResourceKey Paper = ResourceKey.OverHorizon("paper");

    /// <summary>Key for <c>overhorizon:book</c>.</summary>
    public static readonly ResourceKey Book = ResourceKey.OverHorizon("book");

    /// <summary>Key for <c>overhorizon:slime_ball</c>.</summary>
    public static readonly ResourceKey SlimeBall = ResourceKey.OverHorizon("slime_ball");

    /// <summary>Key for <c>overhorizon:wheat</c>.</summary>
    public static readonly ResourceKey Wheat = ResourceKey.OverHorizon("wheat");

    /// <summary>Key for <c>overhorizon:sugar_cane</c>.</summary>
    public static readonly ResourceKey SugarCane = ResourceKey.OverHorizon("sugar_cane");

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

    /// <summary>Key for <c>overhorizon:golden_axe</c>.</summary>
    public static readonly ResourceKey GoldenAxe = ResourceKey.OverHorizon("golden_axe");

    /// <summary>Key for <c>overhorizon:diamond_axe</c>.</summary>
    public static readonly ResourceKey DiamondAxe = ResourceKey.OverHorizon("diamond_axe");

    /// <summary>Key for <c>overhorizon:wooden_shovel</c>.</summary>
    public static readonly ResourceKey WoodenShovel = ResourceKey.OverHorizon("wooden_shovel");

    /// <summary>Key for <c>overhorizon:stone_shovel</c>.</summary>
    public static readonly ResourceKey StoneShovel = ResourceKey.OverHorizon("stone_shovel");

    /// <summary>Key for <c>overhorizon:iron_shovel</c>.</summary>
    public static readonly ResourceKey IronShovel = ResourceKey.OverHorizon("iron_shovel");

    /// <summary>Key for <c>overhorizon:golden_shovel</c>.</summary>
    public static readonly ResourceKey GoldenShovel = ResourceKey.OverHorizon("golden_shovel");

    /// <summary>Key for <c>overhorizon:diamond_shovel</c>.</summary>
    public static readonly ResourceKey DiamondShovel = ResourceKey.OverHorizon("diamond_shovel");

    /// <summary>Key for <c>overhorizon:wooden_hoe</c>.</summary>
    public static readonly ResourceKey WoodenHoe = ResourceKey.OverHorizon("wooden_hoe");

    /// <summary>Key for <c>overhorizon:stone_hoe</c>.</summary>
    public static readonly ResourceKey StoneHoe = ResourceKey.OverHorizon("stone_hoe");

    /// <summary>Key for <c>overhorizon:iron_hoe</c>.</summary>
    public static readonly ResourceKey IronHoe = ResourceKey.OverHorizon("iron_hoe");

    /// <summary>Key for <c>overhorizon:golden_hoe</c>.</summary>
    public static readonly ResourceKey GoldenHoe = ResourceKey.OverHorizon("golden_hoe");

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

    /// <summary>Key for <c>overhorizon:shield</c>.</summary>
    public static readonly ResourceKey Shield = ResourceKey.OverHorizon("shield");

    /// <summary>Key for <c>overhorizon:leather_helmet</c>.</summary>
    public static readonly ResourceKey LeatherHelmet = ResourceKey.OverHorizon("leather_helmet");

    /// <summary>Key for <c>overhorizon:leather_chestplate</c>.</summary>
    public static readonly ResourceKey LeatherChestplate = ResourceKey.OverHorizon("leather_chestplate");

    /// <summary>Key for <c>overhorizon:leather_leggings</c>.</summary>
    public static readonly ResourceKey LeatherLeggings = ResourceKey.OverHorizon("leather_leggings");

    /// <summary>Key for <c>overhorizon:leather_boots</c>.</summary>
    public static readonly ResourceKey LeatherBoots = ResourceKey.OverHorizon("leather_boots");

    /// <summary>Key for <c>overhorizon:iron_helmet</c>.</summary>
    public static readonly ResourceKey IronHelmet = ResourceKey.OverHorizon("iron_helmet");

    /// <summary>Key for <c>overhorizon:iron_chestplate</c>.</summary>
    public static readonly ResourceKey IronChestplate = ResourceKey.OverHorizon("iron_chestplate");

    /// <summary>Key for <c>overhorizon:iron_leggings</c>.</summary>
    public static readonly ResourceKey IronLeggings = ResourceKey.OverHorizon("iron_leggings");

    /// <summary>Key for <c>overhorizon:iron_boots</c>.</summary>
    public static readonly ResourceKey IronBoots = ResourceKey.OverHorizon("iron_boots");

    /// <summary>Key for <c>overhorizon:golden_helmet</c>.</summary>
    public static readonly ResourceKey GoldenHelmet = ResourceKey.OverHorizon("golden_helmet");

    /// <summary>Key for <c>overhorizon:golden_chestplate</c>.</summary>
    public static readonly ResourceKey GoldenChestplate = ResourceKey.OverHorizon("golden_chestplate");

    /// <summary>Key for <c>overhorizon:golden_leggings</c>.</summary>
    public static readonly ResourceKey GoldenLeggings = ResourceKey.OverHorizon("golden_leggings");

    /// <summary>Key for <c>overhorizon:golden_boots</c>.</summary>
    public static readonly ResourceKey GoldenBoots = ResourceKey.OverHorizon("golden_boots");

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

    /// <summary>Key for <c>overhorizon:minecart</c> (rideable rail vehicle).</summary>
    public static readonly ResourceKey Minecart = ResourceKey.OverHorizon("minecart");

    /// <summary>Key for <c>overhorizon:bow</c> (ranged weapon charging ballistic arrows).</summary>
    public static readonly ResourceKey Bow = ResourceKey.OverHorizon("bow");

    /// <summary>Key for <c>overhorizon:arrow</c> (ammunition for <see cref="Bow"/>).</summary>
    public static readonly ResourceKey Arrow = ResourceKey.OverHorizon("arrow");

    /// <summary>Key for <c>overhorizon:quartz</c> (crystalline Quartz used in Redstone Comparators).</summary>
    public static readonly ResourceKey Quartz = ResourceKey.OverHorizon("quartz");

    /// <summary>Key for <c>overhorizon:bone_meal</c> (organic fertilizer produced from Bones or a Composter).</summary>
    public static readonly ResourceKey BoneMeal = ResourceKey.OverHorizon("bone_meal");

    /// <summary>Key for <c>overhorizon:swiftness_potion</c> (grants Speed I movement boost).</summary>
    public static readonly ResourceKey SwiftnessPotion = ResourceKey.OverHorizon("swiftness_potion");

    /// <summary>Key for <c>overhorizon:healing_potion</c> (grants Instant Health + Regeneration).</summary>
    public static readonly ResourceKey HealingPotion = ResourceKey.OverHorizon("healing_potion");

    /// <summary>Key for <c>overhorizon:night_vision_potion</c> (grants cave Night Vision).</summary>
    public static readonly ResourceKey NightVisionPotion = ResourceKey.OverHorizon("night_vision_potion");

    /// <summary>Key for <c>overhorizon:fire_resistance_potion</c> (grants Fire Resistance immunity).</summary>
    public static readonly ResourceKey FireResistancePotion = ResourceKey.OverHorizon("fire_resistance_potion");

    /// <summary>Key for <c>overhorizon:strength_potion</c> (grants melee Strength I boost).</summary>
    public static readonly ResourceKey StrengthPotion = ResourceKey.OverHorizon("strength_potion");

    /// <summary>Alias for <see cref="SwiftnessPotion"/>.</summary>
    public static readonly ResourceKey PotionOfSwiftness = SwiftnessPotion;

    /// <summary>Alias for <see cref="HealingPotion"/>.</summary>
    public static readonly ResourceKey PotionOfRegeneration = HealingPotion;

    /// <summary>Alias for <see cref="StrengthPotion"/>.</summary>
    public static readonly ResourceKey PotionOfStrength = StrengthPotion;

    /// <summary>Alias for <see cref="NightVisionPotion"/>.</summary>
    public static readonly ResourceKey PotionOfNightVision = NightVisionPotion;

    /// <summary>Key for <c>overhorizon:pig_spawn_egg</c>.</summary>
    public static readonly ResourceKey PigSpawnEgg = ResourceKey.OverHorizon("pig_spawn_egg");

    /// <summary>Key for <c>overhorizon:cow_spawn_egg</c>.</summary>
    public static readonly ResourceKey CowSpawnEgg = ResourceKey.OverHorizon("cow_spawn_egg");

    /// <summary>Key for <c>overhorizon:sheep_spawn_egg</c>.</summary>
    public static readonly ResourceKey SheepSpawnEgg = ResourceKey.OverHorizon("sheep_spawn_egg");

    /// <summary>Key for <c>overhorizon:chicken_spawn_egg</c>.</summary>
    public static readonly ResourceKey ChickenSpawnEgg = ResourceKey.OverHorizon("chicken_spawn_egg");

    /// <summary>Key for <c>overhorizon:zombie_spawn_egg</c>.</summary>
    public static readonly ResourceKey ZombieSpawnEgg = ResourceKey.OverHorizon("zombie_spawn_egg");

    /// <summary>Key for <c>overhorizon:skeleton_spawn_egg</c>.</summary>
    public static readonly ResourceKey SkeletonSpawnEgg = ResourceKey.OverHorizon("skeleton_spawn_egg");

    /// <summary>Key for <c>overhorizon:creeper_spawn_egg</c>.</summary>
    public static readonly ResourceKey CreeperSpawnEgg = ResourceKey.OverHorizon("creeper_spawn_egg");

    /// <summary>Key for <c>overhorizon:spider_spawn_egg</c>.</summary>
    public static readonly ResourceKey SpiderSpawnEgg = ResourceKey.OverHorizon("spider_spawn_egg");

    /// <summary>Key for <c>overhorizon:amethyst_shard</c>.</summary>
    public static readonly ResourceKey AmethystShard = ResourceKey.OverHorizon("amethyst_shard");

    /// <summary>Key for <c>overhorizon:glass_bottle</c>.</summary>
    public static readonly ResourceKey GlassBottle = ResourceKey.OverHorizon("glass_bottle");

    /// <summary>Key for <c>overhorizon:experience_bottle</c>.</summary>
    public static readonly ResourceKey ExperienceBottle = ResourceKey.OverHorizon("experience_bottle");

    /// <summary>Attempts to map a Spawn Egg item key to its zero-based mob archetype index (<c>0..7</c>).</summary>
    /// <param name="key">Candidate item resource key.</param>
    /// <param name="mobTypeByte">Resolved mob archetype index when returning <see langword="true"/>.</param>
    public static bool TryGetSpawnEggMobType(ResourceKey key, out byte mobTypeByte)
    {
        if (key == PigSpawnEgg)
        {
            mobTypeByte = 0;
            return true;
        }

        if (key == CowSpawnEgg)
        {
            mobTypeByte = 1;
            return true;
        }

        if (key == SheepSpawnEgg)
        {
            mobTypeByte = 2;
            return true;
        }

        if (key == ChickenSpawnEgg)
        {
            mobTypeByte = 3;
            return true;
        }

        if (key == ZombieSpawnEgg)
        {
            mobTypeByte = 4;
            return true;
        }

        if (key == SkeletonSpawnEgg)
        {
            mobTypeByte = 5;
            return true;
        }

        if (key == SpiderSpawnEgg)
        {
            mobTypeByte = 6;
            return true;
        }

        if (key == CreeperSpawnEgg)
        {
            mobTypeByte = 7;
            return true;
        }

        mobTypeByte = 0;
        return false;
    }

    /// <summary>Maps a zero-based mob archetype index (<c>0..7</c>) back to its Spawn Egg item key.</summary>
    /// <param name="mobTypeByte">Zero-based mob archetype index.</param>
    public static ResourceKey GetSpawnEggForMobType(byte mobTypeByte) => mobTypeByte switch
    {
        0 => PigSpawnEgg,
        1 => CowSpawnEgg,
        2 => SheepSpawnEgg,
        3 => ChickenSpawnEgg,
        4 => ZombieSpawnEgg,
        5 => SkeletonSpawnEgg,
        6 => SpiderSpawnEgg,
        _ => CreeperSpawnEgg,
    };
}
