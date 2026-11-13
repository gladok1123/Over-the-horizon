namespace OverHorizon.Rendering.Textures;

/// <summary>
/// Lantern UV unwrap, framed Redstone Lamp, curved/powered Rail, Bow draw stages, Arrow, and Minecart procedural sprites.
/// </summary>
internal static partial class ProceduralItemTilePainter
{
    public static void PaintLanternTile(byte[] atlas, ushort tile)
    {
        ClearTile(atlas, tile);
        for (var y = 0; y <= 14; y++)
        {
            for (var x = 0; x <= 5; x++)
            {
                var darkIron = y <= 1 || y == 2 || y == 8 || y == 9 || y == 14 || x == 0 || x == 5;
                if (darkIron) SetPx(atlas, tile, x, y, 56, 60, 68);
                else if (x is 2 or 3 && y is 4 or 5) SetPx(atlas, tile, x, y, 255, 246, 178);
                else SetPx(atlas, tile, x, y, 252, 198, 76);
            }
        }

        for (var y = 1; y <= 11; y++)
        {
            SetPx(atlas, tile, 12, y, 74, 78, 88);
            if ((y & 1) == 1)
            {
                SetPx(atlas, tile, 11, y, 64, 68, 76);
                SetPx(atlas, tile, 13, y, 64, 68, 76);
            }
        }
    }

    public static void PaintLanternItemIcon(byte[] atlas, ushort tile)
    {
        ClearTile(atlas, tile);
        SetPx(atlas, tile, 7, 2, 74, 78, 88);
        SetPx(atlas, tile, 8, 2, 74, 78, 88);
        for (var x = 6; x <= 9; x++) SetPx(atlas, tile, x, 3, 62, 66, 74);
        for (var y = 4; y <= 12; y++)
        {
            for (var x = 5; x <= 10; x++)
            {
                var frame = y is 4 or 12 || x is 5 or 10;
                if (frame) SetPx(atlas, tile, x, y, 56, 60, 68);
                else if (x is 7 or 8 && y is >= 6 and <= 9) SetPx(atlas, tile, x, y, 255, 244, 172);
                else SetPx(atlas, tile, x, y, 250, 196, 72);
            }
        }
    }

    public static void PaintRedstoneLampTile(byte[] atlas, ushort tile, bool lit)
    {
        for (var y = 0; y < 16; y++)
        {
            for (var x = 0; x < 16; x++)
            {
                var outerFrame = x <= 1 || x >= 14 || y <= 1 || y >= 14;
                var lattice = x == y || (15 - x) == y || ((x is 2 or 13) && (y is 2 or 13));
                if (outerFrame)
                {
                    SetPx(atlas, tile, x, y, 42, 30, 22);
                }
                else if (lattice && (x <= 4 || x >= 11 || y <= 4 || y >= 11))
                {
                    SetPx(atlas, tile, x, y, 68, 46, 30);
                }
                else if (lit)
                {
                    var centerDist = Math.Abs(x - 7.5f) + Math.Abs(y - 7.5f);
                    if (centerDist < 4.5f) SetPx(atlas, tile, x, y, 255, 246, 188);
                    else if (centerDist < 8.0f) SetPx(atlas, tile, x, y, 250, 212, 112);
                    else SetPx(atlas, tile, x, y, 218, 156, 68);
                }
                else
                {
                    var checker = ((x + y) & 1) == 0 ? 10 : 0;
                    SetPx(atlas, tile, x, y, (byte)(124 + checker), (byte)(78 + checker), (byte)(42 + (checker / 2)));
                }
            }
        }
    }

    public static void PaintPoweredRailTile(byte[] atlas, ushort tile, bool energized)
    {
        PaintRailTile(atlas, tile);
        var gr = energized ? (byte)255 : (byte)214;
        var gg = energized ? (byte)64 : (byte)176;
        var gb = energized ? (byte)48 : (byte)42;
        for (var y = 0; y < 16; y++)
        {
            SetPx(atlas, tile, 2, y, 232, 192, 48);
            SetPx(atlas, tile, 13, y, 232, 192, 48);
            if ((y & 1) == 0)
            {
                SetPx(atlas, tile, 7, y, gr, gg, gb);
                SetPx(atlas, tile, 8, y, gr, gg, gb);
            }
        }
    }

