using System.Collections.Frozen;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;

namespace OverHorizon.Rendering.Textures;

/// <summary>
/// Generates a <c>512x256</c> RGBA8 entity skin sheet (<c>8x4</c> grid of <c>64x64</c> cells) containing
/// Steve, all 8 mobs, 4 full 3D Armor sets (<c>layer_1</c> + <c>layer_2</c>), <c>Minecart</c>, and <c>Arrow</c>
/// from <c>23w13a__or__b</c> with clean-room procedural pixel-art fallbacks when offline.
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

    /// <summary>Cell index for Leather Armor Layer 1 (Helmet, Tunic, Boots — <c>64x32</c>).</summary>
    public const int CellLeatherArmorLayer1 = 10;

    /// <summary>Cell index for Leather Armor Layer 2 (Leggings — <c>64x32</c>).</summary>
    public const int CellLeatherArmorLayer2 = 11;

    /// <summary>Cell index for Iron Armor Layer 1 (Helmet, Chestplate, Boots — <c>64x32</c>).</summary>
    public const int CellIronArmorLayer1 = 12;

    /// <summary>Cell index for Iron Armor Layer 2 (Leggings — <c>64x32</c>).</summary>
    public const int CellIronArmorLayer2 = 13;

    /// <summary>Cell index for Golden Armor Layer 1 (Helmet, Chestplate, Boots — <c>64x32</c>).</summary>
    public const int CellGoldArmorLayer1 = 14;

    /// <summary>Cell index for Golden Armor Layer 2 (Leggings — <c>64x32</c>).</summary>
    public const int CellGoldArmorLayer2 = 15;

    /// <summary>Cell index for Diamond Armor Layer 1 (Helmet, Chestplate, Boots — <c>64x32</c>).</summary>
    public const int CellDiamondArmorLayer1 = 16;

    /// <summary>Cell index for Diamond Armor Layer 2 (Leggings — <c>64x32</c>).</summary>
    public const int CellDiamondArmorLayer2 = 17;

    /// <summary>Cell index for 3D <c>entity/minecart</c> (<c>64x32</c>).</summary>
    public const int CellMinecart = 18;

    /// <summary>Cell index for 3D <c>entity/projectiles/arrow</c> (<c>32x32</c>).</summary>
    public const int CellArrow = 19;

    /// <summary>Cell index for 3D <c>entity/shield_base_nopattern</c> (<c>64x64</c>).</summary>
    public const int CellShield = 20;

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

    /// <summary>Resolves the entity atlas cell index for armor tier (<c>1=Leather, 2=Iron, 3=Gold, 4=Diamond</c>) and layer.</summary>
    public static int GetArmorCell(byte tierCode, bool isLeggingsLayer2) => tierCode switch
    {
        1 => isLeggingsLayer2 ? CellLeatherArmorLayer2 : CellLeatherArmorLayer1,
        2 => isLeggingsLayer2 ? CellIronArmorLayer2 : CellIronArmorLayer1,
        3 => isLeggingsLayer2 ? CellGoldArmorLayer2 : CellGoldArmorLayer1,
        4 => isLeggingsLayer2 ? CellDiamondArmorLayer2 : CellDiamondArmorLayer1,
        _ => -1,
    };

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

        TryBlitTintedArmorLayer(atlas, pack, CellLeatherArmorLayer1, "models/armor/leather_layer_1", "models/armor/leather_layer_1_overlay", 160, 101, 64);
        TryBlitTintedArmorLayer(atlas, pack, CellLeatherArmorLayer2, "models/armor/leather_layer_2", "models/armor/leather_layer_2_overlay", 160, 101, 64);
        TryBlitEntitySkin(atlas, pack, CellIronArmorLayer1, "models/armor/iron_layer_1");
        TryBlitEntitySkin(atlas, pack, CellIronArmorLayer2, "models/armor/iron_layer_2");
        TryBlitEntitySkin(atlas, pack, CellGoldArmorLayer1, "models/armor/gold_layer_1");
        TryBlitEntitySkin(atlas, pack, CellGoldArmorLayer2, "models/armor/gold_layer_2");
        TryBlitEntitySkin(atlas, pack, CellDiamondArmorLayer1, "models/armor/diamond_layer_1");
        TryBlitEntitySkin(atlas, pack, CellDiamondArmorLayer2, "models/armor/diamond_layer_2");

        TryBlitEntitySkin(atlas, pack, CellMinecart, "minecart");
        TryBlitArrowSkin(atlas, pack, CellArrow, "projectiles/arrow");
        TryBlitEntitySkin(atlas, pack, CellShield, "shield_base_nopattern");

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

    private static void TryBlitTintedArmorLayer(byte[] atlas, ResourcePackLoader pack, int cell, string baseKey, string overlayKey, byte tr, byte tg, byte tb)
    {
        if (!pack.TryGetEntityPng(baseKey, out var png) ||
            !PngImageDecoder.TryDecodeRgba(png, out var srcW, out var srcH, out var rgba) ||
            srcW <= 0 || srcH <= 0)
        {
            return;
        }

        byte[]? ovRgba = null;
        var ovW = 0;
        var ovH = 0;
        if (pack.TryGetEntityPng(overlayKey, out var ovPng))
        {
            _ = PngImageDecoder.TryDecodeRgba(ovPng, out ovW, out ovH, out ovRgba);
        }

        var cellX = (cell & 7) * 64;
        var cellY = (cell >> 3) * 64;
        for (var py = 0; py < 32; py++)
        {
            for (var px = 0; px < 64; px++)
            {
                var sx = Math.Clamp((px * srcW) / 64, 0, srcW - 1);
                var sy = Math.Clamp((py * srcH) / 32, 0, srcH - 1);
                var sIdx = ((sy * srcW) + sx) * 4;
                var dIdx = (((cellY + py) * Width) + cellX + px) * 4;
                atlas[dIdx + 0] = (byte)((rgba[sIdx + 0] * tr) / 255);
                atlas[dIdx + 1] = (byte)((rgba[sIdx + 1] * tg) / 255);
                atlas[dIdx + 2] = (byte)((rgba[sIdx + 2] * tb) / 255);
                atlas[dIdx + 3] = rgba[sIdx + 3];

                if (ovRgba is not null && ovW > 0 && ovH > 0)
                {
                    var ox = Math.Clamp((px * ovW) / 64, 0, ovW - 1);
                    var oy = Math.Clamp((py * ovH) / 32, 0, ovH - 1);
                    var oIdx = ((oy * ovW) + ox) * 4;
                    if (ovRgba[oIdx + 3] >= 32)
                    {
                        atlas[dIdx + 0] = ovRgba[oIdx + 0];
                        atlas[dIdx + 1] = ovRgba[oIdx + 1];
                        atlas[dIdx + 2] = ovRgba[oIdx + 2];
                        atlas[dIdx + 3] = ovRgba[oIdx + 3];
                    }
                }
            }
        }
    }

    private static void TryBlitArrowSkin(byte[] atlas, ResourcePackLoader pack, int cell, string key)
    {
        if (!pack.TryGetEntityPng(key, out var png) ||
            !PngImageDecoder.TryDecodeRgba(png, out var srcW, out var srcH, out var rgba) ||
            srcW <= 0 || srcH <= 0)
        {
            return;
        }

        var cellX = (cell & 7) * 64;
        var cellY = (cell >> 3) * 64;
        for (var py = 0; py < 32; py++)
        {
            for (var px = 0; px < 32; px++)
            {
                var sx = Math.Clamp((px * srcW) / 32, 0, srcW - 1);
                var sy = Math.Clamp((py * srcH) / 32, 0, srcH - 1);
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

        FillCellRect(atlas, CellZombie, 0, 0, 32, 16, 86, 142, 76);
        FillCellRect(atlas, CellZombie, 9, 12, 2, 1, 18, 24, 18);
        FillCellRect(atlas, CellZombie, 13, 12, 2, 1, 18, 24, 18);
        FillCellRect(atlas, CellZombie, 16, 16, 24, 16, 42, 132, 146);
        FillCellRect(atlas, CellZombie, 40, 16, 16, 16, 86, 142, 76);
        FillCellRect(atlas, CellZombie, 0, 16, 16, 16, 56, 52, 124);

        FillCellRect(atlas, CellSkeleton, 0, 0, 64, 32, 214, 212, 202);
        FillCellRect(atlas, CellSkeleton, 9, 11, 2, 2, 28, 28, 32);
        FillCellRect(atlas, CellSkeleton, 13, 11, 2, 2, 28, 28, 32);
        FillCellRect(atlas, CellSkeleton, 10, 14, 4, 1, 28, 28, 32);

        FillCellRect(atlas, CellCreeper, 0, 0, 64, 32, 78, 186, 68);
        FillCellRect(atlas, CellCreeper, 9, 10, 2, 2, 16, 22, 16);
        FillCellRect(atlas, CellCreeper, 13, 10, 2, 2, 16, 22, 16);
        FillCellRect(atlas, CellCreeper, 11, 12, 2, 3, 16, 22, 16);
        FillCellRect(atlas, CellCreeper, 10, 13, 4, 2, 16, 22, 16);

        FillCellRect(atlas, CellSpider, 0, 0, 64, 32, 46, 40, 48);
        FillCellRect(atlas, CellSpider, 41, 14, 2, 2, 232, 28, 32);
        FillCellRect(atlas, CellSpider, 45, 14, 2, 2, 232, 28, 32);

        FillCellRect(atlas, CellPig, 0, 0, 64, 32, 238, 164, 166);
        FillCellRect(atlas, CellPig, 8, 11, 2, 1, 32, 28, 32);
        FillCellRect(atlas, CellPig, 14, 11, 2, 1, 32, 28, 32);
        FillCellRect(atlas, CellPig, 16, 16, 8, 6, 216, 126, 134);

        FillCellRect(atlas, CellCow, 0, 0, 64, 32, 84, 58, 42);
        FillCellRect(atlas, CellCow, 24, 8, 14, 10, 226, 222, 214);
        FillCellRect(atlas, CellCow, 22, 0, 4, 4, 196, 190, 172);

        FillCellRect(atlas, CellSheep, 0, 0, 64, 32, 222, 186, 172);
        FillCellRect(atlas, CellSheepFur, 0, 0, 64, 32, 242, 242, 246);

        FillCellRect(atlas, CellChicken, 0, 0, 64, 32, 246, 246, 250);
        FillCellRect(atlas, CellChicken, 14, 0, 8, 4, 234, 174, 42);
        FillCellRect(atlas, CellChicken, 14, 4, 6, 4, 222, 38, 42);
        FillCellRect(atlas, CellChicken, 26, 0, 12, 8, 234, 174, 42);

        PaintProceduralArmorPair(atlas, CellLeatherArmorLayer1, CellLeatherArmorLayer2, 160, 101, 64);
        PaintProceduralArmorPair(atlas, CellIronArmorLayer1, CellIronArmorLayer2, 218, 222, 228);
        PaintProceduralArmorPair(atlas, CellGoldArmorLayer1, CellGoldArmorLayer2, 246, 208, 56);
        PaintProceduralArmorPair(atlas, CellDiamondArmorLayer1, CellDiamondArmorLayer2, 74, 234, 224);

        FillCellRect(atlas, CellMinecart, 0, 0, 64, 32, 146, 150, 158);
        FillCellRect(atlas, CellMinecart, 2, 12, 36, 16, 96, 100, 108);

        FillCellRect(atlas, CellArrow, 0, 1, 14, 3, 158, 114, 62);
        FillCellRect(atlas, CellArrow, 13, 0, 3, 5, 204, 210, 218);
        FillCellRect(atlas, CellArrow, 0, 0, 3, 5, 242, 244, 248);

        // Shield plate [0..25, 0..23] + handle [26..37, 0..11] in 64x64 layout
        FillCellRect(atlas, CellShield, 0, 0, 26, 24, 156, 116, 68);
        FillCellRect(atlas, CellShield, 1, 1, 12, 1, 188, 194, 202);
        FillCellRect(atlas, CellShield, 1, 22, 12, 1, 188, 194, 202);
        FillCellRect(atlas, CellShield, 1, 1, 1, 22, 188, 194, 202);
        FillCellRect(atlas, CellShield, 12, 1, 1, 22, 188, 194, 202);
        FillCellRect(atlas, CellShield, 5, 10, 4, 4, 198, 204, 212);
        FillCellRect(atlas, CellShield, 26, 0, 12, 12, 118, 86, 48);
    }

    private static void PaintProceduralArmorPair(byte[] atlas, int cellL1, int cellL2, byte r, byte g, byte b)
    {
        // Layer 1: Helmet (0..31, 0..15) with open face visor at (9..14, 11..15), Chest+Arms (16..55, 16..27), Boots (0..15, 26..31)
        FillCellRect(atlas, cellL1, 0, 0, 32, 16, r, g, b);
        ClearCellRect(atlas, cellL1, 9, 11, 6, 5);
        FillCellRect(atlas, cellL1, 16, 16, 24, 16, r, g, b);
        FillCellRect(atlas, cellL1, 40, 16, 16, 11, r, g, b);
        FillCellRect(atlas, cellL1, 0, 26, 16, 6, r, g, b);

        // Layer 2: Leggings pelvis (16..39, 16..31) & legs (0..15, 16..28)
        FillCellRect(atlas, cellL2, 16, 16, 24, 16, (byte)(r * 9 / 10), (byte)(g * 9 / 10), (byte)(b * 9 / 10));
        FillCellRect(atlas, cellL2, 0, 16, 16, 12, (byte)(r * 9 / 10), (byte)(g * 9 / 10), (byte)(b * 9 / 10));
    }

    private static void ClearCellRect(byte[] atlas, int cell, int rx, int ry, int rw, int rh)
    {
        var cx = (cell & 7) * 64;
        var cy = (cell >> 3) * 64;
        for (var y = ry; y < ry + rh && y < 64; y++)
        {
            for (var x = rx; x < rx + rw && x < 64; x++)
            {
                var idx = (((cy + y) * Width) + cx + x) * 4;
                atlas[idx + 3] = 0;
            }
        }
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
