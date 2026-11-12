using OverHorizon.Core;
using OverHorizon.Core.Biomes;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Registry;
using OverHorizon.Shared.Random;
using OverHorizon.World.Lighting;
using OverHorizon.World.Storage;

namespace OverHorizon.World.Generation;

/// <summary>
/// Clean-room deterministic Overworld terrain generator:
/// density relief + biomes + 3D cave carver + surface builder + tree/glowstone features + initial lighting.
/// </summary>
public sealed class OverworldGenerator : IWorldGenerator
{
    /// <summary>Default ocean surface Y coordinate.</summary>
    public const int SeaLevel = 63;

    private readonly CoreRegistries _registries;
    private readonly BiomeSampler _biomeSampler;
    private readonly SimplexNoise2D _detailHeightNoise;
    private readonly PerlinNoise _density3DNoise;
    private readonly PerlinNoise _caveNoiseA;
    private readonly PerlinNoise _caveNoiseB;

    private readonly BlockState _air;
    private readonly BlockState _stone;
    private readonly BlockState _bedrock;
    private readonly BlockState _water;
    private readonly BlockState _oakLog;
    private readonly BlockState _oakLeaves;
    private readonly BlockState _glowstone;

    /// <summary>
    /// Initializes a new instance of the <see cref="OverworldGenerator"/> class.
    /// </summary>
    /// <param name="worldSeed">64-bit world seed.</param>
    /// <param name="registries">Frozen domain registries.</param>
    public OverworldGenerator(long worldSeed, CoreRegistries registries)
    {
        ArgumentNullException.ThrowIfNull(registries);

        WorldSeed = worldSeed;
        _registries = registries;
        _biomeSampler = new BiomeSampler(worldSeed, registries.Biomes);

        var seed = unchecked((ulong)worldSeed);
        _detailHeightNoise = new SimplexNoise2D(SplitMix64.Mix(seed ^ 0xA11CEUL));
        _density3DNoise = new PerlinNoise(SplitMix64.Mix(seed ^ 0xB22DFUL));
        _caveNoiseA = new PerlinNoise(SplitMix64.Mix(seed ^ 0xC33E0UL));
        _caveNoiseB = new PerlinNoise(SplitMix64.Mix(seed ^ 0xD44F1UL));

        var blocks = registries.Blocks;
        _air = BlockState.Air;
        _stone = blocks.GetDefaultState(BuiltInBlocks.Stone);
        _bedrock = blocks.GetDefaultState(BuiltInBlocks.Bedrock);
        _water = blocks.GetDefaultState(BuiltInBlocks.Water);
        _oakLog = blocks.GetDefaultState(BuiltInBlocks.OakLog);
        _oakLeaves = blocks.GetDefaultState(BuiltInBlocks.OakLeaves);
        _glowstone = blocks.GetDefaultState(BuiltInBlocks.Glowstone);
    }

    /// <inheritdoc />
    public long WorldSeed { get; }

    /// <inheritdoc />
    public void GenerateChunk(ChunkColumn chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        Span<ushort> columnBiomes = stackalloc ushort[256];
        Span<int> targetSurfaceY = stackalloc int[256];

        PopulateBiomesAndBaseTerrain(chunk, columnBiomes, targetSurfaceY);
        CarveUndergroundCaves(chunk);
        ApplyBiomeSurfaceLayers(chunk, columnBiomes);
        PlaceDeterministicFeatures(chunk, columnBiomes);

        chunk.RecomputeHeightmaps(_registries.Blocks);
        LightingEngine.InitializeChunkLight(chunk, _registries.Blocks);
        chunk.MarkClean();
    }

