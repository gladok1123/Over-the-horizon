using OverHorizon.World.Storage;

namespace OverHorizon.World.Generation;

/// <summary>
/// Interior pieces for <see cref="NetherFortressGenerator"/>: enclosed corridor wings with window slits, the
/// two-storey keep with its Nether Brick staircase and Glowstone chandeliers, plus the Nether Wart farm,
/// Blaze spawner hall, and chest vault terminus rooms (split out to respect the 500-line architecture budget).
/// </summary>
public sealed partial class NetherFortressGenerator
{
    private void BuildCorridorAlongX(
        ChunkColumn chunk,
        int cMinX,
        int cMinZ,
        int x0,
        int x1,
        int cz,
        int baseY)
    {
        for (var wx = x0; wx <= x1; wx++)
        {
            var windowColumn = ((wx % 4) + 4) % 4 == 1;
            for (var dz = -2; dz <= 2; dz++)
            {
                var isWall = Math.Abs(dz) == 2;
                Set(chunk, cMinX, cMinZ, wx, baseY, cz + dz, _bricks);

                for (var h = 1; h <= 3; h++)
                {
                    var block = _air;
                    if (isWall)
                    {
                        block = h == 2 && windowColumn ? _air : _bricks;
                    }

                    Set(chunk, cMinX, cMinZ, wx, baseY + h, cz + dz, block);
                }

                Set(chunk, cMinX, cMinZ, wx, baseY + 4, cz + dz, _bricks);
            }

            if (windowColumn)
            {
                Set(chunk, cMinX, cMinZ, wx, baseY + 3, cz, _glowstone);
            }
        }
    }

    private void BuildCorridorAlongZ(
        ChunkColumn chunk,
        int cMinX,
        int cMinZ,
        int z0,
        int z1,
        int cx,
        int baseY)
    {
        for (var wz = z0; wz <= z1; wz++)
        {
            var windowColumn = ((wz % 4) + 4) % 4 == 1;
            for (var dx = -2; dx <= 2; dx++)
            {
                var isWall = Math.Abs(dx) == 2;
                Set(chunk, cMinX, cMinZ, cx + dx, baseY, wz, _bricks);

                for (var h = 1; h <= 3; h++)
                {
                    var block = _air;
                    if (isWall)
                    {
                        block = h == 2 && windowColumn ? _air : _bricks;
                    }

                    Set(chunk, cMinX, cMinZ, cx + dx, baseY + h, wz, block);
                }

                Set(chunk, cMinX, cMinZ, cx + dx, baseY + 4, wz, _bricks);
            }

            if (windowColumn)
            {
                Set(chunk, cMinX, cMinZ, cx, baseY + 3, wz, _glowstone);
            }
        }
    }

    private void BuildKeep(ChunkColumn chunk, int cMinX, int cMinZ, int hubX, int hubZ, int baseY)
    {
        var upperY = baseY + StoreyHeight;

        for (var dz = -5; dz <= 5; dz++)
        {
            for (var dx = -5; dx <= 5; dx++)
            {
                var edgeX = Math.Abs(dx) == 5;
                var edgeZ = Math.Abs(dz) == 5;
                var isWall = edgeX || edgeZ;
                var isPillar = edgeX && edgeZ;
                var isDoorway = isWall && !isPillar && (edgeX ? Math.Abs(dz) <= 1 : Math.Abs(dx) <= 1);

                Set(chunk, cMinX, cMinZ, hubX + dx, baseY, hubZ + dz, _bricks);
                for (var h = 1; h <= 4; h++)
                {
                    var block = _air;
                    if (isWall)
                    {
                        block = isDoorway && h <= 3 ? _air : _bricks;
                    }

                    Set(chunk, cMinX, cMinZ, hubX + dx, baseY + h, hubZ + dz, block);
                }

                Set(chunk, cMinX, cMinZ, hubX + dx, baseY + 5, hubZ + dz, _bricks);

                var isStairwell = (dx == 2 || dx == 3) && dz is >= -3 and <= 1;
                Set(chunk, cMinX, cMinZ, hubX + dx, upperY, hubZ + dz, isStairwell ? _air : _bricks);
                for (var h = 1; h <= 4; h++)
                {
                    var block = _air;
                    if (isWall)
                    {
                        block = h == 2 && IsUpperWindow(hubX + dx, hubZ + dz) ? _air : _bricks;
                    }

                    Set(chunk, cMinX, cMinZ, hubX + dx, upperY + h, hubZ + dz, block);
                }

                Set(chunk, cMinX, cMinZ, hubX + dx, upperY + 5, hubZ + dz, _bricks);
            }
        }

        for (var sy = -1; sy <= 1; sy += 2)
        {
            for (var sx = -1; sx <= 1; sx += 2)
            {
                Set(chunk, cMinX, cMinZ, hubX + (sx * 3), baseY + 4, hubZ + (sy * 3), _glowstone);
                Set(chunk, cMinX, cMinZ, hubX + (sx * 3), upperY + 4, hubZ + (sy * 3), _glowstone);
            }
        }

        BuildKeepStaircase(chunk, cMinX, cMinZ, hubX, hubZ, baseY);
    }

