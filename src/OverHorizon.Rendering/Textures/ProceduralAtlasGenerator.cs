using System.Collections.Frozen;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Shared.Hashing;

namespace OverHorizon.Rendering.Textures;

/// <summary>
/// Generates the <c>256x256</c> RGBA8 block &amp; item texture atlas covering all 52 blocks and 50 standalone items,
/// automatically overlaying real <c>16x16</c> PNGs from <c>minecraft-assets</c> (<c>23w13a__or__b</c>).
/// </summary>
public static class ProceduralAtlasGenerator
{
    /// <summary>Width and height of the square RGBA8 block &amp; item atlas in pixels (<c>256</c>).</summary>
    public const int AtlasDimension = 256;

    /// <summary>Width and height of a single block/item tile in pixels (<c>16</c>).</summary>
    public const int TileDimension = 16;

    /// <summary>Number of <c>16x16</c> tiles per row (<c>16</c>).</summary>
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

    // v0.5.0-alpha Block Tiles (33..63)
    private const ushort TileTorch = 33;
    private const ushort TileLantern = 34;
    private const ushort TileFurnaceFront = 35;
    private const ushort TileFurnaceFrontLit = 36;
    private const ushort TileFurnaceSide = 37;
    private const ushort TileFurnaceTop = 38;
    private const ushort TileChestSide = 39;
    private const ushort TileChestTop = 40;
    private const ushort TileBarrelSide = 41;
    private const ushort TileBarrelTop = 42;
    private const ushort TileLadder = 43;
    private const ushort TileRail = 44;
    private const ushort TileCobweb = 45;
    private const ushort TileFarmland = 46;
    private const ushort TileHaySide = 47;
    private const ushort TileHayTop = 48;
    private const ushort TileDeepslate = 49;
    private const ushort TileCobbledDeepslate = 50;
    private const ushort TileCopperOre = 51;
    private const ushort TileLapisOre = 52;
    private const ushort TileRedstoneOre = 53;
    private const ushort TileEmeraldOre = 54;
    private const ushort TileBirchLogSide = 55;
    private const ushort TileBirchLogTop = 56;
    private const ushort TileBirchLeaves = 57;
    private const ushort TileBirchPlanks = 58;
    private const ushort TileCoalBlock = 59;
    private const ushort TileIronBlock = 60;
    private const ushort TileGoldBlock = 61;
    private const ushort TileDiamondBlock = 62;

    private const ushort FirstCustomTile = 72;
    private const ushort FirstItemSpriteTile = 128;

    /// <summary>Creates the default <c>256x256</c> block &amp; item texture atlas.</summary>
    /// <param name="blocks">Frozen block registry.</param>
    /// <param name="externalAssetsPath">Optional external resource pack path.</param>
    /// <returns>Generated <see cref="TextureAtlasData"/>.</returns>
    public static TextureAtlasData CreateDefault(IBlockRegistry blocks, string? externalAssetsPath = null) =>
        Generate(blocks, externalAssetsPath);

    /// <summary>Generates the <c>256x256</c> block &amp; item texture atlas.</summary>
    /// <param name="blocks">Frozen block registry.</param>
    /// <param name="externalAssetsPath">Optional external resource pack path.</param>
    /// <returns>Generated <see cref="TextureAtlasData"/>.</returns>
    public static TextureAtlasData Generate(IBlockRegistry blocks, string? externalAssetsPath = null)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        var pixels = new byte[AtlasDimension * AtlasDimension * 4];
        PaintFallbackBlocksAndItems(pixels);
        var itemTiles = ProceduralItemTileManifest.BuiltInItemTiles;

