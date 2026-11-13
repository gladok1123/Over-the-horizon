using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Registry;

namespace OverHorizon.Gameplay.Crafting;

/// <summary>
/// Built-in clean-room shaped and shapeless crafting recipes for 2x2 inventory and 3x3 crafting table grids,
/// including Redstone &amp; Pistons, Beds, Books/Bookshelves, Shields, full Tool/Armor sets, Mineral blocks, and Foods.
/// </summary>
public static class BuiltInRecipes
{
    /// <summary>Recipe key: <c>1 oak_log -&gt; 4 oak_planks</c> (shapeless).</summary>
    public static readonly ResourceKey OakPlanksFromLog = ResourceKey.OverHorizon("oak_planks_from_log");

    /// <summary>Recipe key: <c>2 oak_planks (1x2) -&gt; 4 stick</c> (shaped).</summary>
    public static readonly ResourceKey SticksFromPlanks = ResourceKey.OverHorizon("sticks_from_planks");

    /// <summary>Recipe key: <c>4 oak_planks (2x2) -&gt; 1 crafting_table</c> (shaped).</summary>
    public static readonly ResourceKey CraftingTableFromPlanks = ResourceKey.OverHorizon("crafting_table");

    /// <summary>Recipe key: <c>3 oak_planks + 2 stick (3x3) -&gt; 1 wooden_pickaxe</c> (shaped).</summary>
    public static readonly ResourceKey WoodenPickaxe = ResourceKey.OverHorizon("wooden_pickaxe");

    /// <summary>
    /// Registers all built-in 2x2 and 3x3 crafting recipes into an unfrozen <paramref name="registry"/>.
    /// </summary>
    public static void PopulateBuiltIns(MappedRegistry<ICraftingRecipe> registry, IRegistry<ItemDefinition> items)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(items);

        ushort Id(ResourceKey key) => (ushort)items.GetId(key);

        var oakLog = Id(BuiltInBlocks.OakLog);
        var birchLog = Id(BuiltInBlocks.BirchLog);
        var oakPlanks = Id(BuiltInBlocks.OakPlanks);
        var birchPlanks = Id(BuiltInBlocks.BirchPlanks);
        var stick = Id(BuiltInItems.Stick);
        var cobble = Id(BuiltInBlocks.Cobblestone);
        var cobbledDeep = Id(BuiltInBlocks.CobbledDeepslate);
        var coal = Id(BuiltInItems.Coal);
        var charcoal = Id(BuiltInItems.Charcoal);
        var iron = Id(BuiltInItems.IronIngot);
        var gold = Id(BuiltInItems.GoldIngot);
        var copper = Id(BuiltInItems.CopperIngot);
        var lapis = Id(BuiltInItems.LapisLazuli);
        var emerald = Id(BuiltInItems.Emerald);
        var diamond = Id(BuiltInItems.Diamond);
        var redstone = Id(BuiltInItems.Redstone);
        var slime = Id(BuiltInItems.SlimeBall);
        var leather = Id(BuiltInItems.Leather);
        var paper = Id(BuiltInItems.Paper);
        var book = Id(BuiltInItems.Book);
        var wheat = Id(BuiltInItems.Wheat);
        var sugarCane = Id(BuiltInItems.SugarCane);
        var flint = Id(BuiltInItems.Flint);
        var str = Id(BuiltInItems.String);
        var gunpowder = Id(BuiltInItems.Gunpowder);
        var sand = Id(BuiltInBlocks.Sand);
        var stone = Id(BuiltInBlocks.Stone);
        var glowstone = Id(BuiltInBlocks.Glowstone);
        var torch = Id(BuiltInBlocks.Torch);
        var rsTorch = Id(BuiltInBlocks.RedstoneTorch);
        var piston = Id(BuiltInBlocks.Piston);
        var whiteWool = Id(BuiltInBlocks.WhiteWool);
        var redWool = Id(BuiltInBlocks.CrimsonWool);
        var bed = Id(BuiltInBlocks.RedBed);

