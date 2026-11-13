using OverHorizon.Core.Biomes;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Registry;
using OverHorizon.World.Storage;

namespace OverHorizon.World.Generation;

/// <summary>
/// Deterministic cross-chunk cave and surface structure generator producing Underground Monster Dungeons,
/// Amethyst Crystal Geodes, Desert Sandstone Pyramids, Forest/Plains Ranger Cabins, and Ruined Obsidian Portals.
/// </summary>
internal sealed class WorldStructurePopulator
{
    private const int UndergroundGridSize = 64;
    private const int SurfaceGridSize = 96;

    private readonly long _seed;
    private readonly BlockState _air;
    private readonly BlockState _water;
    private readonly BlockState _cobblestone;
    private readonly BlockState _mossyCobble;
    private readonly BlockState _stoneBricks;
    private readonly BlockState _mossyStoneBricks;
    private readonly BlockState _spawner;
    private readonly BlockState _chest;
    private readonly BlockState _barrel;
    private readonly BlockState _obsidian;
    private readonly BlockState _cobbledDeepslate;
    private readonly BlockState _amethyst;
    private readonly BlockState _sandstone;
    private readonly BlockState _chiseledSandstone;
    private readonly BlockState _crimsonWool;
    private readonly BlockState _blueWool;
    private readonly BlockState _goldBlock;
    private readonly BlockState _oakLog;
    private readonly BlockState _oakPlanks;
    private readonly BlockState _oakFence;
    private readonly BlockState _glass;
    private readonly BlockState _craftingTable;
    private readonly BlockState _furnace;
    private readonly BlockState _composter;
    private readonly BlockState _lantern;
    private readonly BlockState _hangingLantern;
    private readonly BlockState _trapdoor;

    public WorldStructurePopulator(long seed, IBlockRegistry blocks)
    {
        _seed = seed;
        _air = BlockState.Air;
        _water = blocks.GetDefaultState(BuiltInBlocks.Water);
        _cobblestone = blocks.GetDefaultState(BuiltInBlocks.Cobblestone);
        _mossyCobble = blocks.GetDefaultState(BuiltInBlocks.MossyCobblestone);
        _stoneBricks = blocks.GetDefaultState(BuiltInBlocks.StoneBricks);
        _mossyStoneBricks = blocks.GetDefaultState(BuiltInBlocks.MossyStoneBricks);
        _spawner = blocks.GetDefaultState(BuiltInBlocks.MonsterSpawner);
        _chest = blocks.GetDefaultState(BuiltInBlocks.Chest);
        _barrel = blocks.GetDefaultState(BuiltInBlocks.Barrel);
        _obsidian = blocks.GetDefaultState(BuiltInBlocks.Obsidian);
        _cobbledDeepslate = blocks.GetDefaultState(BuiltInBlocks.CobbledDeepslate);
        _amethyst = blocks.GetDefaultState(BuiltInBlocks.AmethystBlock);
        _sandstone = blocks.GetDefaultState(BuiltInBlocks.Sandstone);
        _chiseledSandstone = blocks.GetDefaultState(BuiltInBlocks.ChiseledSandstone);
        _crimsonWool = blocks.GetDefaultState(BuiltInBlocks.CrimsonWool);
        _blueWool = blocks.GetDefaultState(BuiltInBlocks.BlueWool);
        _goldBlock = blocks.GetDefaultState(BuiltInBlocks.GoldBlock);
        _oakLog = blocks.GetDefaultState(BuiltInBlocks.OakLog);
        _oakPlanks = blocks.GetDefaultState(BuiltInBlocks.OakPlanks);
        _oakFence = blocks.GetDefaultState(BuiltInBlocks.OakFence);
        _glass = blocks.GetDefaultState(BuiltInBlocks.Glass);
        _craftingTable = blocks.GetDefaultState(BuiltInBlocks.CraftingTable);
        _furnace = blocks.GetDefaultState(BuiltInBlocks.Furnace);
        _composter = blocks.WithProperty(blocks.GetDefaultState(BuiltInBlocks.Composter), "level", "4");
        _lantern = blocks.GetDefaultState(BuiltInBlocks.Lantern);
        _hangingLantern = blocks.WithProperty(_lantern, "hanging", "true");
        _trapdoor = blocks.GetDefaultState(BuiltInBlocks.OakTrapdoor);
    }

