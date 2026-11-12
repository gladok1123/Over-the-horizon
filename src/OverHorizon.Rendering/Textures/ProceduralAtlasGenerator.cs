using System.Collections.Frozen;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Shared.Hashing;

namespace OverHorizon.Rendering.Textures;

/// <summary>
/// Generates the <c>256x256</c> RGBA8 block &amp; item texture atlas, automatically overlaying real
/// <c>16x16</c> block and item PNGs discovered in <c>minecraft-assets</c> (<c>23w13a__or__b</c>) or user packs.
/// </summary>
public static class ProceduralAtlasGenerator
{
    /// <summary>Atlas width and height in pixels (<c>16 * 16 = 256</c>).</summary>
    public const int AtlasDimension = 256;

    /// <summary>Edge size of each block/item tile in pixels.</summary>
    public const int TileDimension = 16;

    /// <summary>Number of <c>16x16</c> tiles along each row of the atlas (<c>16</c>).</summary>
    public const int TilesPerRow = AtlasDimension / TileDimension;

    private const ushort TileStone = 0;
    private const ushort TileDirt = 1;
    private const ushort TileGrassTop = 2;
    private const ushort TileGrassSide = 3;
    private const ushort TileCobblestone = 4;
    private const ushort TileOakLogSide = 5;
    private const ushort TileOakLogTop = 6;
    private const ushort TileOakLeaves = 7;
    private const ushort TileSand = 8;
    private const ushort TileWater = 9;
    private const ushort TileGlowstone = 10;
    private const ushort TileBedrock = 11;
    private const ushort TileOakPlanks = 12;
    private const ushort TileCraftingTop = 13;
    private const ushort TileCraftingSide = 14;
    private const ushort TileStoneBricks = 15;
    private const ushort TileClayBricks = 16;
    private const ushort TileGlass = 17;
    private const ushort TileSandstoneSide = 18;
    private const ushort TileBookshelfSide = 19;
    private const ushort TileCoalOre = 20;
    private const ushort TileIronOre = 21;
    private const ushort TileGoldOre = 22;
    private const ushort TileDiamondOre = 23;
    private const ushort TileMossyCobblestone = 24;
    private const ushort TileSnowBlock = 25;
    private const ushort TileCrimsonWool = 26;
    private const ushort TileGravel = 27;
    private const ushort TileObsidian = 28;
    private const ushort TileTntSide = 29;
    private const ushort TileTntTop = 30;
    private const ushort TileWhiteWool = 31;
    private const ushort TileBlueWool = 32;
    private const ushort FirstCustomTile = 40;
    private const ushort FirstItemSpriteTile = 128;

    private static readonly (ResourceKey ItemKey, ushort Tile, string PngStem)[] BuiltInItemTiles =
    [
        (BuiltInItems.Stick, 128, "stick"),
        (BuiltInItems.Coal, 129, "coal"),
        (BuiltInItems.IronIngot, 130, "iron_ingot"),
        (BuiltInItems.GoldIngot, 131, "gold_ingot"),
        (BuiltInItems.Diamond, 132, "diamond"),
        (BuiltInItems.WoodenSword, 133, "wooden_sword"),
        (BuiltInItems.StoneSword, 134, "stone_sword"),
        (BuiltInItems.GoldenSword, 135, "golden_sword"),
        (BuiltInItems.IronSword, 136, "iron_sword"),
        (BuiltInItems.DiamondSword, 137, "diamond_sword"),
        (BuiltInItems.NetheriteSword, 138, "netherite_sword"),
        (BuiltInItems.WoodenPickaxe, 139, "wooden_pickaxe"),
        (BuiltInItems.StonePickaxe, 140, "stone_pickaxe"),
        (BuiltInItems.IronPickaxe, 141, "iron_pickaxe"),
        (BuiltInItems.DiamondPickaxe, 142, "diamond_pickaxe"),
        (BuiltInItems.IronAxe, 143, "iron_axe"),
        (BuiltInItems.DiamondAxe, 144, "diamond_axe"),
        (BuiltInItems.IronShovel, 145, "iron_shovel"),
        (BuiltInItems.DiamondShovel, 146, "diamond_shovel"),
        (BuiltInItems.Shears, 147, "shears"),
        (BuiltInItems.FlintAndSteel, 148, "flint_and_steel"),
        (BuiltInItems.Bucket, 149, "bucket"),
        (BuiltInItems.WaterBucket, 150, "water_bucket"),
        (BuiltInItems.Apple, 151, "apple"),
        (BuiltInItems.Bread, 152, "bread"),
        (BuiltInItems.CookedBeef, 153, "cooked_beef"),
        (BuiltInItems.GoldenApple, 154, "golden_apple"),
    ];

