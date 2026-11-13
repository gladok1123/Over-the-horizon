using OverHorizon.World.Storage;

namespace OverHorizon.World.Generation;

internal sealed partial class WorldStructurePopulator
{
    private void BuildRangerCabinAndTower(ChunkColumn chunk, int cMinX, int cMinZ, int ax, int baseY, int az)
    {
        for (var wz = az - 4; wz <= az + 4; wz++)
        {
            var lz = wz - cMinZ;
            if ((uint)lz >= 16U) continue;
            for (var wx = ax - 4; wx <= ax + 4; wx++)
            {
                var lx = wx - cMinX;
                if ((uint)lx >= 16U) continue;

                var dx = wx - ax;
                var dz = wz - az;
                var inCabin = Math.Abs(dx) <= 3 && Math.Abs(dz) <= 3;
                if (!inCabin)
                {
                    if (dx == 4 && dz == 0)
                    {
                        _ = chunk.SetBlockStateRaw(lx, baseY, lz, _cobblestone);
                        _ = chunk.SetBlockStateRaw(lx, baseY + 1, lz, _composter);
                    }
                    else if (dz == 4 && Math.Abs(dx) <= 1)
                    {
                        _ = chunk.SetBlockStateRaw(lx, baseY, lz, _dirtPath);
                    }

                    continue;
                }

                for (var fy = baseY - 4; fy <= baseY; fy++)
                {
                    _ = chunk.SetBlockStateRaw(lx, fy, lz, fy == baseY ? _oakPlanks : _stoneBricks);
                }

                for (var cy = baseY + 1; cy <= baseY + 9; cy++)
                {
                    _ = chunk.SetBlockStateRaw(lx, cy, lz, _air);
                }

                var isCorner = Math.Abs(dx) == 3 && Math.Abs(dz) == 3;
                var isWall = Math.Abs(dx) == 3 || Math.Abs(dz) == 3;
                var isFrontDoor = dz == 3 && dx == 0;
                var isWindow = isWall && !isCorner && !isFrontDoor && (dx == 0 || dz == 0);

                for (var h = 1; h <= 4; h++)
                {
                    if (isCorner) _ = chunk.SetBlockStateRaw(lx, baseY + h, lz, _oakLog);
                    else if (isFrontDoor && h == 1) _ = chunk.SetBlockStateRaw(lx, baseY + h, lz, _oakDoorLower);
                    else if (isFrontDoor && h == 2) _ = chunk.SetBlockStateRaw(lx, baseY + h, lz, _oakDoorUpper);
                    else if (isWindow && h == 2) _ = chunk.SetBlockStateRaw(lx, baseY + h, lz, _glass);
                    else if (isWall) _ = chunk.SetBlockStateRaw(lx, baseY + h, lz, h == 1 ? _stoneBricks : _oakPlanks);
                }

                _ = chunk.SetBlockStateRaw(lx, baseY + 5, lz, (dx == -2 && dz == -2) ? _trapdoor : _oakPlanks);
                if (isWall)
                {
                    _ = chunk.SetBlockStateRaw(lx, baseY + 6, lz, isCorner ? _oakLog : _oakFence);
                }

                if (dx == 0 && dz == 0) _ = chunk.SetBlockStateRaw(lx, baseY + 4, lz, _hangingLantern);
                else if (dx == -2 && dz == 1) _ = chunk.SetBlockStateRaw(lx, baseY + 1, lz, _craftingTable);
                else if (dx == -2 && dz == 2) _ = chunk.SetBlockStateRaw(lx, baseY + 1, lz, _furnace);
                else if (dx == 2 && dz == -2) _ = chunk.SetBlockStateRaw(lx, baseY + 1, lz, _chest);
            }
        }
    }

    private void BuildRuinedObsidianPortal(ChunkColumn chunk, int cMinX, int cMinZ, int ax, int baseY, int az)
    {
        for (var wz = az - 3; wz <= az + 3; wz++)
        {
            var lz = wz - cMinZ;
            if ((uint)lz >= 16U) continue;
            for (var wx = ax - 3; wx <= ax + 3; wx++)
            {
                var lx = wx - cMinX;
                if ((uint)lx >= 16U) continue;

                var dx = wx - ax;
                var dz = wz - az;
                if ((dx * dx) + (dz * dz) > 10) continue;

                _ = chunk.SetBlockStateRaw(lx, baseY, lz, ((wx + wz) & 1) == 0 ? _mossyStoneBricks : _stoneBricks);
                for (var cy = baseY + 1; cy <= baseY + 6; cy++)
                {
                    _ = chunk.SetBlockStateRaw(lx, cy, lz, _air);
                }

                if (dz == 0 && Math.Abs(dx) <= 2)
                {
                    for (var py = 1; py <= 5; py++)
                    {
                        var isFrame = py == 1 || py == 5 || Math.Abs(dx) == 2;
                        var brokenCorner = dx == 2 && py >= 4;
                        if (isFrame && !brokenCorner)
                        {
                            _ = chunk.SetBlockStateRaw(lx, baseY + py, lz, (dx == -2 && py == 5) ? _goldBlock : _obsidian);
                        }
                    }
                }
                else if (dx == 1 && dz == 2)
                {
                    _ = chunk.SetBlockStateRaw(lx, baseY + 1, lz, _chest);
                }
                else if (dx == -1 && dz == 2)
                {
                    _ = chunk.SetBlockStateRaw(lx, baseY + 1, lz, _lantern);
                }
            }
        }
    }

    private void GetSurfaceAnchor(int gx, int gz, out int ax, out int az, out ulong hash)
    {
        hash = Hash(_seed, gx, gz, 0x77C3);
        ax = (gx * SurfaceGridSize) + 28 + (int)(hash % 40UL);
        az = (gz * SurfaceGridSize) + 28 + (int)((hash >> 10) % 40UL);
    }

    private static int FloorDiv(int a, int b) => a >= 0 ? a / b : ((a - b + 1) / b);

    private static ulong Hash(long seed, int a, int b, int c)
    {
        var z = (ulong)seed ^ ((ulong)(uint)a * 0xBF58476D1CE4E5B9UL) ^ ((ulong)(uint)b * 0x94D049BB133111EBUL) ^ ((ulong)(uint)c * 0x9E3779B97F4A7C15UL);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