    public bool IsSurfaceStructureFootprint(int worldX, int worldZ, Func<int, int, (int Height, ResourceKey Biome)> surfaceSampler)
    {
        var gx = FloorDiv(worldX, SurfaceGridSize);
        var gz = FloorDiv(worldZ, SurfaceGridSize);
        GetSurfaceAnchor(gx, gz, out var ax, out var az, out _);
        if (Math.Abs(worldX - ax) > 10 || Math.Abs(worldZ - az) > 10)
        {
            return false;
        }

        var (h, _) = surfaceSampler(ax, az);
        return h > OverworldGenerator.SeaLevel + 2 && h < 92;
    }

    public void PopulateChunk(
        ChunkColumn chunk,
        Func<int, int, (int Height, ResourceKey Biome)> surfaceSampler,
        Func<int, int, bool> isMineshaftShaft)
    {
        var cMinX = chunk.Position.X << 4;
        var cMinZ = chunk.Position.Z << 4;

        PopulateUndergroundDungeonsAndGeodes(chunk, cMinX, cMinZ);
        PopulateSurfaceStructures(chunk, cMinX, cMinZ, surfaceSampler, isMineshaftShaft);
    }

    private void PopulateUndergroundDungeonsAndGeodes(ChunkColumn chunk, int cMinX, int cMinZ)
    {
        var minGx = FloorDiv(cMinX - 10, UndergroundGridSize);
        var maxGx = FloorDiv(cMinX + 25, UndergroundGridSize);
        var minGz = FloorDiv(cMinZ - 10, UndergroundGridSize);
        var maxGz = FloorDiv(cMinZ + 25, UndergroundGridSize);

        for (var gz = minGz; gz <= maxGz; gz++)
        {
            for (var gx = minGx; gx <= maxGx; gx++)
            {
                var h = Hash(_seed, gx, gz, 0x51A7);
                var kind = h % 10UL;
                var cx = (gx * UndergroundGridSize) + 16 + (int)((h >> 8) % 32UL);
                var cz = (gz * UndergroundGridSize) + 16 + (int)((h >> 16) % 32UL);

                if (kind < 4UL)
                {
                    var dy = -28 + (int)((h >> 24) % 58UL);
                    BuildMonsterDungeon(chunk, cMinX, cMinZ, cx, dy, cz, h);
                }
                else if (kind < 7UL)
                {
                    var gy = -42 + (int)((h >> 24) % 48UL);
                    BuildAmethystGeode(chunk, cMinX, cMinZ, cx, gy, cz, h);
                }
            }
        }
    }

    private void BuildMonsterDungeon(ChunkColumn chunk, int cMinX, int cMinZ, int cx, int floorY, int cz, ulong hash)
    {
        if (cMinX + 15 < cx - 4 || cMinX > cx + 4 || cMinZ + 15 < cz - 4 || cMinZ > cz + 4)
        {
            return;
        }

        for (var wz = cz - 3; wz <= cz + 3; wz++)
        {
            var lz = wz - cMinZ;
            if ((uint)lz >= 16U) continue;
            for (var wx = cx - 3; wx <= cx + 3; wx++)
            {
                var lx = wx - cMinX;
                if ((uint)lx >= 16U) continue;

                var isWall = Math.Abs(wx - cx) == 3 || Math.Abs(wz - cz) == 3;
                var isDoor = (wx == cx && Math.Abs(wz - cz) == 3) || (wz == cz && Math.Abs(wx - cx) == 3);

                for (var y = floorY; y <= floorY + 4; y++)
                {
                    if (y == floorY || y == floorY + 4 || (isWall && !(isDoor && y <= floorY + 2)))
                    {
                        var mossy = ((Hash(_seed, wx, wz, y) + hash) & 3UL) != 0;
                        _ = chunk.SetBlockStateRaw(lx, y, lz, mossy ? _mossyCobble : _cobblestone);
                    }
                    else
                    {
                        _ = chunk.SetBlockStateRaw(lx, y, lz, _air);
                    }
                }

                if (wx == cx && wz == cz)
                {
                    _ = chunk.SetBlockStateRaw(lx, floorY + 1, lz, _spawner);
                }
                else if (wx == cx - 2 && wz == cz - 2)
                {
                    _ = chunk.SetBlockStateRaw(lx, floorY + 1, lz, _chest);
                }
                else if (wx == cx + 2 && wz == cz + 2)
                {
                    _ = chunk.SetBlockStateRaw(lx, floorY + 1, lz, _barrel);
                }
            }
        }
    }

