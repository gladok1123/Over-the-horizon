using OverHorizon.Core.Blocks;
using OverHorizon.World.Storage;

namespace OverHorizon.World.Generation;

/// <summary>
/// Deterministic multi-chunk structural abandoned mineshaft generator producing grand central pit chambers,
/// surface ladder access shafts, 2-level branching corridors, wooden support arches, suspended trestle bridges,
/// minecart rails, cobwebs, torches, lanterns, and supply chests/barrels.
/// </summary>
internal sealed class MineshaftStructureGenerator
{
    private const int RegionSizeBlocks = 112; // 7x7 chunks per mineshaft region

    private readonly long _seed;
    private readonly BlockStateId _air;
    private readonly BlockStateId _water;
    private readonly BlockStateId _oakPlanks;
    private readonly BlockStateId _oakLog;
    private readonly BlockStateId _oakFence;
    private readonly BlockStateId _cobblestone;
    private readonly BlockStateId _torch;
    private readonly BlockStateId _lantern;
    private readonly BlockStateId _rail;
    private readonly BlockStateId _cobweb;
    private readonly BlockStateId _chest;
    private readonly BlockStateId _barrel;
    private readonly BlockStateId _ladder;

    public MineshaftStructureGenerator(long seed, IBlockRegistry blocks)
    {
        _seed = seed;
        _air = BlockStateId.Air;
        _water = blocks.GetByKey(BuiltInBlocks.Water).DefaultState;
        _oakPlanks = blocks.GetByKey(BuiltInBlocks.OakPlanks).DefaultState;
        _oakLog = blocks.GetByKey(BuiltInBlocks.OakLog).DefaultState;
        _oakFence = blocks.GetByKey(BuiltInBlocks.OakFence).DefaultState;
        _cobblestone = blocks.GetByKey(BuiltInBlocks.Cobblestone).DefaultState;
        _torch = blocks.GetByKey(BuiltInBlocks.Torch).DefaultState;
        _lantern = blocks.GetByKey(BuiltInBlocks.Lantern).DefaultState;
        _rail = blocks.GetByKey(BuiltInBlocks.Rail).DefaultState;
        _cobweb = blocks.GetByKey(BuiltInBlocks.Cobweb).DefaultState;
        _chest = blocks.GetByKey(BuiltInBlocks.Chest).DefaultState;
        _barrel = blocks.GetByKey(BuiltInBlocks.Barrel).DefaultState;
        _ladder = blocks.GetByKey(BuiltInBlocks.Ladder).DefaultState;
    }

    /// <summary>Returns <see langword="true"/> when <paramref name="worldX"/>/<paramref name="worldZ"/> falls inside a vertical surface access shaft.</summary>
    public bool IsVerticalAccessShaftColumn(int worldX, int worldZ)
    {
        var rx = FloorDiv(worldX, RegionSizeBlocks);
        var rz = FloorDiv(worldZ, RegionSizeBlocks);
        GetHubCenter(rx, rz, out var hubX, out var hubZ, out _);
        return Math.Abs(worldX - (hubX + 7)) <= 2 && Math.Abs(worldZ - hubZ) <= 2;
    }

