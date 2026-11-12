using OverHorizon.Shared.Random;

namespace OverHorizon.Rendering.Textures;

/// <summary>
/// Clean-room 16x16 pixel-art procedural tile painters used by <see cref="ProceduralAtlasGenerator"/>.
/// </summary>
internal static class ProceduralBlockTilePainter
{
    private const int AtlasDimension = ProceduralAtlasGenerator.AtlasDimension;
    private const int TileDimension = ProceduralAtlasGenerator.TileDimension;

    public static void PaintNoiseTile(
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
                WritePixel(atlas, tileIndex, px, py, ClampByte(baseR + jitter), ClampByte(baseG + jitter), ClampByte(baseB + jitter), alpha);
            }
        }
    }

    public static void PaintGrassSideTile(byte[] atlas, ushort tileIndex)
    {
        var rng = new Xoroshiro128PlusPlus(0x6BA5_51DEUL);
        for (var py = 0; py < TileDimension; py++)
        {
            for (var px = 0; px < TileDimension; px++)
            {
                var overhang = 3 + (((px * 7) + 3) % 3);
                var jitter = rng.NextInt32(25) - 12;
                if (py < overhang)
                {
                    WritePixel(atlas, tileIndex, px, py, ClampByte(91 + jitter), ClampByte(158 + jitter), ClampByte(58 + jitter), 255);
                }
                else
                {
                    WritePixel(atlas, tileIndex, px, py, ClampByte(134 + jitter), ClampByte(96 + jitter), ClampByte(67 + jitter), 255);
                }
            }
        }
    }

    public static void PaintCobblestoneTile(byte[] atlas, ushort tileIndex, bool mossy)
    {
        var rng = new Xoroshiro128PlusPlus(mossy ? 0x4055_C0BBUL : 0xC0BB_1E00UL);
        for (var py = 0; py < TileDimension; py++)
        {
            for (var px = 0; px < TileDimension; px++)
            {
                var mortar = ((px + ((py >> 2) * 3)) % 5 == 0) || ((py + ((px >> 2) * 2)) % 5 == 0);
                if (mortar && mossy && rng.NextInt32(3) != 0)
                {
                    var mj = rng.NextInt32(20) - 10;
                    WritePixel(atlas, tileIndex, px, py, ClampByte(62 + mj), ClampByte(122 + mj), ClampByte(48 + mj), 255);
                    continue;
                }

                var baseVal = mortar ? 82 : 132 + (rng.NextInt32(26) - 13);
                var b = ClampByte(baseVal);
                WritePixel(atlas, tileIndex, px, py, b, b, b, 255);
            }
        }
    }

    public static void PaintStoneBricksTile(byte[] atlas, ushort tileIndex)
    {
        var rng = new Xoroshiro128PlusPlus(0x5704_EB1CUL);
        for (var py = 0; py < TileDimension; py++)
        {
            var row = py / 8;
            var offsetX = (row & 1) * 8;
            for (var px = 0; px < TileDimension; px++)
            {
                var mortar = (py % 8 == 0) || (((px + offsetX) % 16) == 0);
                var highlight = (py % 8 == 1) || (((px + offsetX) % 16) == 1);
                var baseVal = mortar ? 74 : (highlight ? 156 : 132) + (rng.NextInt32(15) - 7);
                var c = ClampByte(baseVal);
                WritePixel(atlas, tileIndex, px, py, c, c, ClampByte(baseVal + 3), 255);
            }
        }
    }

    public static void PaintClayBricksTile(byte[] atlas, ushort tileIndex)
    {
        var rng = new Xoroshiro128PlusPlus(0xB41C_4422UL);
        for (var py = 0; py < TileDimension; py++)
        {
            var row = py / 4;
            var shift = (row & 1) * 4;
            for (var px = 0; px < TileDimension; px++)
            {
                var mortar = (py % 4 == 0) || (((px + shift) % 8) == 0);
                if (mortar)
                {
                    WritePixel(atlas, tileIndex, px, py, 188, 182, 172, 255);
                }
                else
                {
                    var j = rng.NextInt32(21) - 10;
                    WritePixel(atlas, tileIndex, px, py, ClampByte(158 + j), ClampByte(74 + j), ClampByte(60 + j), 255);
                }
            }
        }
    }

    public static void PaintGlassTile(byte[] atlas, ushort tileIndex)
    {
        for (var py = 0; py < TileDimension; py++)
        {
            for (var px = 0; px < TileDimension; px++)
            {
                var border = px is 0 or 15 || py is 0 or 15;
                var glint = (px + py) is 4 or 5 && px is >= 2 and <= 5;
                var glint2 = (px + py) is 20 && px is >= 9 and <= 12;
                if (border)
                {
                    WritePixel(atlas, tileIndex, px, py, 205, 238, 248, 235);
                }
                else if (glint || glint2)
                {
                    WritePixel(atlas, tileIndex, px, py, 235, 250, 255, 205);
                }
                else
                {
                    WritePixel(atlas, tileIndex, px, py, 160, 215, 235, 36);
                }
            }
        }
    }

    public static void PaintSandstoneSideTile(byte[] atlas, ushort tileIndex)
    {
        var rng = new Xoroshiro128PlusPlus(0x5A4D_5704UL);
        for (var py = 0; py < TileDimension; py++)
        {
            var band = py < 3 ? 12 : (py is 3 or 11 ? -14 : 0);
            for (var px = 0; px < TileDimension; px++)
            {
                var j = rng.NextInt32(13) - 6 + band;
                WritePixel(atlas, tileIndex, px, py, ClampByte(218 + j), ClampByte(204 + j), ClampByte(152 + j), 255);
            }
        }
    }

    public static void PaintBookshelfSideTile(byte[] atlas, ushort tileIndex)
    {
        ReadOnlySpan<(byte R, byte G, byte B)> bookColors =
        [
            (168, 48, 44),
            (46, 118, 62),
            (48, 86, 164),
            (182, 142, 46),
            (126, 56, 146),
        ];

        for (var py = 0; py < TileDimension; py++)
        {
            var woodFrame = py is <= 1 or 7 or 8 or >= 14;
            for (var px = 0; px < TileDimension; px++)
            {
                if (woodFrame)
                {
                    WritePixel(atlas, tileIndex, px, py, 166, 132, 78, 255);
                    continue;
                }

                if (py is 2 or 9)
                {
                    WritePixel(atlas, tileIndex, px, py, 48, 36, 22, 255);
                    continue;
                }

                var bookIdx = ((px / 3) + (py > 8 ? 2 : 0)) % bookColors.Length;
                var spineEdge = (px % 3) == 0 ? -24 : 0;
                var c = bookColors[bookIdx];
                WritePixel(atlas, tileIndex, px, py, ClampByte(c.R + spineEdge), ClampByte(c.G + spineEdge), ClampByte(c.B + spineEdge), 255);
            }
        }
    }

    public static void PaintOreTile(byte[] atlas, ushort tileIndex, byte oreR, byte oreG, byte oreB, ulong seed)
    {
        PaintNoiseTile(atlas, tileIndex, 124, 124, 128, variation: 16, alpha: 255);
        var rng = new Xoroshiro128PlusPlus(seed);
        for (var cluster = 0; cluster < 6; cluster++)
        {
            var cx = 2 + rng.NextInt32(11);
            var cy = 2 + rng.NextInt32(11);
            WritePixel(atlas, tileIndex, cx, cy, oreR, oreG, oreB, 255);
            WritePixel(atlas, tileIndex, cx + 1, cy, ClampByte(oreR + 24), ClampByte(oreG + 24), ClampByte(oreB + 24), 255);
            WritePixel(atlas, tileIndex, cx, cy + 1, ClampByte(oreR - 20), ClampByte(oreG - 20), ClampByte(oreB - 20), 255);
        }
    }

    public static void PaintLogSideTile(byte[] atlas, ushort tileIndex)
    {
        var rng = new Xoroshiro128PlusPlus(0x1065_1DE0UL);
        for (var py = 0; py < TileDimension; py++)
        {
            for (var px = 0; px < TileDimension; px++)
            {
                var ridge = (px % 4 == 0) ? -16 : 6;
                var jitter = rng.NextInt32(15) - 7 + ridge;
                WritePixel(atlas, tileIndex, px, py, ClampByte(104 + jitter), ClampByte(81 + jitter), ClampByte(48 + jitter), 255);
            }
        }
    }

    public static void PaintLogTopTile(byte[] atlas, ushort tileIndex)
    {
        for (var py = 0; py < TileDimension; py++)
        {
            for (var px = 0; px < TileDimension; px++)
            {
                var dist = Math.Max(Math.Abs(px - 7.5), Math.Abs(py - 7.5));
                if (dist >= 6.5)
                {
                    WritePixel(atlas, tileIndex, px, py, 102, 79, 46, 255);
                }
                else
                {
                    var ring = ((int)Math.Round(dist) & 1) == 0 ? 174 : 154;
                    WritePixel(atlas, tileIndex, px, py, (byte)ring, (byte)(ring - 32), (byte)(ring - 78), 255);
                }
            }
        }
    }

    public static void PaintLeavesTile(byte[] atlas, ushort tileIndex)
    {
        var rng = new Xoroshiro128PlusPlus(0x1EAF_7777UL);
        for (var py = 0; py < TileDimension; py++)
        {
            for (var px = 0; px < TileDimension; px++)
            {
                var cutout = rng.NextInt32(7) == 0;
                var jitter = rng.NextInt32(29) - 14;
                WritePixel(atlas, tileIndex, px, py, ClampByte(58 + jitter), ClampByte(128 + jitter), ClampByte(42 + jitter), cutout ? (byte)0 : (byte)255);
            }
        }
    }

    public static void PaintPlanksTile(byte[] atlas, ushort tileIndex)
    {
        var rng = new Xoroshiro128PlusPlus(0x91A4_BEEFUL + tileIndex);
        for (var py = 0; py < TileDimension; py++)
        {
            for (var px = 0; px < TileDimension; px++)
            {
                var seam = (py % 4 == 0) || (px == (((py / 4) * 7) + 5) % 16);
                var delta = seam ? -28 : (rng.NextInt32(15) - 7);
                WritePixel(atlas, tileIndex, px, py, ClampByte(166 + delta), ClampByte(132 + delta), ClampByte(78 + delta), 255);
            }
        }
    }

    public static void PaintCraftingTopTile(byte[] atlas, ushort tileIndex)
    {
        for (var py = 0; py < TileDimension; py++)
        {
            for (var px = 0; px < TileDimension; px++)
            {
                var border = px is 0 or 15 || py is 0 or 15;
                var gridLine = px is 5 or 10 || py is 5 or 10;
                if (border || gridLine)
                {
                    WritePixel(atlas, tileIndex, px, py, 92, 64, 38, 255);
                }
                else
                {
                    WritePixel(atlas, tileIndex, px, py, 176, 138, 86, 255);
                }
            }
        }
    }

    private static void WritePixel(byte[] atlas, ushort tileIndex, int localX, int localY, byte r, byte g, byte b, byte a)
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