        // 1. Logs -> Planks & Planks -> Sticks / Crafting Tables (2x2 & 3x3)
        AddShapeless(registry, OakPlanksFromLog, [oakLog], oakPlanks, 4);
        AddShapeless(registry, "birch_planks_from_log", [birchLog], birchPlanks, 4);

        AddShaped(registry, SticksFromPlanks, 1, 2, [oakPlanks, oakPlanks], stick, 4);
        AddShaped(registry, "sticks_from_birch", 1, 2, [birchPlanks, birchPlanks], stick, 4);
        AddShaped(registry, "sticks_mixed_a", 1, 2, [oakPlanks, birchPlanks], stick, 4);
        AddShaped(registry, "sticks_mixed_b", 1, 2, [birchPlanks, oakPlanks], stick, 4);

        AddShaped(registry, CraftingTableFromPlanks, 2, 2, [oakPlanks, oakPlanks, oakPlanks, oakPlanks], Id(BuiltInBlocks.CraftingTable), 1);
        AddShaped(registry, "crafting_table_birch", 2, 2, [birchPlanks, birchPlanks, birchPlanks, birchPlanks], Id(BuiltInBlocks.CraftingTable), 1);

        // 2. Torches & Lanterns
        AddShaped(registry, "torch_from_coal", 1, 2, [coal, stick], torch, 4);
        AddShaped(registry, "torch_from_charcoal", 1, 2, [charcoal, stick], torch, 4);
        AddShaped(registry, "lantern", 3, 3, [iron, iron, iron, iron, torch, iron, iron, iron, iron], Id(BuiltInBlocks.Lantern), 1);

        // 3. Furnace, Chest, Barrel, Ladder, Rail, Fence, Bookshelf, Beds (3x3)
        AddShaped(registry, "furnace", 3, 3, [cobble, cobble, cobble, cobble, 0, cobble, cobble, cobble, cobble], Id(BuiltInBlocks.Furnace), 1);
        AddShaped(registry, "furnace_deepslate", 3, 3, [cobbledDeep, cobbledDeep, cobbledDeep, cobbledDeep, 0, cobbledDeep, cobbledDeep, cobbledDeep, cobbledDeep], Id(BuiltInBlocks.Furnace), 1);
        AddShaped(registry, "chest_oak", 3, 3, [oakPlanks, oakPlanks, oakPlanks, oakPlanks, 0, oakPlanks, oakPlanks, oakPlanks, oakPlanks], Id(BuiltInBlocks.Chest), 1);
        AddShaped(registry, "chest_birch", 3, 3, [birchPlanks, birchPlanks, birchPlanks, birchPlanks, 0, birchPlanks, birchPlanks, birchPlanks, birchPlanks], Id(BuiltInBlocks.Chest), 1);
        AddShaped(registry, "barrel", 3, 3, [oakPlanks, oakLog, oakPlanks, oakPlanks, 0, oakPlanks, oakPlanks, oakLog, oakPlanks], Id(BuiltInBlocks.Barrel), 1);
        AddShaped(registry, "ladder", 3, 3, [stick, 0, stick, stick, stick, stick, stick, 0, stick], Id(BuiltInBlocks.Ladder), 3);
        AddShaped(registry, "rail", 3, 3, [iron, 0, iron, iron, stick, iron, iron, 0, iron], Id(BuiltInBlocks.Rail), 16);
        AddShaped(registry, "oak_fence", 3, 2, [oakPlanks, stick, oakPlanks, oakPlanks, stick, oakPlanks], Id(BuiltInBlocks.OakFence), 3);
        AddShaped(registry, "birch_fence", 3, 2, [birchPlanks, stick, birchPlanks, birchPlanks, stick, birchPlanks], Id(BuiltInBlocks.OakFence), 3);