    public void PopulateChunk(ChunkColumn chunk, Func<int, int, int> surfaceHeightSampler)
    {
        var chunkMinX = chunk.Position.X << 4;
        var chunkMinZ = chunk.Position.Z << 4;
        var chunkMaxX = chunkMinX + 15;
        var chunkMaxZ = chunkMinZ + 15;

        var minRx = FloorDiv(chunkMinX - 56, RegionSizeBlocks);
        var maxRx = FloorDiv(chunkMaxX + 56, RegionSizeBlocks);
        var minRz = FloorDiv(chunkMinZ - 56, RegionSizeBlocks);
        var maxRz = FloorDiv(chunkMaxZ + 56, RegionSizeBlocks);

        for (var rz = minRz; rz <= maxRz; rz++)
        {
            for (var rx = minRx; rx <= maxRx; rx++)
            {
                GetHubCenter(rx, rz, out var hubX, out var hubZ, out var lowerY);
                if (chunkMaxX < hubX - 54 || chunkMinX > hubX + 54 || chunkMaxZ < hubZ - 54 || chunkMinZ > hubZ + 54)
                {
                    continue;
                }

                var upperY = lowerY + 8;
                BuildGrandHubRoom(chunk, chunkMinX, chunkMinZ, hubX, hubZ, lowerY, isUpper: false);
                BuildGrandHubRoom(chunk, chunkMinX, chunkMinZ, hubX, hubZ, upperY, isUpper: true);
                BuildConnectingStaircase(chunk, chunkMinX, chunkMinZ, hubX, hubZ, lowerY);
                BuildVerticalAccessShaft(chunk, chunkMinX, chunkMinZ, hubX + 7, hubZ, lowerY, surfaceHeightSampler);

                // Lower level primary 4-way arterial corridors (96 blocks total span!) + cross-galleries
                BuildCorridorAlongX(chunk, chunkMinX, chunkMinZ, hubX - 48, hubX + 48, hubZ, lowerY, seedSalt: 11);
                BuildCorridorAlongZ(chunk, chunkMinX, chunkMinZ, hubZ - 48, hubZ + 48, hubX, lowerY, seedSalt: 22);
                BuildCorridorAlongZ(chunk, chunkMinX, chunkMinZ, hubZ - 36, hubZ + 36, hubX - 24, lowerY, seedSalt: 33);
                BuildCorridorAlongZ(chunk, chunkMinX, chunkMinZ, hubZ - 36, hubZ + 36, hubX + 24, lowerY, seedSalt: 44);
                BuildCorridorAlongX(chunk, chunkMinX, chunkMinZ, hubX - 36, hubX + 36, hubZ - 24, lowerY, seedSalt: 55);
                BuildCorridorAlongX(chunk, chunkMinX, chunkMinZ, hubX - 36, hubX + 36, hubZ + 24, lowerY, seedSalt: 66);

                // Upper level galleries
                BuildCorridorAlongX(chunk, chunkMinX, chunkMinZ, hubX - 36, hubX + 36, hubZ, upperY, seedSalt: 77);
                BuildCorridorAlongZ(chunk, chunkMinX, chunkMinZ, hubZ - 36, hubZ + 36, hubX, upperY, seedSalt: 88);
                BuildCorridorAlongX(chunk, chunkMinX, chunkMinZ, hubX - 28, hubX + 28, hubZ + 18, upperY, seedSalt: 99);
            }
        }
    }

    private void BuildGrandHubRoom(ChunkColumn chunk, int cMinX, int cMinZ, int hubX, int hubZ, int floorY, bool isUpper)
    {
        for (var wz = hubZ - 5; wz <= hubZ + 5; wz++)
        {
            var lz = wz - cMinZ;
            if ((uint)lz >= 16U)
            {
                continue;
            }

            for (var wx = hubX - 5; wx <= hubX + 5; wx++)
            {
                var lx = wx - cMinX;
                if ((uint)lx >= 16U)
                {
                    continue;
                }

                var isEdgeX = Math.Abs(wx - hubX) == 5;
                var isEdgeZ = Math.Abs(wz - hubZ) == 5;
                var isCenterHole = isUpper && Math.Abs(wx - hubX) <= 2 && Math.Abs(wz - hubZ) <= 2;

                if (!isCenterHole)
                {
                    var floorBlock = ((wx + wz) & 1) == 0 ? _cobblestone : _oakPlanks;
                    chunk.SetBlockState(lx, floorY, lz, floorBlock);
                }

                for (var y = floorY + 1; y <= floorY + 5; y++)
                {
                    if (isEdgeX && isEdgeZ)
                    {
                        chunk.SetBlockState(lx, y, lz, _oakLog);
                    }
                    else if (y == floorY + 5 && (isEdgeX || isEdgeZ))
                    {
                        chunk.SetBlockState(lx, y, lz, _oakLog);
                    }
                    else
                    {
                        ClearIfNotWaterSource(chunk, lx, y, lz);
                    }
                }

                if (Math.Abs(wx - hubX) == 3 && Math.Abs(wz - hubZ) == 3)
                {
                    chunk.SetBlockState(lx, floorY + 4, lz, _lantern);
                }

                if (!isUpper && wx == hubX - 4 && wz == hubZ - 4)
                {
                    chunk.SetBlockState(lx, floorY + 1, lz, _chest);
                }
                else if (!isUpper && wx == hubX + 4 && wz == hubZ - 4)
                {
                    chunk.SetBlockState(lx, floorY + 1, lz, _barrel);
                }
            }
        }
    }