    private void BuildKeepStaircase(ChunkColumn chunk, int cMinX, int cMinZ, int hubX, int hubZ, int baseY)
    {
        for (var step = 0; step < StoreyHeight; step++)
        {
            var wz = (hubZ - 3) + step;
            for (var dx = 2; dx <= 3; dx++)
            {
                for (var y = baseY + 1; y <= baseY + 1 + step; y++)
                {
                    Set(chunk, cMinX, cMinZ, hubX + dx, y, wz, _bricks);
                }
            }
        }
    }

    private void BuildWartFarm(
        ChunkColumn chunk,
        int cMinX,
        int cMinZ,
        int x0,
        int x1,
        int z0,
        int z1,
        int baseY)
    {
        var centreZ = (z0 + z1) / 2;
        var centreX = (x0 + x1) / 2;

        for (var wz = z0; wz <= z1; wz++)
        {
            for (var wx = x0; wx <= x1; wx++)
            {
                var isWall = wx == x0 || wx == x1 || wz == z0 || wz == z1;
                var isDoorway = wx == x1 && Math.Abs(wz - centreZ) <= 1;

                Set(chunk, cMinX, cMinZ, wx, baseY, wz, isWall ? _bricks : _soulSand);
                for (var h = 1; h <= 4; h++)
                {
                    var block = _air;
                    if (isWall)
                    {
                        block = isDoorway && h <= 3 ? _air : _bricks;
                    }

                    Set(chunk, cMinX, cMinZ, wx, baseY + h, wz, block);
                }

                Set(chunk, cMinX, cMinZ, wx, baseY + 5, wz, _bricks);
            }
        }

        for (var sy = -1; sy <= 1; sy += 2)
        {
            Set(chunk, cMinX, cMinZ, centreX - 2, baseY + 4, centreZ + (sy * 5), _glowstone);
            Set(chunk, cMinX, cMinZ, centreX + 2, baseY + 4, centreZ + (sy * 5), _glowstone);
        }
    }

    private void BuildBlazeSpawnerHall(
        ChunkColumn chunk,
        int cMinX,
        int cMinZ,
        int x0,
        int x1,
        int z0,
        int z1,
        int baseY)
    {
        var centreX = (x0 + x1) / 2;
        var centreZ = (z0 + z1) / 2;

        for (var wz = z0; wz <= z1; wz++)
        {
            for (var wx = x0; wx <= x1; wx++)
            {
                var isWall = wx == x0 || wx == x1 || wz == z0 || wz == z1;
                var isDoorway = wx == x0 && Math.Abs(wz - centreZ) <= 1;

                Set(chunk, cMinX, cMinZ, wx, baseY, wz, _bricks);
                for (var h = 1; h <= 4; h++)
                {
                    var block = _air;
                    if (isWall)
                    {
                        block = isDoorway && h <= 3 ? _air : _bricks;
                    }

                    Set(chunk, cMinX, cMinZ, wx, baseY + h, wz, block);
                }

                Set(chunk, cMinX, cMinZ, wx, baseY + 5, wz, _bricks);
            }
        }

        Set(chunk, cMinX, cMinZ, centreX, baseY + 1, centreZ, _spawner);

        for (var sy = -1; sy <= 1; sy += 2)
        {
            for (var sx = -1; sx <= 1; sx += 2)
            {
                Set(chunk, cMinX, cMinZ, centreX + (sx * 3), baseY + 4, centreZ + (sy * 3), _glowstone);
            }
        }
    }

    private void BuildTreasureVault(
        ChunkColumn chunk,
        int cMinX,
        int cMinZ,
        int x0,
        int x1,
        int z0,
        int z1,
        int baseY)
    {
        var centreX = (x0 + x1) / 2;
        var centreZ = (z0 + z1) / 2;

        for (var wz = z0; wz <= z1; wz++)
        {
            for (var wx = x0; wx <= x1; wx++)
            {
                var isWall = wx == x0 || wx == x1 || wz == z0 || wz == z1;
                var isDoorway = wz == z1 && Math.Abs(wx - centreX) <= 1;

                Set(chunk, cMinX, cMinZ, wx, baseY, wz, _bricks);
                for (var h = 1; h <= 4; h++)
                {
                    var block = _air;
                    if (isWall)
                    {
                        block = isDoorway && h <= 3 ? _air : _bricks;
                    }

                    Set(chunk, cMinX, cMinZ, wx, baseY + h, wz, block);
                }

                Set(chunk, cMinX, cMinZ, wx, baseY + 5, wz, _bricks);
            }
        }

        Set(chunk, cMinX, cMinZ, centreX - 2, baseY + 1, centreZ - 2, _chest);
        Set(chunk, cMinX, cMinZ, centreX + 2, baseY + 1, centreZ - 2, _chest);
        Set(chunk, cMinX, cMinZ, centreX, baseY + 1, centreZ + 1, _chest);
        Set(chunk, cMinX, cMinZ, centreX, baseY + 4, centreZ - 2, _glowstone);
        Set(chunk, cMinX, cMinZ, centreX, baseY + 4, centreZ + 2, _glowstone);
    }

    private static bool IsUpperWindow(int wx, int wz) => (((wx + wz) % 4) + 4) % 4 == 1;
}