        // Paper, Books, Bookshelf & Beds
        AddShaped(registry, "paper_from_cane", 3, 1, [sugarCane, sugarCane, sugarCane], paper, 3);
        AddShapeless(registry, "book", [paper, paper, paper, leather], book, 1);
        AddShaped(registry, "bookshelf_oak", 3, 3, [oakPlanks, oakPlanks, oakPlanks, book, book, book, oakPlanks, oakPlanks, oakPlanks], Id(BuiltInBlocks.Bookshelf), 1);
        AddShaped(registry, "bookshelf_birch", 3, 3, [birchPlanks, birchPlanks, birchPlanks, book, book, book, birchPlanks, birchPlanks, birchPlanks], Id(BuiltInBlocks.Bookshelf), 1);
        AddShaped(registry, "bed_white_oak", 3, 2, [whiteWool, whiteWool, whiteWool, oakPlanks, oakPlanks, oakPlanks], bed, 1);
        AddShaped(registry, "bed_red_oak", 3, 2, [redWool, redWool, redWool, oakPlanks, oakPlanks, oakPlanks], bed, 1);
        AddShaped(registry, "bed_white_birch", 3, 2, [whiteWool, whiteWool, whiteWool, birchPlanks, birchPlanks, birchPlanks], bed, 1);

        // 4. Redstone Components & Pistons (2x2 & 3x3)
        AddShaped(registry, "redstone_torch", 1, 2, [redstone, stick], rsTorch, 1);
        AddShaped(registry, "lever", 1, 2, [stick, cobble], Id(BuiltInBlocks.Lever), 1);
        AddShapeless(registry, "stone_button", [stone], Id(BuiltInBlocks.StoneButton), 1);
        AddShaped(registry, "stone_pressure_plate", 2, 1, [stone, stone], Id(BuiltInBlocks.StonePressurePlate), 1);
        AddShaped(registry, "redstone_repeater", 3, 2, [rsTorch, redstone, rsTorch, stone, stone, stone], Id(BuiltInBlocks.RedstoneRepeater), 1);
        AddShaped(registry, "redstone_lamp", 3, 3, [0, redstone, 0, redstone, glowstone, redstone, 0, redstone, 0], Id(BuiltInBlocks.RedstoneLamp), 1);
        AddShaped(registry, "piston_oak", 3, 3, [oakPlanks, oakPlanks, oakPlanks, cobble, iron, cobble, cobble, redstone, cobble], piston, 1);
        AddShaped(registry, "piston_birch", 3, 3, [birchPlanks, birchPlanks, birchPlanks, cobble, iron, cobble, cobble, redstone, cobble], piston, 1);
        AddShaped(registry, "sticky_piston", 1, 2, [slime, piston], Id(BuiltInBlocks.StickyPiston), 1);
        AddShapeless(registry, "sticky_piston_shapeless", [slime, piston], Id(BuiltInBlocks.StickyPiston), 1);

