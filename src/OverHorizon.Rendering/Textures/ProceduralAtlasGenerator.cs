using System.Collections.Frozen;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Shared.Hashing;

namespace OverHorizon.Rendering.Textures;

/// <summary>
/// Clean-room procedural 16x16 RGBA8 block texture atlas generator (zero proprietary assets).
/// </summary>
public static class ProceduralAtlasGenerator
{
    /// <summary>Atlas width and height in pixels (<c>16 * 16 = 256</c>).</summary>
    public const int AtlasDimension = 256;

    /// <summary>Edge size of each block tile in pixels.</summary>
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
    private const ushort FirstCustomTile = 32;

    /// <summary>
    /// Alias for <see cref="Generate"/> creating the block texture atlas with optional external resource pack overrides.
    /// </summary>
    /// <param name="blocks">Frozen block registry.</param>
    /// <param name="externalAssetsPath">Optional directory, <c>.zip</c>, or <c>.jar</c> containing <c>assets/*/textures/block/*.png</c>.</param>
    /// <returns>Constructed <see cref="TextureAtlasData"/>.</returns>
    public static TextureAtlasData CreateDefault(IBlockRegistry blocks, string? externalAssetsPath = null) =>
        Generate(blocks, externalAssetsPath);

    /// <summary>
    /// Generates a deterministic 256x256 RGBA8 texture atlas covering all built-in and mod-registered blocks in <paramref name="blocks"/>,
    /// overlaying real PNG block textures whenever discovered by <see cref="ResourcePackLoader"/>.
    /// </summary>
    /// <param name="blocks">Frozen block registry.</param>
    /// <param name="externalAssetsPath">Optional directory, <c>.zip</c>, or <c>.jar</c> path.</param>
    /// <returns>Constructed <see cref="TextureAtlasData"/>.</returns>
    public static TextureAtlasData Generate(IBlockRegistry blocks, string? externalAssetsPath = null)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        var pixels = new byte[AtlasDimension * AtlasDimension * 4];

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

        using (var resourcePack = ResourcePackLoader.Discover(externalAssetsPath))
        {
            if (resourcePack.LoadedBlockTextureCount > 0)
            {
                ApplyResourcePackOverrides(pixels, resourcePack);
            }
        }

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

        var nextCustomTile = FirstCustomTile;
        var entries = blocks.Entries;
        for (var i = 0; i < entries.Count && nextCustomTile < 256; i++)
        {
            var def = entries[i];
            if (def.Key == BuiltInBlocks.Air || lookup.ContainsKey((def.Key, Direction.Up)))
            {
                continue;
            }

            var hasher = Fnv1a64.Create();
            hasher.AppendString(def.Key.ToString());
            var h = hasher.Value;

            var r = (byte)(64 + ((h >> 0) & 0x7F));
            var g = (byte)(64 + ((h >> 8) & 0x7F));
            var b = (byte)(64 + ((h >> 16) & 0x7F));
            var a = def.IsOpaque ? (byte)255 : (byte)200;

            ProceduralBlockTilePainter.PaintNoiseTile(pixels, nextCustomTile, r, g, b, variation: 24, alpha: a);
            MapAllFaces(lookup, def.Key, nextCustomTile);
            nextCustomTile++;
        }

        for (var t = nextCustomTile; t < 256; t++)
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

        return new TextureAtlasData(
            AtlasDimension,
            AtlasDimension,
            TileDimension,
            pixels,
            lookup.ToFrozenDictionary(),
            FirstCustomTile);
    }

    private static void ApplyResourcePackOverrides(byte[] pixels, ResourcePackLoader pack)
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