    private void PopulateBiomesAndBaseTerrain(
        ChunkColumn chunk,
        Span<ushort> columnBiomes,
        Span<int> targetSurfaceY)
    {
        var minBlockX = chunk.Position.MinBlockX;
        var minBlockZ = chunk.Position.MinBlockZ;

        for (var lz = 0; lz < 16; lz++)
        {
            var wz = minBlockZ + lz;
            for (var lx = 0; lx < 16; lx++)
            {
                var wx = minBlockX + lx;
                var idx = (lz << 4) | lx;

                var biomeId = _biomeSampler.SampleBiomeId(wx, wz);
                columnBiomes[idx] = biomeId;

                var continental = _biomeSampler.SampleContinentalness(wx, wz);
                var detail = _detailHeightNoise.SampleFractal2D(wx * 0.015, wz * 0.015, octaves: 3);
                var baseHeight = 66.0 + (continental * 24.0) + (detail * 8.0);
                targetSurfaceY[idx] = Math.Clamp((int)Math.Round(baseHeight), 38, 128);
            }
        }

        // Populate 4x4x4 section biome palettes.
        for (var s = 0; s < ChunkColumn.SectionCount; s++)
        {
            var sec = chunk.GetSection(s);
            for (var qz = 0; qz < 4; qz++)
            {
                for (var qx = 0; qx < 4; qx++)
                {
                    var sampleBiome = columnBiomes[((qz * 4) << 4) | (qx * 4)];
                    for (var qy = 0; qy < 4; qy++)
                    {
                        sec.SetBiomeId(qx, qy, qz, sampleBiome);
                    }
                }
            }
        }

        // Fill bedrock, stone density volume, and ocean water.
        for (var lz = 0; lz < 16; lz++)
        {
            var wz = minBlockZ + lz;
            for (var lx = 0; lx < 16; lx++)
            {
                var wx = minBlockX + lx;
                var surfY = targetSurfaceY[(lz << 4) | lx];
                var bedrockRng = Xoroshiro128PlusPlus.FromPosition(WorldSeed, wx, EngineConstants.WorldMinY, wz);
                var bedrockTop = EngineConstants.WorldMinY + bedrockRng.NextInt32(4);

                var maxScanY = Math.Max(surfY + 6, SeaLevel);
                for (var y = EngineConstants.WorldMinY; y <= maxScanY; y++)
                {
                    if (y == EngineConstants.WorldMinY || y <= bedrockTop)
                    {
                        _ = chunk.SetBlockStateRaw(lx, y, lz, _bedrock);
                        continue;
                    }

                    var gradient = (surfY - y) * 0.12;
                    var densityPerturb = y >= surfY - 8
                        ? _density3DNoise.Sample3D(wx * 0.04, y * 0.04, wz * 0.04) * 0.45
                        : 0.0;

                    if (gradient + densityPerturb > 0.0)
                    {
                        _ = chunk.SetBlockStateRaw(lx, y, lz, _stone);
                    }
                    else if (y <= SeaLevel)
                    {
                        _ = chunk.SetBlockStateRaw(lx, y, lz, _water);
                    }
                }
            }
        }
    }

    private static int FindTopSolidGround(ChunkColumn chunk, int lx, int lz)
    {
        for (var y = 135; y >= SeaLevel; y--)
        {
            var state = chunk.GetBlockState(lx, y, lz);
            if (!state.IsAir)
            {
                return y;
            }
        }

        return -1;
    }

    private void CarveUndergroundCaves(ChunkColumn chunk)
    {
        var minX = chunk.Position.MinBlockX;
        var minZ = chunk.Position.MinBlockZ;

        for (var y = -52; y <= 48; y += 1)
        {
            for (var lz = 0; lz < 16; lz++)
            {
                var wz = minZ + lz;
                for (var lx = 0; lx < 16; lx++)
                {
                    if (chunk.GetBlockState(lx, y, lz) != _stone)
                    {
                        continue;
                    }

                    var wx = minX + lx;
                    var nA = _caveNoiseA.Sample3D(wx * 0.045, y * 0.055, wz * 0.045);
                    var nB = _caveNoiseB.Sample3D(wx * 0.045, y * 0.055, wz * 0.045);

                    // Spaghetti tunnel intersection where both zero-crossings coincide.
                    if (((nA * nA) + (nB * nB)) < 0.012)
                    {
                        _ = chunk.SetBlockStateRaw(lx, y, lz, _air);
                    }
                }
            }
        }
    }