        // 5. Full Tool & Weapon Tiers (Wood, Birch-Wood, Stone, Deepslate, Iron, Gold, Diamond)
        RegisterToolTier(registry, "wooden", oakPlanks, stick, Id(BuiltInItems.WoodenSword), Id(BuiltInItems.WoodenPickaxe), Id(BuiltInItems.WoodenAxe), Id(BuiltInItems.WoodenShovel), Id(BuiltInItems.WoodenHoe), pickaxeKeyOverride: WoodenPickaxe);
        RegisterToolTier(registry, "birch_wooden", birchPlanks, stick, Id(BuiltInItems.WoodenSword), Id(BuiltInItems.WoodenPickaxe), Id(BuiltInItems.WoodenAxe), Id(BuiltInItems.WoodenShovel), Id(BuiltInItems.WoodenHoe));
        RegisterToolTier(registry, "stone", cobble, stick, Id(BuiltInItems.StoneSword), Id(BuiltInItems.StonePickaxe), Id(BuiltInItems.StoneAxe), Id(BuiltInItems.StoneShovel), Id(BuiltInItems.StoneHoe));
        RegisterToolTier(registry, "deepslate", cobbledDeep, stick, Id(BuiltInItems.StoneSword), Id(BuiltInItems.StonePickaxe), Id(BuiltInItems.StoneAxe), Id(BuiltInItems.StoneShovel), Id(BuiltInItems.StoneHoe));
        RegisterToolTier(registry, "iron", iron, stick, Id(BuiltInItems.IronSword), Id(BuiltInItems.IronPickaxe), Id(BuiltInItems.IronAxe), Id(BuiltInItems.IronShovel), Id(BuiltInItems.IronHoe));
        RegisterToolTier(registry, "golden", gold, stick, Id(BuiltInItems.GoldenSword), Id(BuiltInItems.GoldenPickaxe), Id(BuiltInItems.GoldenAxe), Id(BuiltInItems.GoldenShovel), Id(BuiltInItems.GoldenHoe));
        RegisterToolTier(registry, "diamond", diamond, stick, Id(BuiltInItems.DiamondSword), Id(BuiltInItems.DiamondPickaxe), Id(BuiltInItems.DiamondAxe), Id(BuiltInItems.DiamondShovel), Id(BuiltInItems.DiamondHoe));

        // 6. Utility Tools & Shield: Shears, Flint & Steel, Bucket, Shield
        AddShaped(registry, "shears", 2, 2, [0, iron, iron, 0], Id(BuiltInItems.Shears), 1);
        AddShapeless(registry, "flint_and_steel", [iron, flint], Id(BuiltInItems.FlintAndSteel), 1);
        AddShaped(registry, "bucket", 3, 2, [iron, 0, iron, 0, iron, 0], Id(BuiltInItems.Bucket), 1);
        AddShaped(registry, "shield_oak", 3, 3, [oakPlanks, iron, oakPlanks, oakPlanks, oakPlanks, oakPlanks, 0, oakPlanks, 0], Id(BuiltInItems.Shield), 1);
        AddShaped(registry, "shield_birch", 3, 3, [birchPlanks, iron, birchPlanks, birchPlanks, birchPlanks, birchPlanks, 0, birchPlanks, 0], Id(BuiltInItems.Shield), 1);

        // 7. Complete Armor Sets (Leather, Iron, Gold, Diamond)
        RegisterArmorSet(registry, "leather", leather, Id(BuiltInItems.LeatherHelmet), Id(BuiltInItems.LeatherChestplate), Id(BuiltInItems.LeatherLeggings), Id(BuiltInItems.LeatherBoots));
        RegisterArmorSet(registry, "iron", iron, Id(BuiltInItems.IronHelmet), Id(BuiltInItems.IronChestplate), Id(BuiltInItems.IronLeggings), Id(BuiltInItems.IronBoots));
        RegisterArmorSet(registry, "golden", gold, Id(BuiltInItems.GoldenHelmet), Id(BuiltInItems.GoldenChestplate), Id(BuiltInItems.GoldenLeggings), Id(BuiltInItems.GoldenBoots));
        RegisterArmorSet(registry, "diamond", diamond, Id(BuiltInItems.DiamondHelmet), Id(BuiltInItems.DiamondChestplate), Id(BuiltInItems.DiamondLeggings), Id(BuiltInItems.DiamondBoots));

