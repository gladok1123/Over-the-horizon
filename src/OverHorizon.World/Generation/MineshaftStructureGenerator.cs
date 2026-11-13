using OverHorizon.Core.Blocks;
using OverHorizon.World.Storage;

namespace OverHorizon.World.Generation;

/// <summary>
/// Deterministic multi-chunk structural abandoned mineshaft generator producing 2-level branching timbered galleries,
/// properly oriented <c>east_west</c> and <c>north_south</c> minecart tracks, intersection-safe oak support arches,
/// suspended cavern trestle bridges, spider spawner alcoves, grand central 2-story pit chambers, and surface ladder shafts.
/// </summary>
internal sealed partial class MineshaftStructureGenerator
{
    private const int RegionSizeBlocks = 112; // 7x7 chunks per mineshaft region

    private readonly long _seed;
    private readonly BlockState _air;
    private readonly BlockState _water;
    private readonly BlockState _oakPlanks;
    private readonly BlockState _oakLog;
    private readonly BlockState _oakFence;
    private readonly BlockState _cobblestone;
    private readonly BlockState _mossyCobble;
    private readonly BlockState _torchSouth;
    private readonly BlockState _torchEast;
    private readonly BlockState _torchWest;
    private readonly BlockState _hangingLantern;
    private readonly BlockState _railNorthSouth;
    private readonly BlockState _railEastWest;
    private readonly BlockState _cobweb;
    private readonly BlockState _chest;
    private readonly BlockState _barrel;
    private readonly BlockState _spawner;
    private readonly BlockState _ladderSouth;

    public MineshaftStructureGenerator(long seed, IBlockRegistry blocks)
    {
        _seed = seed;
        _air = BlockState.Air;
        _water = blocks.GetDefaultState(BuiltInBlocks.Water);
        _oakPlanks = blocks.GetDefaultState(BuiltInBlocks.OakPlanks);
        _oakLog = blocks.GetDefaultState(BuiltInBlocks.OakLog);
        _oakFence = blocks.GetDefaultState(BuiltInBlocks.OakFence);
        _cobblestone = blocks.GetDefaultState(BuiltInBlocks.Cobblestone);
        _mossyCobble = blocks.GetDefaultState(BuiltInBlocks.MossyCobblestone);

        var baseTorch = blocks.GetDefaultState(BuiltInBlocks.Torch);
        _torchSouth = blocks.WithProperty(baseTorch, "facing", "south");
        _torchEast = blocks.WithProperty(baseTorch, "facing", "east");
        _torchWest = blocks.WithProperty(baseTorch, "facing", "west");

        _hangingLantern = blocks.WithProperty(blocks.GetDefaultState(BuiltInBlocks.Lantern), "hanging", "true");
        var baseRail = blocks.GetDefaultState(BuiltInBlocks.Rail);
        _railNorthSouth = blocks.WithProperty(baseRail, "shape", "north_south");
        _railEastWest = blocks.WithProperty(baseRail, "shape", "east_west");

        _cobweb = blocks.GetDefaultState(BuiltInBlocks.Cobweb);
        _chest = blocks.GetDefaultState(BuiltInBlocks.Chest);
        _barrel = blocks.GetDefaultState(BuiltInBlocks.Barrel);
        _spawner = blocks.GetDefaultState(BuiltInBlocks.MonsterSpawner);
        _ladderSouth = blocks.WithProperty(blocks.GetDefaultState(BuiltInBlocks.Ladder), "facing", "south");
    }

