using System.Collections.Frozen;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;

namespace OverHorizon.Rendering.Textures;

/// <summary>
/// Generates a <c>512x256</c> RGBA8 entity skin sheet (<c>8x4</c> grid of <c>64x64</c> cells) containing
/// Steve (<c>player/wide/steve.png</c>) and all 8 peaceful/hostile mob skins from <c>23w13a__or__b</c>
/// with clean-room procedural pixel-art fallbacks when offline.
/// </summary>
public static class EntityTextureAtlasGenerator
{
    /// <summary>Atlas width in pixels (<c>512</c> = 8 cells of <c>64px</c>).</summary>
    public const int Width = 512;

    /// <summary>Atlas height in pixels (<c>256</c> = 4 cells of <c>64px</c>).</summary>
    public const int Height = 256;

    /// <summary>Cell index for <c>player/wide/steve</c> (<c>64x64</c>).</summary>
    public const int CellSteve = 0;

    /// <summary>Cell index for <c>zombie/zombie</c> (<c>64x64</c>).</summary>
    public const int CellZombie = 1;

    /// <summary>Cell index for <c>skeleton/skeleton</c> (<c>64x32</c>).</summary>
    public const int CellSkeleton = 2;

    /// <summary>Cell index for <c>creeper/creeper</c> (<c>64x32</c>).</summary>
    public const int CellCreeper = 3;

    /// <summary>Cell index for <c>spider/spider</c> (<c>64x32</c>).</summary>
    public const int CellSpider = 4;

    /// <summary>Cell index for <c>pig/pig</c> (<c>64x32</c>).</summary>
    public const int CellPig = 5;

    /// <summary>Cell index for <c>cow/cow</c> (<c>64x32</c>).</summary>
    public const int CellCow = 6;

    /// <summary>Cell index for <c>sheep/sheep</c> (<c>64x32</c>).</summary>
    public const int CellSheep = 7;

    /// <summary>Cell index for <c>sheep/sheep_fur</c> (<c>64x32</c>).</summary>
    public const int CellSheepFur = 8;

    /// <summary>Cell index for <c>chicken</c> (<c>64x32</c>).</summary>
    public const int CellChicken = 9;

    /// <summary>Creates the default <c>512x256</c> <see cref="TextureAtlasData"/> entity skin sheet.</summary>
    public static TextureAtlasData CreateDefault(string? externalAssetsPath = null) =>
        new(
            Width,
            Height,
            64,
            Generate(externalAssetsPath),
            FrozenDictionary<(ResourceKey BlockKey, Direction Face), ushort>.Empty,
            FrozenDictionary<ResourceKey, ushort>.Empty,
            0);

    /// <summary>Generates the <c>512x256</c> RGBA8 entity texture atlas.</summary>
    public static byte[] Generate(string? externalAssetsPath = null)
    {
        var atlas = new byte[Width * Height * 4];
        PaintProceduralFallbacks(atlas);

        using var pack = ResourcePackLoader.Discover(externalAssetsPath);
        TryBlitEntitySkin(atlas, pack, CellSteve, "player/wide/steve");
        TryBlitEntitySkin(atlas, pack, CellZombie, "zombie/zombie");
        TryBlitEntitySkin(atlas, pack, CellSkeleton, "skeleton/skeleton");
        TryBlitEntitySkin(atlas, pack, CellCreeper, "creeper/creeper");
        TryBlitEntitySkin(atlas, pack, CellSpider, "spider/spider");
        TryBlitEntitySkin(atlas, pack, CellPig, "pig/pig");
        TryBlitEntitySkin(atlas, pack, CellCow, "cow/cow");
        TryBlitEntitySkin(atlas, pack, CellSheep, "sheep/sheep");
        TryBlitEntitySkin(atlas, pack, CellSheepFur, "sheep/sheep_fur");
        TryBlitEntitySkin(atlas, pack, CellChicken, "chicken");

        return atlas;
    }

    private static void TryBlitEntitySkin(byte[] atlas, ResourcePackLoader pack, int cell, string key)
    {
        if (!pack.TryGetEntityPng(key, out var png) ||
            !PngImageDecoder.TryDecodeRgba(png, out var srcW, out var srcH, out var rgba) ||
            srcW <= 0 || srcH <= 0)
        {
            return;
        }

        var cellX = (cell & 7) * 64;
        var cellY = (cell >> 3) * 64;
        var dstH = srcH * 2 <= srcW ? 32 : 64;

        // Clear target region before blitting real PNG so transparent hat/ribcage texels stay transparent!
        for (var py = 0; py < dstH; py++)
        {
            for (var px = 0; px < 64; px++)
            {
                var sx = Math.Clamp((px * srcW) / 64, 0, srcW - 1);
                var sy = Math.Clamp((py * srcH) / dstH, 0, srcH - 1);
                var sIdx = ((sy * srcW) + sx) * 4;
                var dIdx = (((cellY + py) * Width) + cellX + px) * 4;
                atlas[dIdx + 0] = rgba[sIdx + 0];
                atlas[dIdx + 1] = rgba[sIdx + 1];
                atlas[dIdx + 2] = rgba[sIdx + 2];
                atlas[dIdx + 3] = rgba[sIdx + 3];
            }
        }
    }