        using (var resourcePack = ResourcePackLoader.Discover(externalAssetsPath))
        {
            if (resourcePack.LoadedBlockTextureCount > 0)
            {
                ApplyBlockPackOverrides(pixels, resourcePack);
            }

            if (resourcePack.LoadedItemTextureCount > 0 || resourcePack.LoadedBlockTextureCount > 0)
            {
                for (var i = 0; i < itemTiles.Length; i++)
                {
                    var (_, tile, stem) = itemTiles[i];
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
            ProceduralBlockTilePainter.PaintNoiseTile(pixels, t, 110, 112, 118, variation: 16, alpha: 255);
        }

        var itemLookup = new Dictionary<ResourceKey, ushort>();
        for (var i = 0; i < itemTiles.Length; i++)
        {
            itemLookup[itemTiles[i].ItemKey] = itemTiles[i].Tile;
        }

        itemLookup[BuiltInBlocks.Torch] = TileTorch;
        itemLookup[BuiltInBlocks.Ladder] = TileLadder;
        itemLookup[BuiltInBlocks.Rail] = TileRail;
        itemLookup[BuiltInBlocks.Cobweb] = TileCobweb;

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

        // v0.5.0-alpha Blocks
        ProceduralItemTilePainter.PaintTorchTile(pixels, TileTorch);
        ProceduralItemTilePainter.PaintLanternTile(pixels, TileLantern);
        ProceduralItemTilePainter.PaintFurnaceFrontTile(pixels, TileFurnaceFront, lit: false);
        ProceduralItemTilePainter.PaintFurnaceFrontTile(pixels, TileFurnaceFrontLit, lit: true);
        ProceduralBlockTilePainter.PaintStoneBricksTile(pixels, TileFurnaceSide);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileFurnaceTop, 112, 112, 116, variation: 14, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileChestSide, 166, 118, 52, variation: 18, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileChestTop, 152, 108, 46, variation: 14, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileBarrelSide, 124, 90, 54, variation: 16, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileBarrelTop, 138, 102, 62, variation: 14, alpha: 255);
        ProceduralItemTilePainter.PaintLadderTile(pixels, TileLadder);
        ProceduralItemTilePainter.PaintRailTile(pixels, TileRail);
        ProceduralItemTilePainter.PaintCobwebTile(pixels, TileCobweb);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileFarmland, 96, 62, 36, variation: 18, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileHaySide, 212, 180, 58, variation: 18, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileHayTop, 198, 168, 50, variation: 16, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileDeepslate, 74, 74, 82, variation: 16, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileCobbledDeepslate, 66, 66, 74, variation: 22, alpha: 255);
        ProceduralBlockTilePainter.PaintOreTile(pixels, TileCopperOre, 216, 122, 76, 0xC099_0005UL);
        ProceduralBlockTilePainter.PaintOreTile(pixels, TileLapisOre, 44, 88, 214, 0x1A91_0006UL);
        ProceduralBlockTilePainter.PaintOreTile(pixels, TileRedstoneOre, 218, 36, 36, 0xBED5_0007UL);
        ProceduralBlockTilePainter.PaintOreTile(pixels, TileEmeraldOre, 42, 218, 96, 0xE4E2_0008UL);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileBirchLogSide, 224, 220, 212, variation: 26, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileBirchLogTop, 198, 184, 138, variation: 16, alpha: 255);
        ProceduralBlockTilePainter.PaintLeavesTile(pixels, TileBirchLeaves);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileBirchPlanks, 206, 190, 134, variation: 14, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileCoalBlock, 26, 26, 30, variation: 10, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileIronBlock, 222, 224, 228, variation: 10, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileGoldBlock, 248, 212, 54, variation: 12, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileDiamondBlock, 88, 238, 230, variation: 12, alpha: 255);

        // Baseline item sprites (128..197)
        PaintFallbackItems(pixels);
    }

    private static void PaintFallbackItems(byte[] pixels)
    {
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
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 155, 58, 52, 48);
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 156, 208, 172, 142);
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 157, 244, 202, 62);
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 158, 214, 118, 76);
        ProceduralItemTilePainter.PaintIngot(pixels, 159, 216, 124, 78);
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 160, 42, 222, 98);
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 161, 46, 92, 220);
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 162, 218, 32, 32);
        ProceduralItemTilePainter.PaintIngot(pixels, 163, 72, 66, 74);
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 164, 78, 78, 86);
        ProceduralItemTilePainter.PaintStick(pixels, 165);
        ProceduralItemTilePainter.PaintStick(pixels, 166);
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 167, 112, 112, 116);
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 168, 156, 36, 54);
        ProceduralItemTilePainter.PaintIngot(pixels, 169, 168, 102, 56);
        ProceduralItemTilePainter.PaintStick(pixels, 170);
        ProceduralItemTilePainter.PaintPickaxe(pixels, 171, 248, 214, 62);
        ProceduralItemTilePainter.PaintPickaxe(pixels, 172, 76, 68, 78);
        ProceduralItemTilePainter.PaintAxe(pixels, 173, 168, 126, 74);
        ProceduralItemTilePainter.PaintAxe(pixels, 174, 148, 148, 154);
        ProceduralItemTilePainter.PaintShovel(pixels, 175, 168, 126, 74);
        ProceduralItemTilePainter.PaintShovel(pixels, 176, 148, 148, 154);
        ProceduralItemTilePainter.PaintHoe(pixels, 177, 168, 126, 74);
        ProceduralItemTilePainter.PaintHoe(pixels, 178, 148, 148, 154);
        ProceduralItemTilePainter.PaintHoe(pixels, 179, 218, 222, 228);
        ProceduralItemTilePainter.PaintHoe(pixels, 180, 74, 238, 228);
        ProceduralItemTilePainter.PaintArmor(pixels, 181, 218, 222, 228, 0);
        ProceduralItemTilePainter.PaintArmor(pixels, 182, 218, 222, 228, 1);
        ProceduralItemTilePainter.PaintArmor(pixels, 183, 218, 222, 228, 2);
        ProceduralItemTilePainter.PaintArmor(pixels, 184, 218, 222, 228, 3);
        ProceduralItemTilePainter.PaintArmor(pixels, 185, 74, 238, 228, 0);
        ProceduralItemTilePainter.PaintArmor(pixels, 186, 74, 238, 228, 1);
        ProceduralItemTilePainter.PaintArmor(pixels, 187, 74, 238, 228, 2);
        ProceduralItemTilePainter.PaintArmor(pixels, 188, 74, 238, 228, 3);
        ProceduralItemTilePainter.PaintIngot(pixels, 189, 232, 148, 142);
        ProceduralItemTilePainter.PaintIngot(pixels, 190, 214, 168, 112);
        ProceduralItemTilePainter.PaintIngot(pixels, 191, 198, 68, 62);
        ProceduralItemTilePainter.PaintIngot(pixels, 192, 236, 186, 172);
        ProceduralItemTilePainter.PaintIngot(pixels, 193, 206, 138, 74);
        ProceduralItemTilePainter.PaintIngot(pixels, 194, 208, 86, 82);
        ProceduralItemTilePainter.PaintIngot(pixels, 195, 148, 88, 58);
        ProceduralItemTilePainter.PaintIngot(pixels, 196, 146, 78, 54);
        ProceduralItemTilePainter.PaintLanternTile(pixels, 197);
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

        // v0.5.0-alpha Blocks
        MapAllFaces(lookup, BuiltInBlocks.Torch, TileTorch);
        MapAllFaces(lookup, BuiltInBlocks.Lantern, TileLantern);
        MapBlockTopBottomSide(lookup, BuiltInBlocks.Furnace, TileFurnaceTop, TileFurnaceTop, TileFurnaceSide);
        lookup[(BuiltInBlocks.Furnace, Direction.North)] = TileFurnaceFront;
        MapBlockTopBottomSide(lookup, BuiltInBlocks.LitFurnace, TileFurnaceTop, TileFurnaceTop, TileFurnaceSide);
        lookup[(BuiltInBlocks.LitFurnace, Direction.North)] = TileFurnaceFrontLit;
        MapBlockTopBottomSide(lookup, BuiltInBlocks.Chest, TileChestTop, TileChestTop, TileChestSide);
        MapBlockTopBottomSide(lookup, BuiltInBlocks.Barrel, TileBarrelTop, TileBarrelTop, TileBarrelSide);
        MapAllFaces(lookup, BuiltInBlocks.Ladder, TileLadder);
        MapAllFaces(lookup, BuiltInBlocks.Rail, TileRail);
        MapAllFaces(lookup, BuiltInBlocks.Cobweb, TileCobweb);
        MapAllFaces(lookup, BuiltInBlocks.OakFence, TileOakPlanks);
        MapBlockTopBottomSide(lookup, BuiltInBlocks.Farmland, TileFarmland, TileDirt, TileDirt);
        MapBlockTopBottomSide(lookup, BuiltInBlocks.HayBlock, TileHayTop, TileHayTop, TileHaySide);
        MapAllFaces(lookup, BuiltInBlocks.Deepslate, TileDeepslate);
        MapAllFaces(lookup, BuiltInBlocks.CobbledDeepslate, TileCobbledDeepslate);
        MapAllFaces(lookup, BuiltInBlocks.CopperOre, TileCopperOre);
        MapAllFaces(lookup, BuiltInBlocks.LapisOre, TileLapisOre);
        MapAllFaces(lookup, BuiltInBlocks.RedstoneOre, TileRedstoneOre);
        MapAllFaces(lookup, BuiltInBlocks.EmeraldOre, TileEmeraldOre);
        MapBlockTopBottomSide(lookup, BuiltInBlocks.BirchLog, TileBirchLogTop, TileBirchLogTop, TileBirchLogSide);
        MapAllFaces(lookup, BuiltInBlocks.BirchLeaves, TileBirchLeaves);
        MapAllFaces(lookup, BuiltInBlocks.BirchPlanks, TileBirchPlanks);
        MapAllFaces(lookup, BuiltInBlocks.CoalBlock, TileCoalBlock);
        MapAllFaces(lookup, BuiltInBlocks.IronBlock, TileIronBlock);
        MapAllFaces(lookup, BuiltInBlocks.GoldBlock, TileGoldBlock);
        MapAllFaces(lookup, BuiltInBlocks.DiamondBlock, TileDiamondBlock);
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

        _ = pack.TryBlitTile(pixels, TileTorch, "torch");
        _ = pack.TryBlitTile(pixels, TileLantern, "lantern");
        _ = pack.TryBlitTile(pixels, TileFurnaceFront, "furnace_front");
        _ = pack.TryBlitTile(pixels, TileFurnaceFrontLit, "furnace_front_on");
        _ = pack.TryBlitTile(pixels, TileFurnaceSide, "furnace_side");
        _ = pack.TryBlitTile(pixels, TileFurnaceTop, "furnace_top");
        _ = pack.TryBlitTile(pixels, TileBarrelSide, "barrel_side");
        _ = pack.TryBlitTile(pixels, TileBarrelTop, "barrel_top");
        _ = pack.TryBlitTile(pixels, TileLadder, "ladder");
        _ = pack.TryBlitTile(pixels, TileRail, "rail");
        _ = pack.TryBlitTile(pixels, TileCobweb, "cobweb");
        _ = pack.TryBlitTile(pixels, TileFarmland, "farmland_moist");
        _ = pack.TryBlitTile(pixels, TileHaySide, "hay_block_side");
        _ = pack.TryBlitTile(pixels, TileHayTop, "hay_block_top");
        _ = pack.TryBlitTile(pixels, TileDeepslate, "deepslate");
        _ = pack.TryBlitTile(pixels, TileCobbledDeepslate, "cobbled_deepslate");
        _ = pack.TryBlitTile(pixels, TileCopperOre, "copper_ore");
        _ = pack.TryBlitTile(pixels, TileLapisOre, "lapis_ore");
        _ = pack.TryBlitTile(pixels, TileRedstoneOre, "redstone_ore");
        _ = pack.TryBlitTile(pixels, TileEmeraldOre, "emerald_ore");
        _ = pack.TryBlitTile(pixels, TileBirchLogSide, "birch_log");
        _ = pack.TryBlitTile(pixels, TileBirchLogTop, "birch_log_top");
        _ = pack.TryBlitTile(pixels, TileBirchLeaves, "birch_leaves", 128, 168, 86);
        _ = pack.TryBlitTile(pixels, TileBirchPlanks, "birch_planks");
        _ = pack.TryBlitTile(pixels, TileCoalBlock, "coal_block");
        _ = pack.TryBlitTile(pixels, TileIronBlock, "iron_block");
        _ = pack.TryBlitTile(pixels, TileGoldBlock, "gold_block");
        _ = pack.TryBlitTile(pixels, TileDiamondBlock, "diamond_block");
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
