using OverHorizon.World.Storage;

namespace OverHorizon.World.Generation;

/// <summary>
/// Corridor carving, rail track laying, intersection-safe timber arches, suspended cavern trestle bridges, and deterministic hash helpers for <see cref="MineshaftStructureGenerator"/>.
/// </summary>
internal sealed partial class MineshaftStructureGenerator
{
    private void BuildCorridorAlongX(
        ChunkColumn chunk,
        int cMinX,
        int cMinZ,
        int minWx,
        int maxWx,
        int centerWz,
        int floorY,
        int seedSalt,
        ReadOnlySpan<int> crossXs,
        int hubX,
        int hubZ)
    {
        var startWx = Math.Max(minWx, cMinX);
        var endWx = Math.Min(maxWx, cMinX + 15);
        if (startWx > endWx) return;

        for (var wx = startWx; wx <= endWx; wx++)
        {
            if (Math.Abs(wx - hubX) <= 5 && Math.Abs(centerWz - hubZ) <= 5) continue;

            var lx = wx - cMinX;
            var nearCross = IsNearAny(wx, crossXs, 1);
            var isSupportArch = !nearCross && FloorMod(wx, 5) == 0;
            var h = Hash(_seed, wx, centerWz, seedSalt);

            for (var dz = -1; dz <= 1; dz++)
            {
                var wz = centerWz + dz;
                var lz = wz - cMinZ;
                if ((uint)lz >= 16U) continue;

                EnsureCorridorFloorAndBridge(chunk, lx, floorY, lz, isArchPost: isSupportArch && dz != 0);
                for (var y = floorY + 1; y <= floorY + 3; y++)
                {
                    ClearIfNotWaterSource(chunk, lx, y, lz);
                }

                if (isSupportArch)
                {
                    if (dz != 0)
                    {
                        _ = chunk.SetBlockStateRaw(lx, floorY + 1, lz, _oakFence);
                        _ = chunk.SetBlockStateRaw(lx, floorY + 2, lz, _oakLog);
                    }

                    _ = chunk.SetBlockStateRaw(lx, floorY + 3, lz, _oakPlanks);
                }
                else if (!nearCross && FloorMod(wx - 1, 10) == 0 && dz == -1)
                {
                    _ = chunk.SetBlockStateRaw(lx, floorY + 2, lz, _torchEast);
                }
                else if (dz == 0 && !nearCross && (h & 7UL) != 0)
                {
                    _ = chunk.SetBlockStateRaw(lx, floorY + 1, lz, _railEastWest);
                }
                else if (dz != 0 && (h & 31UL) == 1)
                {
                    _ = chunk.SetBlockStateRaw(lx, floorY + 3, lz, _cobweb);
                }
                else if (dz == 1 && !nearCross && (h & 127UL) == 3)
                {
                    _ = chunk.SetBlockStateRaw(lx, floorY + 1, lz, (h & 128UL) == 0 ? _chest : _barrel);
                }
            }
        }
    }

    private void BuildCorridorAlongZ(
        ChunkColumn chunk,
        int cMinX,
        int cMinZ,
        int minWz,
        int maxWz,
        int centerWx,
        int floorY,
        int seedSalt,
        ReadOnlySpan<int> crossZs,
        int hubX,
        int hubZ)
    {
        var startWz = Math.Max(minWz, cMinZ);
        var endWz = Math.Min(maxWz, cMinZ + 15);
        if (startWz > endWz) return;

        for (var wz = startWz; wz <= endWz; wz++)
        {
            if (Math.Abs(centerWx - hubX) <= 5 && Math.Abs(wz - hubZ) <= 5) continue;

            var lz = wz - cMinZ;
            var nearCross = IsNearAny(wz, crossZs, 1);
            var isSupportArch = !nearCross && FloorMod(wz, 5) == 0;
            var h = Hash(_seed, centerWx, wz, seedSalt);

            for (var dx = -1; dx <= 1; dx++)
            {
                var wx = centerWx + dx;
                var lx = wx - cMinX;
                if ((uint)lx >= 16U) continue;

                EnsureCorridorFloorAndBridge(chunk, lx, floorY, lz, isArchPost: isSupportArch && dx != 0);
                for (var y = floorY + 1; y <= floorY + 3; y++)
                {
                    ClearIfNotWaterSource(chunk, lx, y, lz);
                }

                if (isSupportArch)
                {
                    if (dx != 0)
                    {
                        _ = chunk.SetBlockStateRaw(lx, floorY + 1, lz, _oakFence);
                        _ = chunk.SetBlockStateRaw(lx, floorY + 2, lz, _oakLog);
                    }

                    _ = chunk.SetBlockStateRaw(lx, floorY + 3, lz, _oakPlanks);
                }
                else if (!nearCross && FloorMod(wz - 1, 10) == 0 && dx == -1)
                {
                    _ = chunk.SetBlockStateRaw(lx, floorY + 2, lz, _torchSouth);
                }
                else if (dx == 0 && !nearCross && (h & 7UL) != 0)
                {
                    _ = chunk.SetBlockStateRaw(lx, floorY + 1, lz, _railNorthSouth);
                }
                else if (dx != 0 && (h & 31UL) == 2)
                {
                    _ = chunk.SetBlockStateRaw(lx, floorY + 3, lz, _cobweb);
                }
            }
        }
    }

    private void EnsureCorridorFloorAndBridge(ChunkColumn chunk, int lx, int floorY, int lz, bool isArchPost)
    {
        var current = chunk.GetBlockState(lx, floorY, lz);
        if (current.IsAir || current == _water || isArchPost)
        {
            _ = chunk.SetBlockStateRaw(lx, floorY, lz, _oakPlanks);
            if (isArchPost)
            {
                for (var py = floorY - 1; py >= floorY - 12 && py > -60; py--)
                {
                    var below = chunk.GetBlockState(lx, py, lz);
                    if (!below.IsAir && below != _water)
                    {
                        break;
                    }

                    _ = chunk.SetBlockStateRaw(lx, py, lz, _oakFence);
                }
            }
        }
    }

    private void ClearIfNotWaterSource(ChunkColumn chunk, int lx, int y, int lz)
    {
        if (chunk.GetBlockState(lx, y, lz) != _water)
        {
            _ = chunk.SetBlockStateRaw(lx, y, lz, _air);
        }
    }

    private void GetHubCenter(int rx, int rz, out int hubX, out int hubZ, out int lowerY)
    {
        var h = Hash(_seed, rx, rz, 777);
        hubX = (rx * RegionSizeBlocks) + 48 + (int)(h % 17UL);
        hubZ = (rz * RegionSizeBlocks) + 48 + (int)((h >> 8) % 17UL);
        lowerY = 16 + (int)((h >> 16) % 10UL);
    }

    private static bool IsNearAny(int coord, ReadOnlySpan<int> targets, int margin)
    {
        for (var i = 0; i < targets.Length; i++)
        {
            if (Math.Abs(coord - targets[i]) <= margin)
            {
                return true;
            }
        }

        return false;
    }

    private static int FloorDiv(int a, int b) => a >= 0 ? a / b : ((a - b + 1) / b);

    private static int FloorMod(int a, int b)
    {
        var r = a % b;
        return r < 0 ? r + b : r;
    }

    private static ulong Hash(long seed, int a, int b, int c)
    {
        var z = (ulong)seed ^ ((ulong)(uint)a * 0xBF58476D1CE4E5B9UL) ^ ((ulong)(uint)b * 0x94D049BB133111EBUL) ^ ((ulong)(uint)c * 0x9E3779B97F4A7C15UL);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