    /// <summary>
    /// Alias for <see cref="Generate"/> creating the block &amp; item texture atlas.
    /// </summary>
    public static TextureAtlasData CreateDefault(IBlockRegistry blocks, string? externalAssetsPath = null) =>
        Generate(blocks, externalAssetsPath);

    /// <summary>
    /// Generates a deterministic <c>256x256</c> RGBA8 texture atlas covering all blocks and items,
    /// overlaying real <c>16x16</c> PNG block &amp; item textures from <see cref="ResourcePackLoader"/>.
    /// </summary>
    public static TextureAtlasData Generate(IBlockRegistry blocks, string? externalAssetsPath = null)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        var pixels = new byte[AtlasDimension * AtlasDimension * 4];
        PaintFallbackBlocksAndItems(pixels);

        using (var resourcePack = ResourcePackLoader.Discover(externalAssetsPath))
        {
            if (resourcePack.LoadedBlockTextureCount > 0)
            {
                ApplyBlockPackOverrides(pixels, resourcePack);
            }

            if (resourcePack.LoadedItemTextureCount > 0)
            {
                for (var i = 0; i < BuiltInItemTiles.Length; i++)
                {
                    var (_, tile, stem) = BuiltInItemTiles[i];
                    _ = resourcePack.TryBlitItemTile(pixels, tile, stem);
                }
            }
        }

        var lookup = BuildFaceLookup();
        var nextCustomTile = FirstCustomTile;
        var entries = blocks.Entries;
        for (var i = 0; i < entries.Count && nextCustomTile < FirstItemSpriteTile; i++)
        {
            var def = entries[i];
            if (def.Key == BuiltInBlocks.Air || lookup.ContainsKey((def.Key, Direction.Up)))
            {
                continue;
            }

            var hasher = Fnv1a64.Create();
            hasher.AppendString(def.Key.ToString());
            var h = hasher.Value;

            ProceduralBlockTilePainter.PaintNoiseTile(
                pixels,
                nextCustomTile,
                (byte)(64 + ((h >> 0) & 0x7F)),
                (byte)(64 + ((h >> 8) & 0x7F)),
                (byte)(64 + ((h >> 16) & 0x7F)),
                variation: 24,
                alpha: def.IsOpaque ? (byte)255 : (byte)200);
            MapAllFaces(lookup, def.Key, nextCustomTile);
            nextCustomTile++;
        }

        for (var t = nextCustomTile; t < FirstItemSpriteTile; t++)
        {
            ProceduralBlockTilePainter.PaintNoiseTile(
                pixels,
                t,
                (byte)(80 + ((t * 37) & 127)),
                (byte)(80 + ((t * 73) & 127)),
                (byte)(80 + ((t * 109) & 127)),
                variation: 18,
                alpha: 255);
        }

        var itemLookup = new Dictionary<ResourceKey, ushort>();
        for (var i = 0; i < BuiltInItemTiles.Length; i++)
        {
            itemLookup[BuiltInItemTiles[i].ItemKey] = BuiltInItemTiles[i].Tile;
        }