        // 8. Mineral Blocks, Building Blocks, Wool, TNT & Food
        RegisterMineralBlockPair(registry, "coal", coal, Id(BuiltInBlocks.CoalBlock));
        RegisterMineralBlockPair(registry, "copper", copper, Id(BuiltInBlocks.CopperBlock));
        RegisterMineralBlockPair(registry, "iron", iron, Id(BuiltInBlocks.IronBlock));
        RegisterMineralBlockPair(registry, "gold", gold, Id(BuiltInBlocks.GoldBlock));
        RegisterMineralBlockPair(registry, "lapis", lapis, Id(BuiltInBlocks.LapisBlock));
        RegisterMineralBlockPair(registry, "redstone", redstone, Id(BuiltInBlocks.RedstoneBlock));
        RegisterMineralBlockPair(registry, "diamond", diamond, Id(BuiltInBlocks.DiamondBlock));
        RegisterMineralBlockPair(registry, "emerald", emerald, Id(BuiltInBlocks.EmeraldBlock));
        RegisterMineralBlockPair(registry, "wheat_hay", wheat, Id(BuiltInBlocks.HayBlock));

        AddShaped(registry, "stone_bricks", 2, 2, [stone, stone, stone, stone], Id(BuiltInBlocks.StoneBricks), 4);
        AddShaped(registry, "sandstone", 2, 2, [sand, sand, sand, sand], Id(BuiltInBlocks.Sandstone), 1);
        AddShapeless(registry, "mossy_cobblestone", [cobble, Id(BuiltInBlocks.OakLeaves)], Id(BuiltInBlocks.MossyCobblestone), 1);
        AddShaped(registry, "white_wool_from_string", 2, 2, [str, str, str, str], whiteWool, 1);
        AddShapeless(registry, "blue_wool_dye", [whiteWool, lapis], Id(BuiltInBlocks.BlueWool), 1);
        AddShapeless(registry, "red_wool_dye", [whiteWool, redstone], redWool, 1);
        AddShaped(registry, "tnt", 3, 3, [gunpowder, sand, gunpowder, sand, gunpowder, sand, gunpowder, sand, gunpowder], Id(BuiltInBlocks.Tnt), 1);
        AddShaped(registry, "bread_from_wheat", 3, 1, [wheat, wheat, wheat], Id(BuiltInItems.Bread), 1);
        AddShapeless(registry, "bread_from_hay", [Id(BuiltInBlocks.HayBlock)], Id(BuiltInItems.Bread), 3);
        AddShaped(registry, "golden_apple", 3, 3, [gold, gold, gold, gold, Id(BuiltInItems.Apple), gold, gold, gold, gold], Id(BuiltInItems.GoldenApple), 1);