    private void ApplyBiomeSurfaceLayers(ChunkColumn chunk, ReadOnlySpan<ushort> columnBiomes)
    {
        for (var lz = 0; lz < 16; lz++)
        {
            for (var lx = 0; lx < 16; lx++)
            {
                var biome = _biomeSampler.GetBiome(columnBiomes[(lz << 4) | lx]);
                var depthRemaining = -1;

                for (var y = 136; y >= -60; y--)
                {
                    var current = chunk.GetBlockState(lx, y, lz);
                    if (current.IsAir || current == _water)
                    {
                        depthRemaining = -1;
                        continue;
                    }

                    if (current != _stone)
                    {
                        continue;
                    }

                    if (depthRemaining < 0)
                    {
                        var above = chunk.GetBlockState(lx, y + 1, lz);
                        var topBlock = above == _water ? biome.UnderSurfaceState : biome.SurfaceState;
                        _ = chunk.SetBlockStateRaw(lx, y, lz, topBlock);
                        depthRemaining = 3;
                    }
                    else if (depthRemaining > 0)
                    {
                        _ = chunk.SetBlockStateRaw(lx, y, lz, biome.UnderSurfaceState);
                        depthRemaining--;
                    }
                }
            }
        }
    }

    private void PlaceDeterministicFeatures(ChunkColumn chunk, ReadOnlySpan<ushort> columnBiomes)
    {
        var rng = Xoroshiro128PlusPlus.FromPosition(WorldSeed, chunk.Position.X, 1, chunk.Position.Z);

        // Underground glowstone crystal cluster in deep caves (exercises BlockLight propagation).
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var gx = 2 + rng.NextInt32(12);
            var gy = -48 + rng.NextInt32(64);
            var gz = 2 + rng.NextInt32(12);

            if (chunk.GetBlockState(gx, gy, gz).IsAir && chunk.GetBlockState(gx, gy + 1, gz) == _stone)
            {
                _ = chunk.SetBlockStateRaw(gx, gy, gz, _glowstone);
            }
        }

        // Interior oak trees (kept in [2..13] so leaves never cross chunk boundaries).
        var forestKey = BuiltInBiomes.Forest;
        var plainsKey = BuiltInBiomes.Plains;

        for (var treeAttempt = 0; treeAttempt < 3; treeAttempt++)
        {
            var tx = 3 + rng.NextInt32(10);
            var tz = 3 + rng.NextInt32(10);
            var biome = _biomeSampler.GetBiome(columnBiomes[(tz << 4) | tx]);

            var allowed = biome.Key == forestKey || (biome.Key == plainsKey && treeAttempt == 0 && rng.NextInt32(3) == 0);
            if (!allowed)
            {
                continue;
            }

            var groundY = FindTopSolidGround(chunk, tx, tz);
            if (groundY <= SeaLevel || groundY >= 120)
            {
                continue;
            }

            var trunkHeight = 4 + rng.NextInt32(2);
            PlaceTree(chunk, tx, groundY + 1, tz, trunkHeight);
        }
    }

    private void PlaceTree(ChunkColumn chunk, int x, int baseY, int z, int trunkHeight)
    {
        var canopyTop = baseY + trunkHeight;
        for (var y = canopyTop - 2; y <= canopyTop + 1; y++)
        {
            var radius = y >= canopyTop ? 1 : 2;
            for (var dz = -radius; dz <= radius; dz++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Abs(dx) == radius && Math.Abs(dz) == radius && y > canopyTop - 2)
                    {
                        continue;
                    }

                    if (chunk.GetBlockState(x + dx, y, z + dz).IsAir)
                    {
                        _ = chunk.SetBlockStateRaw(x + dx, y, z + dz, _oakLeaves);
                    }
                }
            }
        }

        for (var i = 0; i < trunkHeight; i++)
        {
            _ = chunk.SetBlockStateRaw(x, baseY + i, z, _oakLog);
        }
    }
}