        return new TextureAtlasData(
            AtlasDimension,
            AtlasDimension,
            TileDimension,
            pixels,
            lookup.ToFrozenDictionary(),
            itemLookup.ToFrozenDictionary(),
            FirstCustomTile);
    }

    private static void PaintFallbackBlocksAndItems(byte[] pixels)
    {
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileStone, 124, 124, 128, variation: 18, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileDirt, 134, 96, 67, variation: 20, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileGrassTop, 91, 158, 58, variation: 22, alpha: 255);
        ProceduralBlockTilePainter.PaintGrassSideTile(pixels, TileGrassSide);
        ProceduralBlockTilePainter.PaintCobblestoneTile(pixels, TileCobblestone, mossy: false);
        ProceduralBlockTilePainter.PaintLogSideTile(pixels, TileOakLogSide);
        ProceduralBlockTilePainter.PaintLogTopTile(pixels, TileOakLogTop);
        ProceduralBlockTilePainter.PaintLeavesTile(pixels, TileOakLeaves);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileSand, 219, 207, 158, variation: 14, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileWater, 42, 108, 212, variation: 16, alpha: 185);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileGlowstone, 242, 206, 102, variation: 32, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileBedrock, 52, 52, 56, variation: 34, alpha: 255);
        ProceduralBlockTilePainter.PaintPlanksTile(pixels, TileOakPlanks);
        ProceduralBlockTilePainter.PaintCraftingTopTile(pixels, TileCraftingTop);
        ProceduralBlockTilePainter.PaintPlanksTile(pixels, TileCraftingSide);
        ProceduralBlockTilePainter.PaintStoneBricksTile(pixels, TileStoneBricks);
        ProceduralBlockTilePainter.PaintClayBricksTile(pixels, TileClayBricks);
        ProceduralBlockTilePainter.PaintGlassTile(pixels, TileGlass);
        ProceduralBlockTilePainter.PaintSandstoneSideTile(pixels, TileSandstoneSide);
        ProceduralBlockTilePainter.PaintBookshelfSideTile(pixels, TileBookshelfSide);
        ProceduralBlockTilePainter.PaintOreTile(pixels, TileCoalOre, 34, 34, 38, 0xC0A1_0001UL);
        ProceduralBlockTilePainter.PaintOreTile(pixels, TileIronOre, 216, 178, 148, 0x1B04_0002UL);
        ProceduralBlockTilePainter.PaintOreTile(pixels, TileGoldOre, 248, 214, 62, 0x601D_0003UL);
        ProceduralBlockTilePainter.PaintOreTile(pixels, TileDiamondOre, 78, 236, 228, 0xD1A4_0004UL);
        ProceduralBlockTilePainter.PaintCobblestoneTile(pixels, TileMossyCobblestone, mossy: true);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileSnowBlock, 240, 246, 250, variation: 8, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileCrimsonWool, 178, 42, 52, variation: 16, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileGravel, 132, 126, 124, variation: 28, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileObsidian, 26, 18, 38, variation: 18, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileTntSide, 206, 48, 38, variation: 20, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileTntTop, 188, 52, 42, variation: 18, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileWhiteWool, 236, 238, 240, variation: 10, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileBlueWool, 48, 72, 182, variation: 14, alpha: 255);

        // Baseline item sprites (tiles 128..154)
        ProceduralItemTilePainter.PaintStick(pixels, 128);
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 129, 44, 44, 48);
        ProceduralItemTilePainter.PaintIngot(pixels, 130, 214, 218, 224);
        ProceduralItemTilePainter.PaintIngot(pixels, 131, 248, 212, 58);
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 132, 74, 236, 228);
        ProceduralItemTilePainter.PaintSword(pixels, 133, 168, 126, 74);
        ProceduralItemTilePainter.PaintSword(pixels, 134, 148, 148, 154);
        ProceduralItemTilePainter.PaintSword(pixels, 135, 248, 214, 62);
        ProceduralItemTilePainter.PaintSword(pixels, 136, 218, 222, 228);
        ProceduralItemTilePainter.PaintSword(pixels, 137, 74, 238, 228);
        ProceduralItemTilePainter.PaintSword(pixels, 138, 76, 68, 78);
        ProceduralItemTilePainter.PaintPickaxe(pixels, 139, 168, 126, 74);
        ProceduralItemTilePainter.PaintPickaxe(pixels, 140, 148, 148, 154);
        ProceduralItemTilePainter.PaintPickaxe(pixels, 141, 218, 222, 228);
        ProceduralItemTilePainter.PaintPickaxe(pixels, 142, 74, 238, 228);
        ProceduralItemTilePainter.PaintAxe(pixels, 143, 218, 222, 228);
        ProceduralItemTilePainter.PaintAxe(pixels, 144, 74, 238, 228);
        ProceduralItemTilePainter.PaintShovel(pixels, 145, 218, 222, 228);
        ProceduralItemTilePainter.PaintShovel(pixels, 146, 74, 238, 228);
        ProceduralItemTilePainter.PaintSword(pixels, 147, 198, 204, 210);
        ProceduralItemTilePainter.PaintIngot(pixels, 148, 128, 132, 138);
        ProceduralItemTilePainter.PaintBucket(pixels, 149, filledWithWater: false);
        ProceduralItemTilePainter.PaintBucket(pixels, 150, filledWithWater: true);
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 151, 218, 44, 42);
        ProceduralItemTilePainter.PaintIngot(pixels, 152, 204, 152, 76);
        ProceduralItemTilePainter.PaintIngot(pixels, 153, 136, 68, 44);
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 154, 250, 214, 54);
    }

    private static Dictionary<(ResourceKey, Direction), ushort> BuildFaceLookup()
    {
        var lookup = new Dictionary<(ResourceKey, Direction), ushort>();
        MapAllFaces(lookup, BuiltInBlocks.Stone, TileStone);
        MapAllFaces(lookup, BuiltInBlocks.Dirt, TileDirt);
        MapBlockTopBottomSide(lookup, BuiltInBlocks.GrassBlock, TileGrassTop, TileDirt, TileGrassSide);
        MapAllFaces(lookup, BuiltInBlocks.Cobblestone, TileCobblestone);
        MapBlockTopBottomSide(lookup, BuiltInBlocks.OakLog, TileOakLogTop, TileOakLogTop, TileOakLogSide);
        MapAllFaces(lookup, BuiltInBlocks.OakLeaves, TileOakLeaves);
        MapAllFaces(lookup, BuiltInBlocks.Sand, TileSand);
        MapAllFaces(lookup, BuiltInBlocks.Water, TileWater);
        MapAllFaces(lookup, BuiltInBlocks.Glowstone, TileGlowstone);
        MapAllFaces(lookup, BuiltInBlocks.Bedrock, TileBedrock);
        MapAllFaces(lookup, BuiltInBlocks.OakPlanks, TileOakPlanks);
        MapBlockTopBottomSide(lookup, BuiltInBlocks.CraftingTable, TileCraftingTop, TileOakPlanks, TileCraftingSide);
        MapAllFaces(lookup, BuiltInBlocks.StoneBricks, TileStoneBricks);
        MapAllFaces(lookup, BuiltInBlocks.Bricks, TileClayBricks);
        MapAllFaces(lookup, BuiltInBlocks.Glass, TileGlass);
        MapBlockTopBottomSide(lookup, BuiltInBlocks.Sandstone, TileSand, TileSand, TileSandstoneSide);
        MapBlockTopBottomSide(lookup, BuiltInBlocks.Bookshelf, TileOakPlanks, TileOakPlanks, TileBookshelfSide);
        MapAllFaces(lookup, BuiltInBlocks.CoalOre, TileCoalOre);
        MapAllFaces(lookup, BuiltInBlocks.IronOre, TileIronOre);
        MapAllFaces(lookup, BuiltInBlocks.GoldOre, TileGoldOre);
        MapAllFaces(lookup, BuiltInBlocks.DiamondOre, TileDiamondOre);
        MapAllFaces(lookup, BuiltInBlocks.MossyCobblestone, TileMossyCobblestone);
        MapAllFaces(lookup, BuiltInBlocks.SnowBlock, TileSnowBlock);
        MapAllFaces(lookup, BuiltInBlocks.CrimsonWool, TileCrimsonWool);
        MapAllFaces(lookup, BuiltInBlocks.Gravel, TileGravel);
        MapAllFaces(lookup, BuiltInBlocks.Obsidian, TileObsidian);
        MapBlockTopBottomSide(lookup, BuiltInBlocks.Tnt, TileTntTop, TileTntTop, TileTntSide);
        MapAllFaces(lookup, BuiltInBlocks.WhiteWool, TileWhiteWool);
        MapAllFaces(lookup, BuiltInBlocks.BlueWool, TileBlueWool);
        return lookup;
    }

    private static void ApplyBlockPackOverrides(byte[] pixels, ResourcePackLoader pack)
    {
        _ = pack.TryBlitTile(pixels, TileStone, "stone");
        _ = pack.TryBlitTile(pixels, TileDirt, "dirt");
        _ = pack.TryBlitTile(pixels, TileGrassTop, "grass_block_top", 118, 190, 92);
        _ = pack.TryBlitTile(pixels, TileGrassSide, "grass_block_side");
        _ = pack.TryBlitTile(pixels, TileCobblestone, "cobblestone");
        _ = pack.TryBlitTile(pixels, TileOakLogSide, "oak_log");
        _ = pack.TryBlitTile(pixels, TileOakLogTop, "oak_log_top");
        _ = pack.TryBlitTile(pixels, TileOakLeaves, "oak_leaves", 96, 172, 72);
        _ = pack.TryBlitTile(pixels, TileSand, "sand");
        _ = pack.TryBlitTile(pixels, TileWater, "water_still", 63, 118, 228, alphaOverride: 205);
        _ = pack.TryBlitTile(pixels, TileGlowstone, "glowstone");
        _ = pack.TryBlitTile(pixels, TileBedrock, "bedrock");
        _ = pack.TryBlitTile(pixels, TileOakPlanks, "oak_planks");
        _ = pack.TryBlitTile(pixels, TileCraftingTop, "crafting_table_top");
        if (!pack.TryBlitTile(pixels, TileCraftingSide, "crafting_table_front"))
        {
            _ = pack.TryBlitTile(pixels, TileCraftingSide, "crafting_table_side");
        }

        _ = pack.TryBlitTile(pixels, TileStoneBricks, "stone_bricks");
        _ = pack.TryBlitTile(pixels, TileClayBricks, "bricks");
        _ = pack.TryBlitTile(pixels, TileGlass, "glass");
        _ = pack.TryBlitTile(pixels, TileSandstoneSide, "sandstone");
        _ = pack.TryBlitTile(pixels, TileBookshelfSide, "bookshelf");
        _ = pack.TryBlitTile(pixels, TileCoalOre, "coal_ore");
        _ = pack.TryBlitTile(pixels, TileIronOre, "iron_ore");
        _ = pack.TryBlitTile(pixels, TileGoldOre, "gold_ore");
        _ = pack.TryBlitTile(pixels, TileDiamondOre, "diamond_ore");
        _ = pack.TryBlitTile(pixels, TileMossyCobblestone, "mossy_cobblestone");
        if (!pack.TryBlitTile(pixels, TileSnowBlock, "snow"))
        {
            _ = pack.TryBlitTile(pixels, TileSnowBlock, "snow_block");
        }

        if (!pack.TryBlitTile(pixels, TileCrimsonWool, "red_wool"))
        {
            _ = pack.TryBlitTile(pixels, TileCrimsonWool, "crimson_wool");
        }

        _ = pack.TryBlitTile(pixels, TileGravel, "gravel");
        _ = pack.TryBlitTile(pixels, TileObsidian, "obsidian");
        _ = pack.TryBlitTile(pixels, TileTntSide, "tnt_side");
        _ = pack.TryBlitTile(pixels, TileTntTop, "tnt_top");
        _ = pack.TryBlitTile(pixels, TileWhiteWool, "white_wool");
        _ = pack.TryBlitTile(pixels, TileBlueWool, "blue_wool");
    }

    private static void MapAllFaces(Dictionary<(ResourceKey, Direction), ushort> map, ResourceKey key, ushort tile)
    {
        foreach (var dir in DirectionExtensions.All)
        {
            map[(key, dir)] = tile;
        }
    }

    private static void MapBlockTopBottomSide(
        Dictionary<(ResourceKey, Direction), ushort> map,
        ResourceKey key,
        ushort topTile,
        ushort bottomTile,
        ushort sideTile)
    {
        map[(key, Direction.Up)] = topTile;
        map[(key, Direction.Down)] = bottomTile;
        map[(key, Direction.North)] = sideTile;
        map[(key, Direction.South)] = sideTile;
        map[(key, Direction.West)] = sideTile;
        map[(key, Direction.East)] = sideTile;
    }
}
