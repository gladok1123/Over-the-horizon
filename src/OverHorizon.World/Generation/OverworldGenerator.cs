using OverHorizon.Core;
using OverHorizon.Core.Biomes;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Registry;
using OverHorizon.Shared.Random;
using OverHorizon.World.Lighting;
using OverHorizon.World.Storage;

namespace OverHorizon.World.Generation;

/// <summary>
/// Deterministic multi-octave Overworld chunk generator featuring continentalness, mountain ridges,
/// deepslate strata, surface-connected 3D caves, stratified mineral ore veins, multi-chunk
/// structural abandoned mineshafts, and cross-chunk Oak &amp; Birch forests.
/// </summary>
public sealed class OverworldGenerator : IWorldGenerator
{
    /// <summary>Default ocean surface Y coordinate.</summary>
    public const int SeaLevel = 63;

    private readonly CoreRegistries _registries;
    private readonly BiomeSampler _biomeSampler;
    private readonly SimplexNoise2D _erosionNoise;
    private readonly SimplexNoise2D _ridgeNoise;
    private readonly PerlinNoise _densityNoise3D;
    private readonly PerlinNoise _caveNoiseA;
    private readonly PerlinNoise _caveNoiseB;
    private readonly OreVeinPopulator _orePopulator;
    private readonly MineshaftStructureGenerator _mineshaftGenerator;
    private readonly TreeFeaturePopulator _treePopulator;

    private readonly BlockState _air;
    private readonly BlockState _bedrock;
    private readonly BlockState _stone;
    private readonly BlockState _deepslate;
    private readonly BlockState _dirt;
    private readonly BlockState _grass;
    private readonly BlockState _snowBlock;
    private readonly BlockState _sand;
    private readonly BlockState _sandstone;
    private readonly BlockState _water;

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
        _erosionNoise = new SimplexNoise2D(SplitMix64.Mix(seed ^ 0xA11CE002UL));
        _ridgeNoise = new SimplexNoise2D(SplitMix64.Mix(seed ^ 0xA11CE003UL));
        _densityNoise3D = new PerlinNoise(SplitMix64.Mix(seed ^ 0xB22DF004UL));
        _caveNoiseA = new PerlinNoise(SplitMix64.Mix(seed ^ 0xC33E0005UL));
        _caveNoiseB = new PerlinNoise(SplitMix64.Mix(seed ^ 0xD44F1006UL));

        var blocks = registries.Blocks;
        _orePopulator = new OreVeinPopulator(worldSeed, blocks);
        _mineshaftGenerator = new MineshaftStructureGenerator(worldSeed, blocks);
        _treePopulator = new TreeFeaturePopulator(worldSeed, blocks);