    private void BuildConnectingStaircase(ChunkColumn chunk, int cMinX, int cMinZ, int hubX, int hubZ, int lowerY)
    {
        for (var step = 0; step < 8; step++)
        {
            var wx = hubX - 3 + step;
            var lx = wx - cMinX;
            if ((uint)lx >= 16U)
            {
                continue;
            }

            var stepY = lowerY + 1 + step;
            for (var wz = hubZ - 4; wz <= hubZ - 3; wz++)
            {
                var lz = wz - cMinZ;
                if ((uint)lz >= 16U)
                {
                    continue;
                }

                chunk.SetBlockState(lx, stepY, lz, _oakPlanks);
                for (var clearY = stepY + 1; clearY <= stepY + 3; clearY++)
                {
                    ClearIfNotWaterSource(chunk, lx, clearY, lz);
                }
            }
        }
    }

    private void BuildVerticalAccessShaft(
        ChunkColumn chunk,
        int cMinX,
        int cMinZ,
        int shaftX,
        int shaftZ,
        int lowerY,
        Func<int, int, int> surfaceHeightSampler)
    {
        var topSurface = surfaceHeightSampler(shaftX, shaftZ);
        if (topSurface <= OverworldGenerator.SeaLevel + 2)
        {
            return; // Never open a flooded ocean shaft
        }

        var maxShaftY = Math.Min(topSurface + 2, 82);
        for (var wz = shaftZ - 2; wz <= shaftZ + 2; wz++)
        {
            var lz = wz - cMinZ;
            if ((uint)lz >= 16U)
            {
                continue;
            }

            for (var wx = shaftX - 2; wx <= shaftX + 2; wx++)
            {
                var lx = wx - cMinX;
                if ((uint)lx >= 16U)
                {
                    continue;
                }

                var dx = wx - shaftX;
                var dz = wz - shaftZ;
                var isOuter = Math.Abs(dx) == 2 || Math.Abs(dz) == 2;
                var isCorner = Math.Abs(dx) == 2 && Math.Abs(dz) == 2;

                for (var y = lowerY; y <= maxShaftY; y++)
                {
                    if (isCorner)
                    {
                        chunk.SetBlockState(lx, y, lz, _oakLog);
                    }
                    else if (isOuter)
                    {
                        if (y <= topSurface || (y % 4 == 0))
                        {
                            chunk.SetBlockState(lx, y, lz, _oakPlanks);
                        }
                    }
                    else
                    {
                        chunk.SetBlockState(lx, y, lz, _air);
                        if (dx == 0 && dz == -1 && y <= maxShaftY - 1)
                        {
                            chunk.SetBlockState(lx, y, lz, _ladder);
                        }
                        else if (dx == 1 && dz == 1 && (y - lowerY) % 8 == 4)
                        {
                            chunk.SetBlockState(lx, y, lz, _torch);
                        }
                    }
                }

                if (Math.Abs(dx) <= 1 && Math.Abs(dz) <= 1)
                {
                    chunk.SetBlockState(lx, lowerY, lz, _oakPlanks);
                }
            }
        }
    }

    private void BuildCorridorAlongX(ChunkColumn chunk, int cMinX, int cMinZ, int minWx, int maxWx, int centerWz, int floorY, int seedSalt)
    {
        var startWx = Math.Max(minWx, cMinX);
        var endWx = Math.Min(maxWx, cMinX + 15);
        if (startWx > endWx)
        {
            return;
        }

        for (var wx = startWx; wx <= endWx; wx++)
        {
            var lx = wx - cMinX;
            var isSupportArch = FloorMod(wx, 5) == 0;
            var h = Hash(_seed, wx, centerWz, seedSalt);

            for (var dz = -1; dz <= 1; dz++)
            {
                var wz = centerWz + dz;
                var lz = wz - cMinZ;
                if ((uint)lz >= 16U)
                {
                    continue;
                }

                EnsureCorridorFloorAndBridge(chunk, lx, floorY, lz, isArchPost: isSupportArch && dz != 0);

                for (var y = floorY + 1; y <= floorY + 3; y++)
                {
                    ClearIfNotWaterSource(chunk, lx, y, lz);
                }

                if (isSupportArch)
                {
                    if (dz != 0)
                    {
                        chunk.SetBlockState(lx, floorY + 1, lz, _oakFence);
                        chunk.SetBlockState(lx, floorY + 2, lz, _oakFence);
                    }

                    chunk.SetBlockState(lx, floorY + 3, lz, _oakPlanks);
                    if (dz == 0 && FloorMod(wx, 10) == 0)
                    {
                        chunk.SetBlockState(lx, floorY + 2, lz, _torch);
                    }
                }
                else if (dz == 0 && (h & 7UL) != 0)
                {
                    chunk.SetBlockState(lx, floorY + 1, lz, _rail);
                }
                else if (dz != 0 && (h & 31UL) == 1)
                {
                    chunk.SetBlockState(lx, floorY + 3, lz, _cobweb);
                }
                else if (dz == 1 && (h & 127UL) == 3)
                {
                    chunk.SetBlockState(lx, floorY + 1, lz, (h & 128UL) == 0 ? _chest : _barrel);
                }
            }
        }
    }

