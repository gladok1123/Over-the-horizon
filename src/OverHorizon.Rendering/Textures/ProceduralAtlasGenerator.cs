using System.Collections.Frozen;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Shared.Hashing;
using OverHorizon.Shared.Random;

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
    private const ushort FirstCustomTile = 16;

    /// <summary>
    /// Generates a deterministic 256x256 RGBA8 texture atlas covering all built-in and mod-registered blocks in <paramref name="blocks"/>.
    /// </summary>
    /// <param name="blocks">Frozen block registry.</param>
    /// <returns>Generated <see cref="TextureAtlasData"/>.</returns>
    public static TextureAtlasData Generate(IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        var pixels = new byte[AtlasDimension * AtlasDimension * 4];

        PaintNoiseTile(pixels, TileStone, 124, 124, 128, variation: 18, alpha: 255);
        PaintNoiseTile(pixels, TileDirt, 134, 96, 67, variation: 20, alpha: 255);
        PaintNoiseTile(pixels, TileGrassTop, 91, 158, 58, variation: 22, alpha: 255);
        PaintGrassSideTile(pixels, TileGrassSide);
        PaintCobblestoneTile(pixels, TileCobblestone);
        PaintLogSideTile(pixels, TileOakLogSide);
        PaintLogTopTile(pixels, TileOakLogTop);
        PaintLeavesTile(pixels, TileOakLeaves);
        PaintNoiseTile(pixels, TileSand, 219, 207, 158, variation: 14, alpha: 255);
        PaintNoiseTile(pixels, TileWater, 42, 108, 212, variation: 16, alpha: 185);
        PaintNoiseTile(pixels, TileGlowstone, 242, 206, 102, variation: 32, alpha: 255);
        PaintNoiseTile(pixels, TileBedrock, 52, 52, 56, variation: 34, alpha: 255);
        PaintPlanksTile(pixels, TileOakPlanks);
        PaintCraftingTopTile(pixels, TileCraftingTop);
        PaintPlanksTile(pixels, TileCraftingSide);

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

        // Paint dedicated procedural tiles for any mod/datapack blocks!
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

            PaintNoiseTile(pixels, nextCustomTile, r, g, b, variation: 24, alpha: a);
            MapAllFaces(lookup, def.Key, nextCustomTile);
            nextCustomTile++;
        }

        // Fill remaining fallback slots deterministically so unmapped runtime keys still render cleanly.
        for (var t = nextCustomTile; t < 256; t++)
        {
            PaintNoiseTile(
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

    private static void PaintNoiseTile(
        byte[] atlas,
        ushort tileIndex,
        byte baseR,
        byte baseG,
        byte baseB,
        int variation,
        byte alpha)
    {
        var rng = new Xoroshiro128PlusPlus(0xA71A_5000UL + tileIndex);
        for (var py = 0; py < TileDimension; py++)
        {
            for (var px = 0; px < TileDimension; px++)
            {
                var jitter = rng.NextInt32((variation * 2) + 1) - variation;
                WriteAtlasPixel(
                    atlas,
                    tileIndex,
                    px,
                    py,
                    ClampByte(baseR + jitter),
                    ClampByte(baseG + jitter),
                    ClampByte(baseB + jitter),
                    alpha);
            }
        }
    }

    private static void PaintGrassSideTile(byte[] atlas, ushort tileIndex)
    {
        var rng = new Xoroshiro128PlusPlus(0x6BA5_51DEUL);
        for (var py = 0; py < TileDimension; py++)
        {
            for (var px = 0; px < TileDimension; px++)
            {
                var overhang = 3 + ((px * 7 + 3) % 3);
                var jitter = rng.NextInt32(25) - 12;
                if (py < overhang)
                {
                    WriteAtlasPixel(atlas, tileIndex, px, py, ClampByte(91 + jitter), ClampByte(158 + jitter), ClampByte(58 + jitter), 255);
                }
                else
                {
                    WriteAtlasPixel(atlas, tileIndex, px, py, ClampByte(134 + jitter), ClampByte(96 + jitter), ClampByte(67 + jitter), 255);
                }
            }
        }
    }

    private static void PaintCobblestoneTile(byte[] atlas, ushort tileIndex)
    {
        var rng = new Xoroshiro128PlusPlus(0xC0BB_1E00UL);
        for (var py = 0; py < TileDimension; py++)
        {
            for (var px = 0; px < TileDimension; px++)
            {
                var mortar = ((px + ((py >> 2) * 3)) % 5 == 0) || ((py + ((px >> 2) * 2)) % 5 == 0);
                var baseVal = mortar ? 82 : 132 + (rng.NextInt32(26) - 13);
                var b = ClampByte(baseVal);
                WriteAtlasPixel(atlas, tileIndex, px, py, b, b, b, 255);
            }
        }
    }

    private static void PaintLogSideTile(byte[] atlas, ushort tileIndex)
    {
        var rng = new Xoroshiro128PlusPlus(0x1065_1DE0UL);
        for (var py = 0; py < TileDimension; py++)
        {
            for (var px = 0; px < TileDimension; px++)
            {
                var ridge = (px % 4 == 0) ? -16 : 6;
                var jitter = rng.NextInt32(15) - 7 + ridge;
                WriteAtlasPixel(atlas, tileIndex, px, py, ClampByte(104 + jitter), ClampByte(81 + jitter), ClampByte(48 + jitter), 255);
            }
        }
    }

    private static void PaintLogTopTile(byte[] atlas, ushort tileIndex)
    {
        for (var py = 0; py < TileDimension; py++)
        {
            for (var px = 0; px < TileDimension; px++)
            {
                var dist = Math.Max(Math.Abs(px - 7.5), Math.Abs(py - 7.5));
                if (dist >= 6.5)
                {
                    WriteAtlasPixel(atlas, tileIndex, px, py, 102, 79, 46, 255);
                }
                else
                {
                    var ring = ((int)Math.Round(dist) & 1) == 0 ? 174 : 154;
                    WriteAtlasPixel(atlas, tileIndex, px, py, (byte)ring, (byte)(ring - 32), (byte)(ring - 78), 255);
                }
            }
        }
    }

    private static void PaintLeavesTile(byte[] atlas, ushort tileIndex)
    {
        var rng = new Xoroshiro128PlusPlus(0x1EAF_7777UL);
        for (var py = 0; py < TileDimension; py++)
        {
            for (var px = 0; px < TileDimension; px++)
            {
                var cutout = rng.NextInt32(7) == 0;
                var jitter = rng.NextInt32(29) - 14;
                WriteAtlasPixel(
                    atlas,
                    tileIndex,
                    px,
                    py,
                    ClampByte(58 + jitter),
                    ClampByte(128 + jitter),
                    ClampByte(42 + jitter),
                    cutout ? (byte)0 : (byte)255);
            }
        }
    }

    private static void PaintPlanksTile(byte[] atlas, ushort tileIndex)
    {
        var rng = new Xoroshiro128PlusPlus(0x91A4_BEEFUL + tileIndex);
        for (var py = 0; py < TileDimension; py++)
        {
            for (var px = 0; px < TileDimension; px++)
            {
                var seam = (py % 4 == 0) || (px == ((py / 4) * 7 + 5) % 16);
                var delta = seam ? -28 : (rng.NextInt32(15) - 7);
                WriteAtlasPixel(atlas, tileIndex, px, py, ClampByte(166 + delta), ClampByte(132 + delta), ClampByte(78 + delta), 255);
            }
        }
    }

    private static void PaintCraftingTopTile(byte[] atlas, ushort tileIndex)
    {
        for (var py = 0; py < TileDimension; py++)
        {
            for (var px = 0; px < TileDimension; px++)
            {
                var border = px is 0 or 15 || py is 0 or 15;
                var gridLine = px is 5 or 10 || py is 5 or 10;
                if (border || gridLine)
                {
                    WriteAtlasPixel(atlas, tileIndex, px, py, 92, 64, 38, 255);
                }
                else
                {
                    WriteAtlasPixel(atlas, tileIndex, px, py, 176, 138, 86, 255);
                }
            }
        }
    }

    private static void WriteAtlasPixel(
        byte[] atlas,
        ushort tileIndex,
        int localX,
        int localY,
        byte r,
        byte g,
        byte b,
        byte a)
    {
        var tileX = (tileIndex & 15) * TileDimension;
        var tileY = (tileIndex >> 4) * TileDimension;
        var idx = (((tileY + localY) * AtlasDimension) + (tileX + localX)) * 4;
        atlas[idx] = r;
        atlas[idx + 1] = g;
        atlas[idx + 2] = b;
        atlas[idx + 3] = a;
    }

    private static byte ClampByte(int channel) => (byte)Math.Clamp(channel, 0, 255);
}
