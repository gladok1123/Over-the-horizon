using OverHorizon.Core.Biomes;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Registry;
using OverHorizon.World.Storage;

namespace OverHorizon.World.Generation;

/// <summary>
/// Deterministic cross-chunk Oak and Birch tree populator with strict grass/dirt root validation,
/// spacing guarantees, and rounded 4-layer canopies (eliminating floating/stacked trees and chunk-border alleys).
/// </summary>
internal sealed class TreeFeaturePopulator
{
    private const int CellSize = 7;

    private readonly long _seed;
    private readonly BlockState _dirt;
    private readonly BlockState _grass;
    private readonly BlockState _oakLog;
    private readonly BlockState _oakLeaves;
    private readonly BlockState _birchLog;
    private readonly BlockState _birchLeaves;

    public TreeFeaturePopulator(long seed, IBlockRegistry blocks)
    {
        _seed = seed;
        _dirt = blocks.GetDefaultState(BuiltInBlocks.Dirt);
        _grass = blocks.GetDefaultState(BuiltInBlocks.GrassBlock);
        _oakLog = blocks.GetDefaultState(BuiltInBlocks.OakLog);
        _oakLeaves = blocks.GetDefaultState(BuiltInBlocks.OakLeaves);
        _birchLog = blocks.GetDefaultState(BuiltInBlocks.BirchLog);
        _birchLeaves = blocks.GetDefaultState(BuiltInBlocks.BirchLeaves);
    }

    public void PopulateChunk(ChunkColumn chunk, Func<int, int, (bool Valid, int GroundY, ResourceKey BiomeKey)> groundQuery)
    {
        var chunkMinX = chunk.Position.X << 4;
        var chunkMinZ = chunk.Position.Z << 4;
        var chunkMaxX = chunkMinX + 15;
        var chunkMaxZ = chunkMinZ + 15;

        var minCellX = FloorDiv(chunkMinX - 3, CellSize);
        var maxCellX = FloorDiv(chunkMaxX + 3, CellSize);
        var minCellZ = FloorDiv(chunkMinZ - 3, CellSize);
        var maxCellZ = FloorDiv(chunkMaxZ + 3, CellSize);

        for (var cellZ = minCellZ; cellZ <= maxCellZ; cellZ++)
        {
            for (var cellX = minCellX; cellX <= maxCellX; cellX++)
            {
                if (!TryGetCellTreeSpec(_seed, cellX, cellZ, groundQuery, out var treeX, out var treeZ, out var groundY, out var height, out var isBirch, out var rng))
                {
                    continue;
                }

                if (treeX < chunkMinX - 2 || treeX > chunkMaxX + 2 || treeZ < chunkMinZ - 2 || treeZ > chunkMaxZ + 2)
                {
                    continue;
                }

                var trunkLx = treeX - chunkMinX;
                var trunkLz = treeZ - chunkMinZ;
                if ((uint)trunkLx < 16U && (uint)trunkLz < 16U)
                {
                    var soil = chunk.GetBlockState(trunkLx, groundY, trunkLz);
                    if (soil != _grass && soil != _dirt)
                    {
                        continue;
                    }

                    if (!chunk.GetBlockState(trunkLx, groundY + 1, trunkLz).IsAir)
                    {
                        continue;
                    }
                }

                StampTreeIntoChunk(chunk, chunkMinX, chunkMinZ, treeX, groundY, treeZ, height, isBirch, rng);
            }
        }
    }

    private static bool TryGetCellTreeSpec(
        long seed,
        int cellX,
        int cellZ,
        Func<int, int, (bool Valid, int GroundY, ResourceKey BiomeKey)> groundQuery,
        out int treeX,
        out int treeZ,
        out int groundY,
        out int trunkHeight,
        out bool isBirch,
        out ulong rng)
    {
        rng = Hash(seed, cellX, cellZ);
        treeX = (cellX * CellSize) + 1 + (int)(rng % 5UL);
        treeZ = (cellZ * CellSize) + 1 + (int)((rng >> 4) % 5UL);
        trunkHeight = 5;
        isBirch = false;

        var sample = groundQuery(treeX, treeZ);
        groundY = sample.GroundY;
        if (!sample.Valid)
        {
            return false;
        }

        var biomeKey = sample.BiomeKey;
        var spawnRoll = (int)((rng >> 8) % 100UL);
        var threshold = 0;
        if (biomeKey == BuiltInBiomes.Forest)
        {
            threshold = 62;
        }
        else if (biomeKey == BuiltInBiomes.Plains)
        {
            threshold = 14;
        }

        if (spawnRoll >= threshold)
        {
            return false;
        }

        isBirch = biomeKey == BuiltInBiomes.Forest
            ? ((rng >> 16) % 4UL) == 0
            : ((rng >> 16) % 3UL) == 0;

        trunkHeight = 5 + (int)((rng >> 20) % 3UL);
        return true;
    }

    private void StampTreeIntoChunk(
        ChunkColumn chunk,
        int chunkMinX,
        int chunkMinZ,
        int treeX,
        int groundY,
        int treeZ,
        int trunkHeight,
        bool isBirch,
        ulong rng)
    {
        var logState = isBirch ? _birchLog : _oakLog;
        var leafState = isBirch ? _birchLeaves : _oakLeaves;
        var topY = groundY + trunkHeight;

        for (var layer = -2; layer <= 1; layer++)
        {
            var y = topY + layer;
            if (y >= 316)
            {
                continue;
            }

            var radius = layer <= -1 ? 2 : 1;
            for (var dz = -radius; dz <= radius; dz++)
            {
                var lz = treeZ + dz - chunkMinZ;
                if ((uint)lz >= 16U)
                {
                    continue;
                }

                for (var dx = -radius; dx <= radius; dx++)
                {
                    var lx = treeX + dx - chunkMinX;
                    if ((uint)lx >= 16U)
                    {
                        continue;
                    }

                    var isCorner = Math.Abs(dx) == radius && Math.Abs(dz) == radius;
                    if (isCorner)
                    {
                        if (layer == 1)
                        {
                            if (dx != 0 && dz != 0)
                            {
                                continue;
                            }
                        }
                        else
                        {
                            var cornerBit = ((dx + 2) * 3) + dz + 2 + ((layer + 2) * 7);
                            if (((rng >> (24 + (cornerBit & 15))) & 1UL) == 0)
                            {
                                continue;
                            }
                        }
                    }

                    if (chunk.GetBlockState(lx, y, lz).IsAir)
                    {
                        _ = chunk.SetBlockStateRaw(lx, y, lz, leafState);
                    }
                }
            }
        }

        var trunkLx = treeX - chunkMinX;
        var trunkLz = treeZ - chunkMinZ;
        if ((uint)trunkLx < 16U && (uint)trunkLz < 16U)
        {
            _ = chunk.SetBlockStateRaw(trunkLx, groundY, trunkLz, _dirt);
            for (var h = 1; h <= trunkHeight; h++)
            {
                var y = groundY + h;
                var existing = chunk.GetBlockState(trunkLx, y, trunkLz);
                if (existing.IsAir || existing == _oakLeaves || existing == _birchLeaves)
                {
                    _ = chunk.SetBlockStateRaw(trunkLx, y, trunkLz, logState);
                }
            }
        }
    }

    private static int FloorDiv(int a, int b) => a >= 0 ? a / b : ((a - b + 1) / b);

    private static ulong Hash(long seed, int cx, int cz)
    {
        var z = (ulong)seed ^ ((ulong)(uint)cx * 0xBF58476D1CE4E5B9UL) ^ ((ulong)(uint)cz * 0x94D049BB133111EBUL);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