    private void BuildCorridorAlongZ(ChunkColumn chunk, int cMinX, int cMinZ, int minWz, int maxWz, int centerWx, int floorY, int seedSalt)
    {
        var startWz = Math.Max(minWz, cMinZ);
        var endWz = Math.Min(maxWz, cMinZ + 15);
        if (startWz > endWz)
        {
            return;
        }

        for (var wz = startWz; wz <= endWz; wz++)
        {
            var lz = wz - cMinZ;
            var isSupportArch = FloorMod(wz, 5) == 0;
            var h = Hash(_seed, centerWx, wz, seedSalt);

            for (var dx = -1; dx <= 1; dx++)
            {
                var wx = centerWx + dx;
                var lx = wx - cMinX;
                if ((uint)lx >= 16U)
                {
                    continue;
                }

                EnsureCorridorFloorAndBridge(chunk, lx, floorY, lz, isArchPost: isSupportArch && dx != 0);

                for (var y = floorY + 1; y <= floorY + 3; y++)
                {
                    ClearIfNotWaterSource(chunk, lx, y, lz);
                }

                if (isSupportArch)
                {
                    if (dx != 0)
                    {
                        chunk.SetBlockState(lx, floorY + 1, lz, _oakFence);
                        chunk.SetBlockState(lx, floorY + 2, lz, _oakFence);
                    }

                    chunk.SetBlockState(lx, floorY + 3, lz, _oakPlanks);
                    if (dx == 0 && FloorMod(wz, 10) == 0)
                    {
                        chunk.SetBlockState(lx, floorY + 2, lz, _torch);
                    }
                }
                else if (dx == 0 && (h & 7UL) != 0)
                {
                    chunk.SetBlockState(lx, floorY + 1, lz, _rail);
                }
                else if (dx != 0 && (h & 31UL) == 2)
                {
                    chunk.SetBlockState(lx, floorY + 3, lz, _cobweb);
                }
            }
        }
    }

    private void EnsureCorridorFloorAndBridge(ChunkColumn chunk, int lx, int floorY, int lz, bool isArchPost)
    {
        var current = chunk.GetBlockState(lx, floorY, lz);
        if (current.IsAir || current == _water || isArchPost)
        {
            chunk.SetBlockState(lx, floorY, lz, _oakPlanks);
            if (isArchPost)
            {
                // Drop suspended trestle bridge support pier down through open caverns!
                for (var py = floorY - 1; py >= floorY - 10 && py > -60; py--)
                {
                    var below = chunk.GetBlockState(lx, py, lz);
                    if (!below.IsAir && below != _water)
                    {
                        break;
                    }

                    chunk.SetBlockState(lx, py, lz, _oakFence);
                }
            }
        }
    }

    private void ClearIfNotWaterSource(ChunkColumn chunk, int lx, int y, int lz)
    {
        if (chunk.GetBlockState(lx, y, lz) != _water)
        {
            chunk.SetBlockState(lx, y, lz, _air);
        }
    }

    private void GetHubCenter(int rx, int rz, out int hubX, out int hubZ, out int lowerY)
    {
        var h = Hash(_seed, rx, rz, 777);
        hubX = (rx * RegionSizeBlocks) + 48 + (int)(h % 17UL);
        hubZ = (rz * RegionSizeBlocks) + 48 + (int)((h >> 8) % 17UL);
        lowerY = 16 + (int)((h >> 16) % 10UL);
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
