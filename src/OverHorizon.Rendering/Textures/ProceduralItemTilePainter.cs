namespace OverHorizon.Rendering.Textures;

/// <summary>
/// Clean-room <c>16x16</c> pixel-art item sprite painter providing baseline transparent-background icons
/// for weapons, tools, buckets, food, and ingots (overridden by <c>23w13a__or__b</c> PNGs when loaded).
/// </summary>
internal static class ProceduralItemTilePainter
{
    public static void PaintSword(byte[] atlas, ushort tile, byte r, byte g, byte b)
    {
        ClearTile(atlas, tile);
        for (var i = 0; i < 4; i++)
        {
            SetPx(atlas, tile, 2 + i, 13 - i, 118, 82, 46);
        }

        for (var d = -2; d <= 2; d++)
        {
            SetPx(atlas, tile, 5 + d, 10 + d, (byte)(r * 3 / 4), (byte)(g * 3 / 4), (byte)(b * 3 / 4));
        }

        for (var i = 0; i < 8; i++)
        {
            SetPx(atlas, tile, 6 + i, 9 - i, r, g, b);
            SetPx(atlas, tile, 7 + i, 9 - i, (byte)Math.Min(255, r + 28), (byte)Math.Min(255, g + 28), (byte)Math.Min(255, b + 28));
        }
    }

    public static void PaintPickaxe(byte[] atlas, ushort tile, byte r, byte g, byte b)
    {
        ClearTile(atlas, tile);
        for (var i = 0; i < 10; i++)
        {
            SetPx(atlas, tile, 2 + i, 13 - i, 128, 88, 48);
        }

        for (var dx = 7; dx <= 13; dx++)
        {
            SetPx(atlas, tile, dx, 2, r, g, b);
            SetPx(atlas, tile, dx, 3, r, g, b);
        }

        for (var dy = 3; dy <= 8; dy++)
        {
            SetPx(atlas, tile, 13, dy, r, g, b);
            SetPx(atlas, tile, 12, dy, r, g, b);
        }
    }

    public static void PaintAxe(byte[] atlas, ushort tile, byte r, byte g, byte b)
    {
        ClearTile(atlas, tile);
        for (var i = 0; i < 10; i++)
        {
            SetPx(atlas, tile, 3 + i, 13 - i, 128, 88, 48);
        }

        for (var y = 2; y <= 7; y++)
        {
            for (var x = 8; x <= 13; x++)
            {
                if (x + y <= 18)
                {
                    SetPx(atlas, tile, x, y, r, g, b);
                }
            }
        }
    }

    public static void PaintShovel(byte[] atlas, ushort tile, byte r, byte g, byte b)
    {
        ClearTile(atlas, tile);
        for (var i = 0; i < 9; i++)
        {
            SetPx(atlas, tile, 2 + i, 13 - i, 128, 88, 48);
        }

        for (var y = 2; y <= 6; y++)
        {
            for (var x = 9; x <= 13; x++)
            {
                SetPx(atlas, tile, x, y, r, g, b);
            }
        }
    }

    public static void PaintStick(byte[] atlas, ushort tile)
    {
        ClearTile(atlas, tile);
        for (var i = 0; i < 11; i++)
        {
            SetPx(atlas, tile, 3 + i, 13 - i, 146, 102, 56);
            SetPx(atlas, tile, 3 + i, 12 - i, 112, 76, 40);
        }
    }

    public static void PaintDiscOrGem(byte[] atlas, ushort tile, byte r, byte g, byte b)
    {
        ClearTile(atlas, tile);
        for (var y = 3; y <= 12; y++)
        {
            for (var x = 4; x <= 11; x++)
            {
                var dx = x - 7.5f;
                var dy = y - 7.5f;
                if ((dx * dx) + (dy * dy) <= 18.5f)
                {
                    var hi = (x < 7 && y < 7) ? 32 : 0;
                    SetPx(
                        atlas,
                        tile,
                        x,
                        y,
                        (byte)Math.Min(255, r + hi),
                        (byte)Math.Min(255, g + hi),
                        (byte)Math.Min(255, b + hi));
                }
            }
        }
    }

    public static void PaintIngot(byte[] atlas, ushort tile, byte r, byte g, byte b)
    {
        ClearTile(atlas, tile);
        for (var y = 5; y <= 10; y++)
        {
            for (var x = 3; x <= 12; x++)
            {
                var hi = y <= 6 ? 26 : -14;
                SetPx(
                    atlas,
                    tile,
                    x,
                    y,
                    (byte)Math.Clamp(r + hi, 0, 255),
                    (byte)Math.Clamp(g + hi, 0, 255),
                    (byte)Math.Clamp(b + hi, 0, 255));
            }
        }
    }

    public static void PaintBucket(byte[] atlas, ushort tile, bool filledWithWater)
    {
        ClearTile(atlas, tile);
        for (var y = 4; y <= 12; y++)
        {
            for (var x = 4; x <= 11; x++)
            {
                if (y >= 11 && (x == 4 || x == 11))
                {
                    continue;
                }

                if (filledWithWater && y is 4 or 5 && x is >= 5 and <= 10)
                {
                    SetPx(atlas, tile, x, y, 48, 124, 240);
                }
                else
                {
                    SetPx(atlas, tile, x, y, 186, 192, 198);
                }
            }
        }
    }

    private static void ClearTile(byte[] atlas, ushort tile)
    {
        var tx = (tile % ProceduralAtlasGenerator.TilesPerRow) * 16;
        var ty = (tile / ProceduralAtlasGenerator.TilesPerRow) * 16;
        for (var py = 0; py < 16; py++)
        {
            var dst = (((ty + py) * ProceduralAtlasGenerator.AtlasDimension) + tx) * 4;
            Array.Clear(atlas, dst, 16 * 4);
        }
    }

    private static void SetPx(byte[] atlas, ushort tile, int px, int py, byte r, byte g, byte b)
    {
        if ((uint)px >= 16 || (uint)py >= 16)
        {
            return;
        }

        var tx = (tile % ProceduralAtlasGenerator.TilesPerRow) * 16;
        var ty = (tile / ProceduralAtlasGenerator.TilesPerRow) * 16;
        var dst = (((ty + py) * ProceduralAtlasGenerator.AtlasDimension) + tx + px) * 4;
        atlas[dst + 0] = r;
        atlas[dst + 1] = g;
        atlas[dst + 2] = b;
        atlas[dst + 3] = 255;
    }
}