    private void BuildAmethystGeode(ChunkColumn chunk, int cMinX, int cMinZ, int cx, int cy, int cz, ulong hash)
    {
        const int radius = 6;
        if (cMinX + 15 < cx - radius || cMinX > cx + radius || cMinZ + 15 < cz - radius || cMinZ > cz + radius)
        {
            return;
        }

        var crackDirX = (hash & 1UL) == 0 ? 1 : -1;
        for (var wz = cz - radius; wz <= cz + radius; wz++)
        {
            var lz = wz - cMinZ;
            if ((uint)lz >= 16U) continue;
            for (var wx = cx - radius; wx <= cx + radius; wx++)
            {
                var lx = wx - cMinX;
                if ((uint)lx >= 16U) continue;

                var dx = wx - cx;
                var dz = wz - cz;
                for (var dy = -radius; dy <= radius; dy++)
                {
                    var y = cy + dy;
                    if (y <= -60 || y >= 48) continue;

                    var distSq = (dx * dx) + (dy * dy) + (dz * dz);
                    if (distSq > 34) continue;

                    var isCrackOpening = dx * crackDirX >= 2 && Math.Abs(dy) <= 1 && Math.Abs(dz) <= 1;
                    if (isCrackOpening || distSq <= 11)
                    {
                        _ = chunk.SetBlockStateRaw(lx, y, lz, _air);
                    }
                    else if (distSq <= 19)
                    {
                        _ = chunk.SetBlockStateRaw(lx, y, lz, _amethyst);
                    }
                    else if (distSq <= 26)
                    {
                        _ = chunk.SetBlockStateRaw(lx, y, lz, _cobbledDeepslate);
                    }
                    else
                    {
                        _ = chunk.SetBlockStateRaw(lx, y, lz, _obsidian);
                    }
                }
            }
        }
    }

    private void PopulateSurfaceStructures(
        ChunkColumn chunk,
        int cMinX,
        int cMinZ,
        Func<int, int, (int Height, ResourceKey Biome)> surfaceSampler,
        Func<int, int, bool> isMineshaftShaft)
    {
        var minGx = FloorDiv(cMinX - 12, SurfaceGridSize);
        var maxGx = FloorDiv(cMinX + 27, SurfaceGridSize);
        var minGz = FloorDiv(cMinZ - 12, SurfaceGridSize);
        var maxGz = FloorDiv(cMinZ + 27, SurfaceGridSize);

        for (var gz = minGz; gz <= maxGz; gz++)
        {
            for (var gx = minGx; gx <= maxGx; gx++)
            {
                GetSurfaceAnchor(gx, gz, out var ax, out var az, out var variantHash);
                if (cMinX + 15 < ax - 10 || cMinX > ax + 10 || cMinZ + 15 < az - 10 || cMinZ > az + 10)
                {
                    continue;
                }

                if (isMineshaftShaft(ax, az)) continue;

                var (centerY, biome) = surfaceSampler(ax, az);
                if (centerY <= OverworldGenerator.SeaLevel + 2 || centerY >= 90 || biome == BuiltInBiomes.Ocean)
                {
                    continue;
                }

                // Require reasonably gentle slope so structures sit naturally on terrain
                var (northY, _) = surfaceSampler(ax, az - 5);
                var (southY, _) = surfaceSampler(ax, az + 5);
                var (eastY, _) = surfaceSampler(ax + 5, az);
                if (Math.Abs(northY - centerY) > 4 || Math.Abs(southY - centerY) > 4 || Math.Abs(eastY - centerY) > 4)
                {
                    continue;
                }

                if (biome == BuiltInBiomes.Desert)
                {
                    BuildDesertPyramid(chunk, cMinX, cMinZ, ax, centerY, az);
                }
                else if ((variantHash % 3UL) == 0UL)
                {
                    BuildRuinedObsidianPortal(chunk, cMinX, cMinZ, ax, centerY, az);
                }
                else
                {
                    BuildRangerCabinAndTower(chunk, cMinX, cMinZ, ax, centerY, az);
                }
            }
        }
    }