    /// <summary>Returns <see langword="true"/> when <paramref name="worldX"/>/<paramref name="worldZ"/> falls inside an active vertical surface access shaft.</summary>
    public bool IsVerticalAccessShaftColumn(int worldX, int worldZ)
    {
        var rx = FloorDiv(worldX, RegionSizeBlocks);
        var rz = FloorDiv(worldZ, RegionSizeBlocks);
        if (!IsRegionActive(rx, rz))
        {
            return false;
        }

        GetHubCenter(rx, rz, out var hubX, out var hubZ, out _);
        return Math.Abs(worldX - (hubX + 8)) <= 2 && Math.Abs(worldZ - (hubZ - 8)) <= 2;
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
        var maxRz = FloorDiv(chunkMinZ + 71, RegionSizeBlocks);

        for (var rz = minRz; rz <= maxRz; rz++)
        {
            for (var rx = minRx; rx <= maxRx; rx++)
            {
                if (!IsRegionActive(rx, rz))
                {
                    continue;
                }

                GetHubCenter(rx, rz, out var hubX, out var hubZ, out var lowerY);
                if (chunkMaxX < hubX - 54 || chunkMinX > hubX + 54 || chunkMaxZ < hubZ - 54 || chunkMinZ > hubZ + 54)
                {
                    continue;
                }

                var upperY = lowerY + 8;

                // 1. Build branching corridors FIRST so the central hub, staircase, and vertical ladder shaft are never overwritten!
                ReadOnlySpan<int> lowerCrossZ = [hubZ - 24, hubZ, hubZ + 24];
                ReadOnlySpan<int> lowerCrossX = [hubX - 24, hubX, hubX + 24];
                BuildCorridorAlongX(chunk, chunkMinX, chunkMinZ, hubX - 48, hubX + 48, hubZ, lowerY, 11, lowerCrossX, hubX, hubZ);
                BuildCorridorAlongZ(chunk, chunkMinX, chunkMinZ, hubZ - 48, hubZ + 48, hubX, lowerY, 22, lowerCrossZ, hubX, hubZ);
                BuildCorridorAlongZ(chunk, chunkMinX, chunkMinZ, hubZ - 36, hubZ + 36, hubX - 24, lowerY, 33, lowerCrossZ, hubX, hubZ);
                BuildCorridorAlongZ(chunk, chunkMinX, chunkMinZ, hubZ - 36, hubZ + 36, hubX + 24, lowerY, 44, lowerCrossZ, hubX, hubZ);
                BuildCorridorAlongX(chunk, chunkMinX, chunkMinZ, hubX - 36, hubX + 36, hubZ - 24, lowerY, 55, lowerCrossX, hubX, hubZ);
                BuildCorridorAlongX(chunk, chunkMinX, chunkMinZ, hubX - 36, hubX + 36, hubZ + 24, lowerY, 66, lowerCrossX, hubX, hubZ);

                ReadOnlySpan<int> upperCrossZ = [hubZ, hubZ + 18];
                ReadOnlySpan<int> upperCrossX = [hubX];
                BuildCorridorAlongX(chunk, chunkMinX, chunkMinZ, hubX - 36, hubX + 36, hubZ, upperY, 77, upperCrossX, hubX, hubZ);
                BuildCorridorAlongZ(chunk, chunkMinX, chunkMinZ, hubZ - 36, hubZ + 36, hubX, upperY, 88, upperCrossZ, hubX, hubZ);
                BuildCorridorAlongX(chunk, chunkMinX, chunkMinZ, hubX - 28, hubX + 28, hubZ + 18, upperY, 99, upperCrossX, hubX, hubZ);

                BuildSpawnerAlcove(chunk, chunkMinX, chunkMinZ, hubX - 24, hubZ + 28, lowerY);

                // 2. Carve and furnish the 2-level Grand Hub, connecting timber stairs, and intact surface access shaft AFTER corridors!
                BuildGrandHubRoom(chunk, chunkMinX, chunkMinZ, hubX, hubZ, lowerY, isUpper: false);
                BuildGrandHubRoom(chunk, chunkMinX, chunkMinZ, hubX, hubZ, upperY, isUpper: true);
                BuildConnectingStaircase(chunk, chunkMinX, chunkMinZ, hubX, hubZ, lowerY);
                BuildVerticalAccessShaft(chunk, chunkMinX, chunkMinZ, hubX + 8, hubZ - 8, lowerY, surfaceHeightSampler);
            }
        }
    }

    private bool IsRegionActive(int rx, int rz)
    {
        if (Math.Abs(rx) <= 1 && Math.Abs(rz) <= 1)
        {
            return true;
        }

        return (Hash(_seed, rx, rz, 901) % 10UL) < 6UL;
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
                    _ = chunk.SetBlockStateRaw(lx, floorY, lz, floorBlock);
                }
                else
                {
                    _ = chunk.SetBlockStateRaw(lx, floorY, lz, _air);
                }

                for (var y = floorY + 1; y <= floorY + 5; y++)
                {
                    if (isEdgeX && isEdgeZ)
                    {
                        _ = chunk.SetBlockStateRaw(lx, y, lz, _oakLog);
                    }
                    else if (y == floorY + 5 && (isEdgeX || isEdgeZ || Math.Abs(wx - hubX) == 3 || Math.Abs(wz - hubZ) == 3))
                    {
                        _ = chunk.SetBlockStateRaw(lx, y, lz, isEdgeX || isEdgeZ ? _oakLog : _oakPlanks);
                    }
                    else
                    {
                        ClearIfNotWaterSource(chunk, lx, y, lz);
                    }
                }

                if (Math.Abs(wx - hubX) == 3 && Math.Abs(wz - hubZ) == 3)
                {
                    // Ceiling beam at floorY + 5 supports hanging lantern at floorY + 4!
                    _ = chunk.SetBlockStateRaw(lx, floorY + 5, lz, _oakPlanks);
                    _ = chunk.SetBlockStateRaw(lx, floorY + 4, lz, _hangingLantern);
                }

