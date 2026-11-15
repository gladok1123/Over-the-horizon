using OverHorizon.Core.Biomes;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Registry;
using OverHorizon.World.Storage;

namespace OverHorizon.World.Generation;

/// <summary>
/// Deterministic cross-chunk feature populator generating Oak, Birch, Cherry Blossom, Conical Spruce,
/// Savanna flat-crown trees, and towering <c>PackedIce</c> spires across all 10 Overworld biomes.
/// </summary>
internal sealed class TreeFeaturePopulator
{
    private const int CellSize = 7;

    private readonly long _seed;
    private readonly BlockState _dirt;
    private readonly BlockState _grass;
    private readonly BlockState _snow;
    private readonly BlockState _oakLog;
    private readonly BlockState _oakLeaves;
    private readonly BlockState _birchLog;
    private readonly BlockState _birchLeaves;
    private readonly BlockState _cherryLog;
    private readonly BlockState _cherryLeaves;
    private readonly BlockState _spruceLog;
    private readonly BlockState _spruceLeaves;
    private readonly BlockState _packedIce;

    private enum FeatureSpecies : byte
    {
        Oak = 0,
        Birch = 1,
        Cherry = 2,
        Spruce = 3,
        SavannaCanopy = 4,
        IceSpike = 5,
    }

    public TreeFeaturePopulator(long seed, IBlockRegistry blocks)
    {
        _seed = seed;
        _dirt = blocks.GetDefaultState(BuiltInBlocks.Dirt);
        _grass = blocks.GetDefaultState(BuiltInBlocks.GrassBlock);
        _snow = blocks.GetDefaultState(BuiltInBlocks.SnowBlock);
        _oakLog = blocks.GetDefaultState(BuiltInBlocks.OakLog);
        _oakLeaves = blocks.GetDefaultState(BuiltInBlocks.OakLeaves);
        _birchLog = blocks.GetDefaultState(BuiltInBlocks.BirchLog);
        _birchLeaves = blocks.GetDefaultState(BuiltInBlocks.BirchLeaves);
        _cherryLog = blocks.GetDefaultState(BuiltInBlocks.CherryLog);
        _cherryLeaves = blocks.GetDefaultState(BuiltInBlocks.CherryLeaves);
        _spruceLog = blocks.GetDefaultState(BuiltInBlocks.SpruceLog);
        _spruceLeaves = blocks.GetDefaultState(BuiltInBlocks.SpruceLeaves);
        _packedIce = blocks.GetDefaultState(BuiltInBlocks.PackedIce);
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
                if (!TryGetCellTreeSpec(_seed, cellX, cellZ, groundQuery, out var treeX, out var treeZ, out var groundY, out var height, out var species, out var rng))
                {
                    continue;
                }

                if (treeX < chunkMinX - 3 || treeX > chunkMaxX + 3 || treeZ < chunkMinZ - 3 || treeZ > chunkMaxZ + 3)
                {
                    continue;
                }

                var trunkLx = treeX - chunkMinX;
                var trunkLz = treeZ - chunkMinZ;
                if ((uint)trunkLx < 16U && (uint)trunkLz < 16U)
                {
                    var soil = chunk.GetBlockState(trunkLx, groundY, trunkLz);
                    if (soil != _grass && soil != _dirt && soil != _snow)
                    {
                        continue;
                    }

                    if (!chunk.GetBlockState(trunkLx, groundY + 1, trunkLz).IsAir)
                    {
                        continue;
                    }
                }

                if (species == FeatureSpecies.IceSpike)
                {
                    StampIceSpikeIntoChunk(chunk, chunkMinX, chunkMinZ, treeX, groundY, treeZ, height);
                }
                else if (species == FeatureSpecies.Spruce)
                {
                    StampSpruceIntoChunk(chunk, chunkMinX, chunkMinZ, treeX, groundY, treeZ, height);
                }
                else
                {
                    StampBroadleafTreeIntoChunk(chunk, chunkMinX, chunkMinZ, treeX, groundY, treeZ, height, species, rng);
                }
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
        out FeatureSpecies species,
        out ulong rng)
    {
        rng = Hash(seed, cellX, cellZ);
        treeX = (cellX * CellSize) + 1 + (int)(rng % 5UL);
        treeZ = (cellZ * CellSize) + 1 + (int)((rng >> 4) % 5UL);
        trunkHeight = 5;
        species = FeatureSpecies.Oak;

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
            species = ((rng >> 16) % 4UL) == 0 ? FeatureSpecies.Birch : FeatureSpecies.Oak;
        }
        else if (biomeKey == BuiltInBiomes.BirchForest)
        {
            threshold = 64;
            species = FeatureSpecies.Birch;
        }
        else if (biomeKey == BuiltInBiomes.CherryGrove)
        {
            threshold = 54;
            species = FeatureSpecies.Cherry;
        }
        else if (biomeKey == BuiltInBiomes.SnowyTaiga)
        {
            threshold = 56;
            species = FeatureSpecies.Spruce;
        }
        else if (biomeKey == BuiltInBiomes.IceSpikes)
        {
            threshold = 24;
            species = FeatureSpecies.IceSpike;
        }
        else if (biomeKey == BuiltInBiomes.Savanna)
        {
            threshold = 18;
            species = FeatureSpecies.SavannaCanopy;
        }
        else if (biomeKey == BuiltInBiomes.Plains)
        {
            threshold = 14;
            species = ((rng >> 16) % 3UL) == 0 ? FeatureSpecies.Birch : FeatureSpecies.Oak;
        }

