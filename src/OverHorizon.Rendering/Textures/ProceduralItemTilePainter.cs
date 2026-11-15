namespace OverHorizon.Rendering.Textures;

/// <summary>
/// Clean-room <c>16x16</c> pixel-art item &amp; cutout block sprite painter providing baseline transparent-background icons
/// for weapons, tools, armor, torches, ladders, rails, cobwebs, buckets, food, and ingots.
/// </summary>
internal static partial class ProceduralItemTilePainter
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

    public static void PaintHoe(byte[] atlas, ushort tile, byte r, byte g, byte b)
    {
        ClearTile(atlas, tile);
        for (var i = 0; i < 10; i++)
        {
            SetPx(atlas, tile, 3 + i, 13 - i, 128, 88, 48);
        }

        for (var x = 8; x <= 13; x++)
        {
            SetPx(atlas, tile, x, 2, r, g, b);
            SetPx(atlas, tile, x, 3, r, g, b);
        }
    }

    public static void PaintArmor(byte[] atlas, ushort tile, byte r, byte g, byte b, int slotKind)
    {
        ClearTile(atlas, tile);
        for (var y = 3; y <= 13; y++)
        {
            for (var x = 3; x <= 12; x++)
            {
                var draw = slotKind switch
                {
                    0 => y <= 9 && (y <= 5 || x <= 5 || x >= 10),
                    1 => y >= 4 && !(y <= 5 && x is >= 6 and <= 9),
                    2 => y >= 4 && (y <= 6 || x <= 6 || x >= 9),
                    _ => y >= 8 && (x <= 6 || x >= 9),
                };
                if (draw)
                {
                    var shade = (x < 6 || y < 6) ? 18 : 0;
                    SetPx(atlas, tile, x, y, (byte)Math.Min(255, r + shade), (byte)Math.Min(255, g + shade), (byte)Math.Min(255, b + shade));
                }
            }
        }
    }

    public static void PaintTorchTile(byte[] atlas, ushort tile)
    {
        ClearTile(atlas, tile);
        for (var y = 8; y <= 15; y++)
        {
            SetPx(atlas, tile, 7, y, 138, 98, 52);
            SetPx(atlas, tile, 8, y, 112, 78, 40);
        }

        SetPx(atlas, tile, 7, 7, 255, 198, 58);
        SetPx(atlas, tile, 8, 7, 255, 168, 36);
        SetPx(atlas, tile, 7, 6, 255, 246, 162);
        SetPx(atlas, tile, 8, 6, 255, 224, 96);
    }

    public static void PaintLadderTile(byte[] atlas, ushort tile)
    {
        ClearTile(atlas, tile);
        for (var y = 0; y < 16; y++)
        {
            SetPx(atlas, tile, 2, y, 146, 108, 60);
            SetPx(atlas, tile, 3, y, 126, 90, 48);
            SetPx(atlas, tile, 12, y, 146, 108, 60);
            SetPx(atlas, tile, 13, y, 126, 90, 48);
            if (y % 4 == 1 || y % 4 == 2)
            {
                for (var x = 2; x <= 13; x++)
                {
                    SetPx(atlas, tile, x, y, 158, 118, 66);
                }
            }
        }
    }

    public static void PaintRailTile(byte[] atlas, ushort tile)
    {
        ClearTile(atlas, tile);
        for (var y = 0; y < 16; y++)
        {
            if (y % 4 == 1 || y % 4 == 2)
            {
                for (var x = 1; x <= 14; x++)
                {
                    SetPx(atlas, tile, x, y, 128, 92, 52);
                }
            }

            SetPx(atlas, tile, 3, y, 178, 182, 188);
            SetPx(atlas, tile, 4, y, 148, 152, 158);
            SetPx(atlas, tile, 11, y, 178, 182, 188);
            SetPx(atlas, tile, 12, y, 148, 152, 158);
        }
    }

    public static void PaintCobwebTile(byte[] atlas, ushort tile)
    {
        ClearTile(atlas, tile);
        for (var i = 0; i < 16; i++)
        {
            SetPx(atlas, tile, i, i, 232, 236, 240);
            SetPx(atlas, tile, 15 - i, i, 232, 236, 240);
            SetPx(atlas, tile, 7, i, 218, 222, 226);
            SetPx(atlas, tile, i, 7, 218, 222, 226);
        }
    }

    public static void PaintFurnaceFrontTile(byte[] atlas, ushort tile, bool lit)
    {
        ProceduralBlockTilePainter.PaintCobblestoneTile(atlas, tile, mossy: false);
        for (var y = 4; y <= 13; y++)
        {
            for (var x = 4; x <= 11; x++)
            {
                if (y <= 7)
                {
                    SetPx(atlas, tile, x, y, 36, 36, 40);
                }
                else if (y >= 9)
                {
                    if (lit)
                    {
                        var glow = ((x + y) & 1) == 0 ? (byte)255 : (byte)232;
                        SetPx(atlas, tile, x, y, glow, (byte)(glow - 85), 32);
                    }
                    else
                    {
                        SetPx(atlas, tile, x, y, 28, 28, 32);
                    }
                }
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
                    SetPx(atlas, tile, x, y, (byte)Math.Min(255, r + hi), (byte)Math.Min(255, g + hi), (byte)Math.Min(255, b + hi));
                }
            }
        }
    }

    public static void PaintIngot(byte[] atlas, ushort tile, byte r, byte g, byte b)
    {
        ClearTile(atlas, tile);
        for (var y = 5; y <= 10; y++)
        {
            var indent = Math.Abs(y - 7) / 2;
            for (var x = 3 + indent; x <= 12 - indent; x++)
            {
                var hi = 0;
                if (y <= 6) hi = 26;
                else if (y >= 9) hi = -22;
                SetPx(atlas, tile, x, y, (byte)Math.Clamp(r + hi, 0, 255), (byte)Math.Clamp(g + hi, 0, 255), (byte)Math.Clamp(b + hi, 0, 255));
            }
        }
    }

    public static void PaintBucket(byte[] atlas, ushort tile, bool filledWithWater)
    {
        ClearTile(atlas, tile);
        for (var y = 4; y <= 12; y++)
        {
            var halfW = y <= 6 ? 4 : 3;
            for (var x = 8 - halfW; x <= 7 + halfW; x++)
            {
                if (filledWithWater && y is >= 5 and <= 6 && x > 8 - halfW && x < 7 + halfW)
                {
                    SetPx(atlas, tile, x, y, 48, 124, 238);
                }
                else
                {
                    SetPx(atlas, tile, x, y, 186, 192, 200);
                }
            }
        }
    }

    public static void PaintShield(byte[] atlas, ushort tile)
    {
        ClearTile(atlas, tile);
        for (var y = 2; y <= 13; y++)
        {
            for (var x = 4; x <= 11; x++)
            {
                var border = x == 4 || x == 11 || y == 2 || y == 13 || (x is 7 or 8 && y is 7 or 8);
                if (border) SetPx(atlas, tile, x, y, 168, 174, 184);
                else SetPx(atlas, tile, x, y, 164, 122, 72);
            }
        }
    }

    public static void PaintBedItem(byte[] atlas, ushort tile)
    {
        ClearTile(atlas, tile);
        for (var y = 6; y <= 10; y++)
        {
            for (var x = 2; x <= 13; x++)
            {
                if (x <= 5) SetPx(atlas, tile, x, y, 240, 242, 246);
                else SetPx(atlas, tile, x, y, 198, 38, 42);
            }
        }

        for (var x = 2; x <= 13; x++) SetPx(atlas, tile, x, 11, 152, 112, 66);
        SetPx(atlas, tile, 2, 12, 132, 96, 54);
        SetPx(atlas, tile, 13, 12, 132, 96, 54);
    }

    public static void PaintComparatorItem(byte[] atlas, ushort tile)
    {
        ClearTile(atlas, tile);
        for (var y = 10; y <= 13; y++)
        {
            for (var x = 2; x <= 13; x++) SetPx(atlas, tile, x, y, 164, 158, 158);
        }

        for (var y = 5; y <= 9; y++)
        {
            SetPx(atlas, tile, 4, y, 138, 102, 58);
            SetPx(atlas, tile, 11, y, 138, 102, 58);
            SetPx(atlas, tile, 7, y - 1, 138, 102, 58);
        }

        SetPx(atlas, tile, 4, 4, 242, 48, 38);
        SetPx(atlas, tile, 11, 4, 242, 48, 38);
        SetPx(atlas, tile, 7, 3, 250, 72, 48);
    }

    public static void PaintPotionBottle(byte[] atlas, ushort tile, byte lr, byte lg, byte lb)
    {
        ClearTile(atlas, tile);
        SetPx(atlas, tile, 7, 2, 182, 136, 84);
        SetPx(atlas, tile, 8, 2, 182, 136, 84);
        for (var x = 6; x <= 9; x++) SetPx(atlas, tile, x, 3, 206, 224, 238);
        SetPx(atlas, tile, 7, 4, 190, 214, 232);
        SetPx(atlas, tile, 8, 4, 190, 214, 232);
        for (var y = 5; y <= 13; y++)
        {
            var half = y is 5 or 13 ? 2 : 3;
            for (var x = 7 - half; x <= 8 + half; x++)
            {
                var rim = x == 7 - half || x == 8 + half || y == 5 || y == 13;
                if (rim) SetPx(atlas, tile, x, y, 186, 212, 232);
                else if (y >= 7) SetPx(atlas, tile, x, y, lr, lg, lb);
                else SetPx(atlas, tile, x, y, 224, 238, 248);
            }
        }
    }

    public static void PaintSpawnEgg(byte[] atlas, ushort tile, byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
    {
        ClearTile(atlas, tile);
        for (var y = 2; y <= 13; y++)
        {
            var ny = (y - 8.2f) / 6.0f;
            var halfW = (int)MathF.Round((y < 8 ? 4.2f : 5.1f) * MathF.Sqrt(Math.Max(0.0f, 1.0f - (ny * ny))));
            for (var x = 8 - halfW; x <= 7 + halfW; x++)
            {
                var border = x == 8 - halfW || x == 7 + halfW || y == 2 || y == 13;
                var spot = ((x * 5 + y * 3) % 7) == 0 || (x is 6 or 10 && y is 6 or 9);
                if (border) SetPx(atlas, tile, x, y, (byte)(r1 * 0.65f), (byte)(g1 * 0.65f), (byte)(b1 * 0.65f));
                else if (spot) SetPx(atlas, tile, x, y, r2, g2, b2);
                else SetPx(atlas, tile, x, y, r1, g1, b1);
            }
        }
    }

    public static void PaintTrapdoorOrSpawnerTile(byte[] atlas, ushort tile, bool isSpawner)
    {
        ClearTile(atlas, tile);
        byte r = isSpawner ? (byte)46 : (byte)158;
        byte g = isSpawner ? (byte)58 : (byte)118;
        byte b = isSpawner ? (byte)74 : (byte)68;
        for (var y = 0; y < 16; y++)
        {
            for (var x = 0; x < 16; x++)
            {
                var frame = x < 2 || x >= 14 || y < 2 || y >= 14 || x is 7 or 8 || y is 7 or 8;
                if (frame) SetPx(atlas, tile, x, y, r, g, b);
                else if (isSpawner && x is 5 or 10 && y is 5 or 10) SetPx(atlas, tile, x, y, 232, 92, 32);
            }
        }
    }

    public static void PaintRedstoneDustCross(byte[] atlas, ushort tile, bool lineOnly)
    {
        ClearTile(atlas, tile);
        for (var i = 0; i < 16; i++)
        {
            SetPx(atlas, tile, i, 7, 212, 24, 24);
            SetPx(atlas, tile, i, 8, 228, 36, 36);
            if (!lineOnly)
            {
                SetPx(atlas, tile, 7, i, 212, 24, 24);
                SetPx(atlas, tile, 8, i, 228, 36, 36);
            }
        }
    }

    public static void PaintDestroyCrackStage(byte[] atlas, ushort tile, int stage0To9)
    {
        ClearTile(atlas, tile);
        var branches = 2 + stage0To9;
        var maxLen = 3 + stage0To9;
        var baseX = (tile % 16) * 16;
        var baseY = (tile / 16) * 16;

        for (var b = 0; b < branches; b++)
        {
            var angle = b * 2.3999632f;
            var dx = MathF.Cos(angle);
            var dy = MathF.Sin(angle);
            for (var step = 0; step <= maxLen; step++)
            {
                var jitter = (((b * 7) + step) % 3) - 1;
                var px = (int)MathF.Round(7.5f + (dx * step) + (dy * jitter * 0.35f));
                var py = (int)MathF.Round(7.5f + (dy * step) - (dx * jitter * 0.35f));
                if ((uint)px < 16U && (uint)py < 16U)
                {
                    var idx = (((baseY + py) * 256) + baseX + px) * 4;
                    atlas[idx + 0] = 18;
                    atlas[idx + 1] = 18;
                    atlas[idx + 2] = 20;
                    atlas[idx + 3] = (byte)Math.Clamp(140 + (stage0To9 * 10), 140, 235);
                }
            }
        }
    }

    private static void ClearTile(byte[] atlas, ushort tile)
    {
        var baseX = (tile % 16) * 16;
        var baseY = (tile / 16) * 16;
        for (var y = 0; y < 16; y++)
        {
            var rowIdx = (((baseY + y) * 256) + baseX) * 4;
            Array.Clear(atlas, rowIdx, 16 * 4);
        }
    }

    private static void SetPx(byte[] atlas, ushort tile, int x, int y, byte r, byte g, byte b)
    {
        if ((uint)x >= 16U || (uint)y >= 16U)
        {
            return;
        }

        var ax = ((tile % 16) * 16) + x;
        var ay = ((tile / 16) * 16) + y;
        var idx = ((ay * 256) + ax) * 4;
        atlas[idx + 0] = r;
        atlas[idx + 1] = g;
        atlas[idx + 2] = b;
        atlas[idx + 3] = 255;
    }
}