                if (!isUpper && wx == hubX - 4 && wz == hubZ + 4)
                {
                    _ = chunk.SetBlockStateRaw(lx, floorY + 1, lz, _chest);
                }
                else if (!isUpper && wx == hubX + 4 && wz == hubZ + 4)
                {
                    _ = chunk.SetBlockStateRaw(lx, floorY + 1, lz, _barrel);
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

                _ = chunk.SetBlockStateRaw(lx, stepY, lz, _oakPlanks);
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
        if (topSurface <= OverworldGenerator.SeaLevel + 2 || topSurface >= 96)
        {
            return;
        }

        // Connect shaft into the northern cross-gallery via a clean short doorway at lowerY
        for (var wz = shaftZ; wz <= shaftZ + 8; wz++)
        {
            var lz = wz - cMinZ;
            if ((uint)lz >= 16U) continue;
            for (var dx = -1; dx <= 1; dx++)
            {
                var lx = (shaftX + dx) - cMinX;
                if ((uint)lx >= 16U) continue;
                _ = chunk.SetBlockStateRaw(lx, lowerY, lz, _oakPlanks);
                for (var y = lowerY + 1; y <= lowerY + 3; y++)
                {
                    ClearIfNotWaterSource(chunk, lx, y, lz);
                }
            }
        }

        var maxShaftY = topSurface + 1;
        for (var wz = shaftZ - 2; wz <= shaftZ + 2; wz++)
        {
            var lz = wz - cMinZ;
            if ((uint)lz >= 16U) continue;

            for (var wx = shaftX - 2; wx <= shaftX + 2; wx++)
            {
                var lx = wx - cMinX;
                if ((uint)lx >= 16U) continue;

                var dx = wx - shaftX;
                var dz = wz - shaftZ;
                var isOuter = Math.Abs(dx) == 2 || Math.Abs(dz) == 2;
                var isCorner = Math.Abs(dx) == 2 && Math.Abs(dz) == 2;

                for (var y = lowerY; y <= maxShaftY; y++)
                {
                    if (isCorner)
                    {
                        _ = chunk.SetBlockStateRaw(lx, y, lz, _oakLog);
                    }
                    else if (isOuter)
                    {
                        var isDoorway = dz == 2 && Math.Abs(dx) <= 1 && y >= lowerY + 1 && y <= lowerY + 3;
                        if (isDoorway)
                        {
                            ClearIfNotWaterSource(chunk, lx, y, lz);
                        }
                        else if (y <= topSurface || (dx == 0 && dz == -2))
                        {
                            _ = chunk.SetBlockStateRaw(lx, y, lz, _oakPlanks);
                        }
                        else
                        {
                            _ = chunk.SetBlockStateRaw(lx, y, lz, _oakFence);
                        }
                    }
                    else
                    {
                        _ = chunk.SetBlockStateRaw(lx, y, lz, _air);
                        if (dx == 0 && dz == -1 && y >= lowerY + 1 && y <= maxShaftY)
                        {
                            _ = chunk.SetBlockStateRaw(lx, y, lz, _ladderSouth);
                        }
                        else if (dx == 1 && dz == 1 && y <= topSurface - 1 && (y - lowerY) % 8 == 4)
                        {
                            _ = chunk.SetBlockStateRaw(lx, y, lz, _torchWest);
                        }
                    }
                }

                if (Math.Abs(dx) <= 1 && Math.Abs(dz) <= 1)
                {
                    _ = chunk.SetBlockStateRaw(lx, lowerY, lz, _oakPlanks);
                }
            }
        }
    }

    private void BuildSpawnerAlcove(ChunkColumn chunk, int cMinX, int cMinZ, int centerX, int centerZ, int floorY)
    {
        for (var wz = centerZ - 2; wz <= centerZ + 2; wz++)
        {
            var lz = wz - cMinZ;
            if ((uint)lz >= 16U) continue;
            for (var wx = centerX - 2; wx <= centerX + 2; wx++)
            {
                var lx = wx - cMinX;
                if ((uint)lx >= 16U) continue;

                _ = chunk.SetBlockStateRaw(lx, floorY, lz, ((wx + wz) & 1) == 0 ? _mossyCobble : _cobblestone);
                for (var y = floorY + 1; y <= floorY + 3; y++)
                {
                    ClearIfNotWaterSource(chunk, lx, y, lz);
                }

                if (wx == centerX && wz == centerZ)
                {
                    _ = chunk.SetBlockStateRaw(lx, floorY + 1, lz, _spawner);
                }
                else if (wx == centerX + 1 && wz == centerZ + 1)
                {
                    _ = chunk.SetBlockStateRaw(lx, floorY + 1, lz, _chest);
                }
                else if (Math.Abs(wx - centerX) == 1 || Math.Abs(wz - centerZ) == 1)
                {
                    _ = chunk.SetBlockStateRaw(lx, floorY + 2, lz, _cobweb);
                }
            }
        }
    }
}