        _air = BlockState.Air;
        _bedrock = blocks.GetDefaultState(BuiltInBlocks.Bedrock);
        _stone = blocks.GetDefaultState(BuiltInBlocks.Stone);
        _deepslate = blocks.GetDefaultState(BuiltInBlocks.Deepslate);
        _dirt = blocks.GetDefaultState(BuiltInBlocks.Dirt);
        _grass = blocks.GetDefaultState(BuiltInBlocks.GrassBlock);
        _snowBlock = blocks.GetDefaultState(BuiltInBlocks.SnowBlock);
        _sand = blocks.GetDefaultState(BuiltInBlocks.Sand);
        _sandstone = blocks.GetDefaultState(BuiltInBlocks.Sandstone);
        _water = blocks.GetDefaultState(BuiltInBlocks.Water);
    }

    /// <inheritdoc />
    public long WorldSeed { get; }

    /// <inheritdoc />
    public void GenerateChunk(ChunkColumn chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        var chunkWorldX = chunk.Position.MinBlockX;
        var chunkWorldZ = chunk.Position.MinBlockZ;

        Span<int> baseSurface = stackalloc int[256];
        Span<ushort> columnBiomes = stackalloc ushort[256];

        for (var lz = 0; lz < 16; lz++)
        {
            for (var lx = 0; lx < 16; lx++)
            {
                var wx = chunkWorldX + lx;
                var wz = chunkWorldZ + lz;
                var idx = (lz << 4) | lx;
                baseSurface[idx] = ComputeColumnBaseHeight(wx, wz, out var biomeId);
                columnBiomes[idx] = biomeId;
            }
        }

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

        PopulateBaseTerrainAndCaves(chunk, chunkWorldX, chunkWorldZ, baseSurface);
        ApplySurfaceStrata(chunk, chunkWorldX, chunkWorldZ, columnBiomes);
        _orePopulator.PopulateChunk(chunk);
        _mineshaftGenerator.PopulateChunk(chunk, SampleRawSurfaceHeight);
        _treePopulator.PopulateChunk(chunk, SampleTreeGround);

        chunk.RecomputeHeightmaps(_registries.Blocks);
        LightingEngine.InitializeChunkLight(chunk, _registries.Blocks);
        chunk.MarkClean();
    }

    private int SampleRawSurfaceHeight(int worldX, int worldZ) =>
        ComputeColumnBaseHeight(worldX, worldZ, out _);

    private (bool Valid, int GroundY, ResourceKey BiomeKey) SampleTreeGround(int worldX, int worldZ)
    {
        var baseH = ComputeColumnBaseHeight(worldX, worldZ, out var biomeId);
        var biomeKey = _biomeSampler.GetBiome(biomeId).Key;
        if (biomeKey == BuiltInBiomes.Ocean || biomeKey == BuiltInBiomes.Desert)
        {
            return (false, baseH, biomeKey);
        }

        if (_mineshaftGenerator.IsVerticalAccessShaftColumn(worldX, worldZ))
        {
            return (false, baseH, biomeKey);
        }

        var topY = Math.Min(EngineConstants.WorldMaxY - 12, baseH + 16);
        var minSearchY = Math.Max(SeaLevel + 2, baseH - 12);
        for (var y = topY; y >= minSearchY; y--)
        {
            if (!IsTerrainSolidBeforeCaves(worldX, y, worldZ, baseH))
            {
                continue;
            }

            if (IsCaveCarved(worldX, y, worldZ, baseH) || y >= 92)
            {
                return (false, y, biomeKey);
            }

            return (true, y, biomeKey);
        }

        return (false, baseH, biomeKey);
    }

    private int ComputeColumnBaseHeight(int wx, int wz, out ushort biomeId)
    {
        biomeId = _biomeSampler.SampleBiomeId(wx, wz);
        var continental = _biomeSampler.SampleContinentalness(wx, wz);
        var erosion = _erosionNoise.SampleFractal2D(wx * 0.0045, wz * 0.0045, octaves: 3);
        var ridgeRaw = _ridgeNoise.SampleFractal2D(wx * 0.006, wz * 0.006, octaves: 4);
        var peaks = 1.0 - Math.Abs(ridgeRaw);
        peaks *= peaks;

        var elevation = 65.0 + (continental * 26.0) - (erosion * 9.0) + (peaks * Math.Max(0.0, continental + 0.15) * 38.0);
        return Math.Clamp((int)Math.Round(elevation), 36, 150);
    }

    private bool IsTerrainSolidBeforeCaves(int wx, int y, int wz, int targetSurfaceY)
    {
        if (y <= EngineConstants.WorldMinY + 4) return true;
        if (y > targetSurfaceY + 14) return false;
        if (y < targetSurfaceY - 10) return true;

        var densityOffset = _densityNoise3D.SampleFractal3D(wx * 0.035, y * 0.045, wz * 0.035, octaves: 3) * 8.5;
        return (targetSurfaceY - y + densityOffset) > 0.0;
    }

    private bool IsCaveCarved(int wx, int y, int wz, int surfaceY)
    {
        if (y <= EngineConstants.WorldMinY + 5 || y > surfaceY + 4)
        {
            return false;
        }

        if (surfaceY <= SeaLevel + 2 && y >= surfaceY - 5)
        {
            return false;
        }

        var ca = _caveNoiseA.Sample3D(wx * 0.038, y * 0.048, wz * 0.038);
        var cb = _caveNoiseB.Sample3D(wx * 0.038, y * 0.048, wz * 0.038);
        var spaghetti = (ca * ca) + (cb * cb);
        var cheese = _densityNoise3D.Sample3D(wx * 0.022 + 97.3, y * 0.032 - 41.7, wz * 0.022 + 63.1);
        var threshold = y > 52 ? 0.0105 : 0.0145;
        return spaghetti < threshold || (y < 36 && cheese > 0.56);
    }

    private void PopulateBaseTerrainAndCaves(
        ChunkColumn chunk,
        int chunkWorldX,
        int chunkWorldZ,
        ReadOnlySpan<int> baseSurface)
    {
        for (var lz = 0; lz < 16; lz++)
        {
            var wz = chunkWorldZ + lz;
            for (var lx = 0; lx < 16; lx++)
            {
                var wx = chunkWorldX + lx;
                var targetY = baseSurface[(lz << 4) | lx];
                var maxConsiderY = Math.Max(SeaLevel, targetY + 14);

                for (var y = EngineConstants.WorldMinY; y <= maxConsiderY; y++)
                {
                    if (y == EngineConstants.WorldMinY || (y < EngineConstants.WorldMinY + 4 && (((wx * 31) + (wz * 17) + (y * 13)) & 3) == 0))
                    {
                        _ = chunk.SetBlockStateRaw(lx, y, lz, _bedrock);
                        continue;
                    }

                    if (IsTerrainSolidBeforeCaves(wx, y, wz, targetY))
                    {
                        if (IsCaveCarved(wx, y, wz, targetY))
                        {
                            continue;
                        }

                        var isDeep = y < 4 || (y < 10 && (((wx * 19) + (wz * 23) + (y * 7)) & 3) <= (9 - y) / 2);
                        _ = chunk.SetBlockStateRaw(lx, y, lz, isDeep ? _deepslate : _stone);
                    }
                    else if (y <= SeaLevel)
                    {
                        _ = chunk.SetBlockStateRaw(lx, y, lz, _water);
                    }
                }
            }
        }
    }

    private void ApplySurfaceStrata(ChunkColumn chunk, int chunkWorldX, int chunkWorldZ, ReadOnlySpan<ushort> columnBiomes)
    {
        for (var lz = 0; lz < 16; lz++)
        {
            for (var lx = 0; lx < 16; lx++)
            {
                var biomeKey = _biomeSampler.GetBiome(columnBiomes[(lz << 4) | lx]).Key;
                var wx = chunkWorldX + lx;
                var wz = chunkWorldZ + lz;
                var depthRemaining = 0;

                for (var y = 175; y >= EngineConstants.WorldMinY + 5; y--)
                {
                    var current = chunk.GetBlockState(lx, y, lz);
                    if (current.IsAir || current == _water)
                    {
                        depthRemaining = 0;
                        continue;
                    }

                    if (current != _stone)
                    {
                        continue;
                    }

                    if (depthRemaining == 0)
                    {
                        var above = chunk.GetBlockState(lx, y + 1, lz);
                        var underWater = above == _water;
                        if (underWater || biomeKey == BuiltInBiomes.Desert || (y <= SeaLevel + 1 && !_mineshaftGenerator.IsVerticalAccessShaftColumn(wx, wz)))
                        {
                            _ = chunk.SetBlockStateRaw(lx, y, lz, _sand);
                            depthRemaining = 3;
                        }
                        else
                        {
                            var topBlock = y >= 102 ? _snowBlock : _grass;
                            _ = chunk.SetBlockStateRaw(lx, y, lz, topBlock);
                            depthRemaining = 3;
                        }
                    }
                    else
                    {
                        var subBlock = biomeKey == BuiltInBiomes.Desert ? (depthRemaining == 1 ? _sandstone : _sand) : _dirt;
                        _ = chunk.SetBlockStateRaw(lx, y, lz, subBlock);
                        depthRemaining--;
                    }
                }
            }
        }
    }
}