    private static void PaintProceduralFallbacks(byte[] atlas)
    {
        // Cell 0: Steve (skin head, brown hair, cyan shirt, indigo pants)
        FillCellRect(atlas, CellSteve, 0, 0, 32, 16, 198, 150, 118);
        FillCellRect(atlas, CellSteve, 8, 0, 8, 8, 68, 44, 26);
        FillCellRect(atlas, CellSteve, 8, 8, 8, 3, 68, 44, 26);
        FillCellRect(atlas, CellSteve, 9, 12, 2, 1, 52, 68, 156);
        FillCellRect(atlas, CellSteve, 13, 12, 2, 1, 52, 68, 156);
        FillCellRect(atlas, CellSteve, 16, 16, 24, 16, 38, 156, 174);
        FillCellRect(atlas, CellSteve, 40, 16, 16, 16, 198, 150, 118);
        FillCellRect(atlas, CellSteve, 40, 20, 16, 4, 38, 156, 174);
        FillCellRect(atlas, CellSteve, 32, 48, 16, 16, 198, 150, 118);
        FillCellRect(atlas, CellSteve, 32, 52, 16, 4, 38, 156, 174);
        FillCellRect(atlas, CellSteve, 0, 16, 16, 16, 56, 54, 138);
        FillCellRect(atlas, CellSteve, 16, 48, 16, 16, 56, 54, 138);

        // Cell 1: Zombie
        FillCellRect(atlas, CellZombie, 0, 0, 32, 16, 86, 142, 76);
        FillCellRect(atlas, CellZombie, 9, 12, 2, 1, 18, 24, 18);
        FillCellRect(atlas, CellZombie, 13, 12, 2, 1, 18, 24, 18);
        FillCellRect(atlas, CellZombie, 16, 16, 24, 16, 42, 132, 146);
        FillCellRect(atlas, CellZombie, 40, 16, 16, 16, 86, 142, 76);
        FillCellRect(atlas, CellZombie, 0, 16, 16, 16, 56, 52, 124);

        // Cell 2: Skeleton
        FillCellRect(atlas, CellSkeleton, 0, 0, 64, 32, 214, 212, 202);
        FillCellRect(atlas, CellSkeleton, 9, 11, 2, 2, 28, 28, 32);
        FillCellRect(atlas, CellSkeleton, 13, 11, 2, 2, 28, 28, 32);
        FillCellRect(atlas, CellSkeleton, 10, 14, 4, 1, 28, 28, 32);

        // Cell 3: Creeper
        FillCellRect(atlas, CellCreeper, 0, 0, 64, 32, 78, 186, 68);
        FillCellRect(atlas, CellCreeper, 9, 10, 2, 2, 16, 22, 16);
        FillCellRect(atlas, CellCreeper, 13, 10, 2, 2, 16, 22, 16);
        FillCellRect(atlas, CellCreeper, 11, 12, 2, 3, 16, 22, 16);
        FillCellRect(atlas, CellCreeper, 10, 13, 4, 2, 16, 22, 16);

        // Cell 4: Spider
        FillCellRect(atlas, CellSpider, 0, 0, 64, 32, 46, 40, 48);
        FillCellRect(atlas, CellSpider, 41, 14, 2, 2, 232, 28, 32);
        FillCellRect(atlas, CellSpider, 45, 14, 2, 2, 232, 28, 32);

        // Cell 5: Pig
        FillCellRect(atlas, CellPig, 0, 0, 64, 32, 238, 164, 166);
        FillCellRect(atlas, CellPig, 8, 11, 2, 1, 32, 28, 32);
        FillCellRect(atlas, CellPig, 14, 11, 2, 1, 32, 28, 32);
        FillCellRect(atlas, CellPig, 16, 16, 8, 6, 216, 126, 134);

        // Cell 6: Cow
        FillCellRect(atlas, CellCow, 0, 0, 64, 32, 84, 58, 42);
        FillCellRect(atlas, CellCow, 24, 8, 14, 10, 226, 222, 214);
        FillCellRect(atlas, CellCow, 22, 0, 4, 4, 196, 190, 172);

        // Cell 7 & 8: Sheep + Sheep Fur
        FillCellRect(atlas, CellSheep, 0, 0, 64, 32, 222, 186, 172);
        FillCellRect(atlas, CellSheepFur, 0, 0, 64, 32, 242, 242, 246);

        // Cell 9: Chicken
        FillCellRect(atlas, CellChicken, 0, 0, 64, 32, 246, 246, 250);
        FillCellRect(atlas, CellChicken, 14, 0, 8, 4, 234, 174, 42);
        FillCellRect(atlas, CellChicken, 14, 4, 6, 4, 222, 38, 42);
        FillCellRect(atlas, CellChicken, 26, 0, 12, 8, 234, 174, 42);
    }

    private static void FillCellRect(byte[] atlas, int cell, int rx, int ry, int rw, int rh, byte r, byte g, byte b)
    {
        var cx = (cell & 7) * 64;
        var cy = (cell >> 3) * 64;
        for (var y = ry; y < ry + rh && y < 64; y++)
        {
            for (var x = rx; x < rx + rw && x < 64; x++)
            {
                var n = (((x * 37) + (y * 19) + (cell * 11)) % 13) - 6;
                var idx = (((cy + y) * Width) + cx + x) * 4;
                atlas[idx + 0] = (byte)Math.Clamp(r + n, 0, 255);
                atlas[idx + 1] = (byte)Math.Clamp(g + n, 0, 255);
                atlas[idx + 2] = (byte)Math.Clamp(b + n, 0, 255);
                atlas[idx + 3] = 255;
            }
        }
    }
}