        // 9. Minecart, Powered Rail, Bow & Arrows
        var feather = Id(BuiltInItems.Feather);
        AddShaped(registry, "powered_rail", 3, 3, [gold, 0, gold, gold, stick, gold, gold, redstone, gold], Id(BuiltInBlocks.PoweredRail), 6);
        AddShaped(registry, "minecart", 3, 2, [iron, 0, iron, iron, iron, iron], Id(BuiltInItems.Minecart), 1);
        AddShaped(registry, "bow_right", 3, 3, [0, stick, str, stick, 0, str, 0, stick, str], Id(BuiltInItems.Bow), 1);
        AddShaped(registry, "bow_left", 3, 3, [str, stick, 0, str, 0, stick, str, stick, 0], Id(BuiltInItems.Bow), 1);
        AddShaped(registry, "arrow", 1, 3, [flint, stick, feather], Id(BuiltInItems.Arrow), 4);
    }

    /// <summary>Creates, populates, and freezes the built-in crafting recipe registry.</summary>
    public static MappedRegistry<ICraftingRecipe> CreateFrozen(IRegistry<ItemDefinition> items)
    {
        var registry = new MappedRegistry<ICraftingRecipe>();
        PopulateBuiltIns(registry, items);
        registry.Freeze();
        return registry;
    }

    /// <summary>Finds the first matching recipe in <paramref name="recipes"/> for <paramref name="grid"/>, or <see langword="null"/>.</summary>
    public static ICraftingRecipe? FindMatch(CraftingGrid grid, IRegistry<ICraftingRecipe> recipes)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(recipes);

        var entries = recipes.Entries;
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].Matches(grid))
            {
                return entries[i];
            }
        }

        return null;
    }

    /// <summary>Attempts to craft one output batch from <paramref name="grid"/>, consuming 1 item per occupied input slot on success.</summary>
    public static ItemStack CraftOnce(CraftingGrid grid, IRegistry<ICraftingRecipe> recipes)
    {
        var match = FindMatch(grid, recipes);
        if (match is null)
        {
            return ItemStack.Empty;
        }

        grid.ConsumeOnePerOccupiedSlot();
        return match.Output;
    }

    private static void RegisterToolTier(
        MappedRegistry<ICraftingRecipe> reg,
        string prefix,
        ushort mat,
        ushort stick,
        ushort swordId,
        ushort pickaxeId,
        ushort axeId,
        ushort shovelId,
        ushort hoeId,
        ResourceKey? pickaxeKeyOverride = null)
    {
        AddShaped(reg, $"{prefix}_sword", 1, 3, [mat, mat, stick], swordId, 1);
        var pickKey = pickaxeKeyOverride ?? ResourceKey.OverHorizon($"{prefix}_pickaxe");
        AddShaped(reg, pickKey, 3, 3, [mat, mat, mat, 0, stick, 0, 0, stick, 0], pickaxeId, 1);
        AddShaped(reg, $"{prefix}_axe", 2, 3, [mat, mat, mat, stick, 0, stick], axeId, 1);
        AddShaped(reg, $"{prefix}_shovel", 1, 3, [mat, stick, stick], shovelId, 1);
        AddShaped(reg, $"{prefix}_hoe", 2, 3, [mat, mat, 0, stick, 0, stick], hoeId, 1);
    }

    private static void RegisterArmorSet(
        MappedRegistry<ICraftingRecipe> reg,
        string prefix,
        ushort mat,
        ushort helmetId,
        ushort chestId,
        ushort legsId,
        ushort bootsId)
    {
        AddShaped(reg, $"{prefix}_helmet", 3, 2, [mat, mat, mat, mat, 0, mat], helmetId, 1);
        AddShaped(reg, $"{prefix}_chestplate", 3, 3, [mat, 0, mat, mat, mat, mat, mat, mat, mat], chestId, 1);
        AddShaped(reg, $"{prefix}_leggings", 3, 3, [mat, mat, mat, mat, 0, mat, mat, 0, mat], legsId, 1);
        AddShaped(reg, $"{prefix}_boots", 3, 2, [mat, 0, mat, mat, 0, mat], bootsId, 1);
    }

    private static void RegisterMineralBlockPair(MappedRegistry<ICraftingRecipe> reg, string prefix, ushort item, ushort block)
    {
        AddShaped(reg, $"{prefix}_block_compress", 3, 3, [item, item, item, item, item, item, item, item, item], block, 1);
        AddShapeless(reg, $"{prefix}_block_unpack", [block], item, 9);
    }

    private static void AddShaped(MappedRegistry<ICraftingRecipe> reg, string name, int w, int h, ReadOnlySpan<ushort> pattern, ushort outId, int count) =>
        AddShaped(reg, ResourceKey.OverHorizon(name), w, h, pattern, outId, count);

    private static void AddShaped(MappedRegistry<ICraftingRecipe> reg, ResourceKey key, int w, int h, ReadOnlySpan<ushort> pattern, ushort outId, int count) =>
        _ = reg.Register(key, new ShapedRecipe(key, w, h, pattern, new ItemStack(outId, count)));

    private static void AddShapeless(MappedRegistry<ICraftingRecipe> reg, string name, ReadOnlySpan<ushort> ingredients, ushort outId, int count) =>
        AddShapeless(reg, ResourceKey.OverHorizon(name), ingredients, outId, count);

    private static void AddShapeless(MappedRegistry<ICraftingRecipe> reg, ResourceKey key, ReadOnlySpan<ushort> ingredients, ushort outId, int count) =>
        _ = reg.Register(key, new ShapelessRecipe(key, ingredients, new ItemStack(outId, count)));
}