    public static void PaintRailCornerTile(byte[] atlas, ushort tile)
    {
        ClearTile(atlas, tile);
        for (var y = 0; y < 16; y++)
        {
            for (var x = 0; x < 16; x++)
            {
                var dx = 15 - x;
                var dy = 15 - y;
                var r = MathF.Sqrt((dx * dx) + (dy * dy));
                if (r is >= 2.8f and <= 4.3f || r is >= 10.8f and <= 12.3f)
                {
                    SetPx(atlas, tile, x, y, 176, 180, 186);
                }
                else if (r is > 4.3f and < 10.8f && ((x + y) % 4 == 0))
                {
                    SetPx(atlas, tile, x, y, 128, 92, 52);
                }
            }
        }
    }

    public static void PaintBowAndArrowTiles(byte[] atlas, ushort bowBaseTile, ushort arrowTile, ushort minecartTile)
    {
        // Match exact 23w13a bow.png diagonal orientation: wooden arc along top-left, string along bottom-right, arrow tip at top-left (1,1)!
        for (var stage = 0; stage < 4; stage++)
        {
            var t = stage == 0 ? bowBaseTile : (ushort)(ProceduralItemTileManifest.TileBowPulling0 + stage - 1);
            ClearTile(atlas, t);
            var pull = stage * 2;
            for (var i = 3; i <= 13; i++)
            {
                var strX = i + (stage > 0 && i is >= 5 and <= 11 ? pull / 2 : 0);
                var strY = (16 - i) + (stage > 0 && i is >= 5 and <= 11 ? pull / 2 : 0);
                if (strX <= 15 && strY <= 15) SetPx(atlas, t, strX, strY, 224, 228, 234);
            }

            for (var i = 0; i <= 10; i++)
            {
                var wx = 2 + (i * 9 / 10);
                var wy = 11 - (i * 9 / 10);
                var curve = 2 - (Math.Abs(i - 5) * 2 / 5);
                SetPx(atlas, t, Math.Max(1, wx - curve), Math.Max(1, wy - curve), 148, 102, 54);
                SetPx(atlas, t, Math.Max(1, wx - curve + 1), Math.Max(1, wy - curve), 116, 78, 40);
            }

            if (stage > 0)
            {
                for (var k = 2; k <= 10 + (pull / 2); k++) SetPx(atlas, t, k, k, 162, 118, 64);
                SetPx(atlas, t, 1, 1, 206, 212, 220);
                SetPx(atlas, t, 2, 1, 192, 198, 208);
                SetPx(atlas, t, 1, 2, 192, 198, 208);
            }
        }

        ClearTile(atlas, arrowTile);
        for (var i = 3; i <= 12; i++) SetPx(atlas, arrowTile, i, 15 - i, 152, 108, 58);
        SetPx(atlas, arrowTile, 13, 2, 204, 210, 218);
        SetPx(atlas, arrowTile, 12, 2, 184, 190, 198);
        SetPx(atlas, arrowTile, 13, 3, 184, 190, 198);
        SetPx(atlas, arrowTile, 2, 13, 240, 244, 248);
        SetPx(atlas, arrowTile, 3, 14, 240, 244, 248);

        ClearTile(atlas, minecartTile);
        for (var y = 6; y <= 12; y++)
        {
            for (var x = 2; x <= 13; x++)
            {
                var rim = y == 6 || y == 12 || x == 2 || x == 13;
                SetPx(atlas, minecartTile, x, y, rim ? (byte)196 : (byte)138, rim ? (byte)202 : (byte)144, rim ? (byte)210 : (byte)152);
            }
        }
    }
}