        if (spawnRoll >= threshold)
        {
            return false;
        }

        trunkHeight = species switch
        {
            FeatureSpecies.Spruce => 7 + (int)((rng >> 20) % 3UL),
            FeatureSpecies.IceSpike => 8 + (int)((rng >> 20) % 7UL),
            FeatureSpecies.Cherry => 5 + (int)((rng >> 20) % 3UL),
            _ => 5 + (int)((rng >> 20) % 3UL),
        };
        return true;
    }

    private void StampBroadleafTreeIntoChunk(
        ChunkColumn chunk,
        int chunkMinX,
        int chunkMinZ,
        int treeX,
        int groundY,
        int treeZ,
        int trunkHeight,
        FeatureSpecies species,
        ulong rng)
    {
        var logState = species switch
        {
            FeatureSpecies.Birch => _birchLog,
            FeatureSpecies.Cherry => _cherryLog,
            _ => _oakLog,
        };
        var leafState = species switch
        {
            FeatureSpecies.Birch => _birchLeaves,
            FeatureSpecies.Cherry => _cherryLeaves,
            _ => _oakLeaves,
        };
        var topY = groundY + trunkHeight;
        var minLayer = species == FeatureSpecies.SavannaCanopy ? -1 : -2;

        for (var layer = minLayer; layer <= 1; layer++)
        {
            var y = topY + layer;
            if (y >= 316) continue;

            var radius = 1;
            if (layer <= -1)
            {
                radius = species == FeatureSpecies.Cherry ? 3 : 2;
            }
            else if (species == FeatureSpecies.SavannaCanopy && layer == 0)
            {
                radius = 2;
            }

            for (var dz = -radius; dz <= radius; dz++)
            {
                var lz = treeZ + dz - chunkMinZ;
                if ((uint)lz >= 16U) continue;

                for (var dx = -radius; dx <= radius; dx++)
                {
                    var lx = treeX + dx - chunkMinX;
                    if ((uint)lx >= 16U) continue;

                    var isCorner = Math.Abs(dx) == radius && Math.Abs(dz) == radius;
                    if (isCorner)
                    {
                        if (layer == 1 || radius == 3)
                        {
                            continue;
                        }

                        var cornerBit = ((dx + 2) * 3) + dz + 2 + ((layer + 2) * 7);
                        if (((rng >> (24 + (cornerBit & 15))) & 1UL) == 0)
                        {
                            continue;
                        }
                    }

                    if (chunk.GetBlockState(lx, y, lz).IsAir)
                    {
                        _ = chunk.SetBlockStateRaw(lx, y, lz, leafState);
                    }
                }
            }
        }

        StampTrunkColumn(chunk, chunkMinX, chunkMinZ, treeX, groundY, treeZ, trunkHeight, logState);
    }

    private void StampSpruceIntoChunk(
        ChunkColumn chunk,
        int chunkMinX,
        int chunkMinZ,
        int treeX,
        int groundY,
        int treeZ,
        int trunkHeight)
    {
        var topY = groundY + trunkHeight;
        for (var h = 2; h <= trunkHeight + 1; h++)
        {
            var y = groundY + h;
            if (y >= 316) continue;

            var fromTop = topY + 1 - y;
            int radius;
            if (fromTop <= 0)
            {
                radius = 0;
            }
            else if (fromTop == 1)
            {
                radius = 1;
            }
            else
            {
                radius = (fromTop & 1) == 0 ? 2 : 1;
            }
            for (var dz = -radius; dz <= radius; dz++)
            {
                var lz = treeZ + dz - chunkMinZ;
                if ((uint)lz >= 16U) continue;
                for (var dx = -radius; dx <= radius; dx++)
                {
                    var lx = treeX + dx - chunkMinX;
                    if ((uint)lx >= 16U) continue;
                    if (radius > 0 && Math.Abs(dx) == radius && Math.Abs(dz) == radius) continue;

                    if (chunk.GetBlockState(lx, y, lz).IsAir)
                    {
                        _ = chunk.SetBlockStateRaw(lx, y, lz, _spruceLeaves);
                    }
                }
            }
        }

        StampTrunkColumn(chunk, chunkMinX, chunkMinZ, treeX, groundY, treeZ, trunkHeight, _spruceLog);
    }

    private void StampIceSpikeIntoChunk(
        ChunkColumn chunk,
        int chunkMinX,
        int chunkMinZ,
        int spikeX,
        int groundY,
        int spikeZ,
        int spikeHeight)
    {
        for (var h = -1; h <= spikeHeight; h++)
        {
            var y = groundY + h;
            if (y >= 316) continue;

            var radius = h < spikeHeight / 3 ? 1 : 0;
            for (var dz = -radius; dz <= radius; dz++)
            {
                var lz = spikeZ + dz - chunkMinZ;
                if ((uint)lz >= 16U) continue;
                for (var dx = -radius; dx <= radius; dx++)
                {
                    var lx = spikeX + dx - chunkMinX;
                    if ((uint)lx >= 16U) continue;
                    if (radius == 1 && Math.Abs(dx) == 1 && Math.Abs(dz) == 1) continue;

                    _ = chunk.SetBlockStateRaw(lx, y, lz, _packedIce);
                }
            }
        }
    }

    private void StampTrunkColumn(
        ChunkColumn chunk,
        int chunkMinX,
        int chunkMinZ,
        int treeX,
        int groundY,
        int treeZ,
        int trunkHeight,
        BlockState logState)
    {
        var trunkLx = treeX - chunkMinX;
        var trunkLz = treeZ - chunkMinZ;
        if ((uint)trunkLx >= 16U || (uint)trunkLz >= 16U)
        {
            return;
        }

        _ = chunk.SetBlockStateRaw(trunkLx, groundY, trunkLz, _dirt);
        for (var h = 1; h <= trunkHeight; h++)
        {
            var y = groundY + h;
            var existing = chunk.GetBlockState(trunkLx, y, trunkLz);
            if (existing.IsAir || existing == _oakLeaves || existing == _birchLeaves || existing == _cherryLeaves || existing == _spruceLeaves)
            {
                _ = chunk.SetBlockStateRaw(trunkLx, y, trunkLz, logState);
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
