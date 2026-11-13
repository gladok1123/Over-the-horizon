using OverHorizon.Core.Blocks;
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
    private readonly BlockStateId _dirt;
    private readonly BlockStateId _grass;
    private readonly BlockStateId _oakLog;
    private readonly BlockStateId _oakLeaves;
    private readonly BlockStateId _birchLog;
    private readonly BlockStateId _birchLeaves;

    public TreeFeaturePopulator(long seed, IBlockRegistry blocks)
    {
        _seed = seed;
        _dirt = blocks.GetByKey(BuiltInBlocks.Dirt).DefaultState;
        _grass = blocks.GetByKey(BuiltInBlocks.GrassBlock).DefaultState;
        _oakLog = blocks.GetByKey(BuiltInBlocks.OakLog).DefaultState;
        _oakLeaves = blocks.GetByKey(BuiltInBlocks.OakLeaves).DefaultState;
        _birchLog = blocks.GetByKey(BuiltInBlocks.BirchLog).DefaultState;
        _birchLeaves = blocks.GetByKey(BuiltInBlocks.BirchLeaves).DefaultState;
    }

    public void PopulateChunk(ChunkColumn chunk, Func<int, int, (bool Valid, int GroundY, BiomeKind Biome)> groundQuery)
    {
        var chunkMinX = chunk.Position.X << 4;
        var chunkMinZ = chunk.Position.Z << 4;
        var chunkMaxX = chunkMinX + 15;
        var chunkMaxZ = chunkMinZ + 15;

        // Inspect all 7x7 cells within a 3-block canopy halo so canopies cross chunk seams seamlessly!
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

                // Extra safety when trunk itself is inside this chunk: verify ground block is genuine grass/dirt and space above is clear!
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
        Func<int, int, (bool Valid, int GroundY, BiomeKind Biome)> groundQuery,
        out int treeX,
        out int treeZ,
        out int groundY,
        out int trunkHeight,
        out bool isBirch,
        out ulong rng)
    {
        rng = Hash(seed, cellX, cellZ);

        // Keep 1-block inner padding inside each 7x7 cell so adjacent-cell trees are ALWAYS >= 3 blocks apart (trunks never touch!)
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
        var biome = sample.Biome;

        // Biome-appropriate density: lush forests, scattered plains groves, sparse lower mountain pines/oaks, barren deserts/oceans
        var spawnRoll = (int)((rng >> 8) % 100UL);
        var threshold = biome switch
        {
            BiomeKind.Forest => 62,
            BiomeKind.Plains => 10,
            BiomeKind.Mountains when groundY < 86 => 16,
            _ => 0,
        };

        if (spawnRoll >= threshold)
        {
            return false;
        }

        isBirch = biome switch
        {
            BiomeKind.Forest => ((rng >> 16) % 4UL) == 0,
            BiomeKind.Plains => ((rng >> 16) % 3UL) == 0,
            _ => false,
        };

        trunkHeight = isBirch
            ? 5 + (int)((rng >> 20) % 3UL) // 5..7 slender birch
            : 5 + (int)((rng >> 20) % 3UL); // 5..7 classic oak

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

        // 1. Place rounded 4-layer foliage crown FIRST (only into Air)
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

                    // Authentic Minecraft corner trimming: trim top crown corners and randomly round lower canopy corners!
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
                            var cornerBit = (int)(((dx + 2) * 3) + dz + 2 + ((layer + 2) * 7));
                            if (((rng >> (24 + (cornerBit & 15))) & 1UL) == 0)
                            {
                                continue;
                            }
                        }
                    }

                    if (chunk.GetBlockState(lx, y, lz).IsAir)
                    {
                        chunk.SetBlockState(lx, y, lz, leafState);
                    }
                }
            }
        }

        // 2. Place solid vertical log trunk & convert grass root under trunk to dirt
        var trunkLx = treeX - chunkMinX;
        var trunkLz = treeZ - chunkMinZ;
        if ((uint)trunkLx < 16U && (uint)trunkLz < 16U)
        {
            chunk.SetBlockState(trunkLx, groundY, trunkLz, _dirt);
            for (var h = 1; h <= trunkHeight; h++)
            {
                var y = groundY + h;
                var existing = chunk.GetBlockState(trunkLx, y, trunkLz);
                if (existing.IsAir || existing == _oakLeaves || existing == _birchLeaves)
                {
                    chunk.SetBlockState(trunkLx, y, trunkLz, logState);
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