    private void BuildDesertPyramid(ChunkColumn chunk, int cMinX, int cMinZ, int ax, int baseY, int az)
    {
        for (var wz = az - 8; wz <= az + 8; wz++)
        {
            var lz = wz - cMinZ;
            if ((uint)lz >= 16U) continue;
            for (var wx = ax - 8; wx <= ax + 8; wx++)
            {
                var lx = wx - cMinX;
                if ((uint)lx >= 16U) continue;

                var dx = Math.Abs(wx - ax);
                var dz = Math.Abs(wz - az);
                var cheb = Math.Max(dx, dz);

                // Solid sandstone foundation plinth down into desert dunes
                for (var fy = baseY - 4; fy <= baseY; fy++)
                {
                    _ = chunk.SetBlockStateRaw(lx, fy, lz, _sandstone);
                }

                // Clear sky above pyramid
                for (var cy = baseY + 1; cy <= baseY + 10; cy++)
                {
                    if (chunk.GetBlockState(lx, cy, lz) != _water)
                    {
                        _ = chunk.SetBlockStateRaw(lx, cy, lz, _air);
                    }
                }

                // Stepped pyramid shell (height 8)
                var maxLayer = 8 - cheb;
                for (var h = 1; h <= maxLayer; h++)
                {
                    var isShell = h == maxLayer || cheb >= 7;
                    var isDoor = wz == az + 8 && dx <= 1 && h <= 2;
                    if (isDoor) continue;
                    if (isShell)
                    {
                        var block = (h == 3 || h == 6) ? _chiseledSandstone : _sandstone;
                        _ = chunk.SetBlockStateRaw(lx, baseY + h, lz, block);
                    }
                }

                // Mosaic center floor & hidden subterranean treasure chamber!
                if (dx == 0 && dz == 0)
                {
                    _ = chunk.SetBlockStateRaw(lx, baseY, lz, _blueWool);
                }
                else if (dx + dz == 1 || (dx == 1 && dz == 1))
                {
                    _ = chunk.SetBlockStateRaw(lx, baseY, lz, _crimsonWool);
                }

                if (dx <= 2 && dz <= 2)
                {
                    for (var vy = baseY - 8; vy <= baseY - 5; vy++)
                    {
                        var vaultWall = dx == 2 || dz == 2 || vy == baseY - 8;
                        _ = chunk.SetBlockStateRaw(lx, vy, lz, vaultWall ? _chiseledSandstone : _air);
                    }

                    if (wx == ax - 1 && wz == az) _ = chunk.SetBlockStateRaw(lx, baseY - 7, lz, _chest);
                    else if (wx == ax + 1 && wz == az) _ = chunk.SetBlockStateRaw(lx, baseY - 7, lz, _goldBlock);
                }
            }
        }
    }

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
                    else if (isFrontDoor && h <= 2) _ = chunk.SetBlockStateRaw(lx, baseY + h, lz, _air);
                    else if (isWindow && h == 2) _ = chunk.SetBlockStateRaw(lx, baseY + h, lz, _glass);
                    else if (isWall) _ = chunk.SetBlockStateRaw(lx, baseY + h, lz, h == 1 ? _stoneBricks : _oakPlanks);
                }

                // Watchtower battlement roof + trapdoor hatch
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
