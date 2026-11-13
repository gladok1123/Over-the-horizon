using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.World.Lighting;
using OverHorizon.World.Storage;

namespace OverHorizon.World.Generation;

/// <summary>
/// Deterministic multi-octave Overworld chunk generator featuring continentalness, mountain ridges,
/// deepslate strata, surface-connected 3D caves, stratified mineral ore veins, massive multi-chunk
/// structural abandoned mineshafts, and cross-chunk Oak &amp; Birch forests.
/// </summary>
public sealed class OverworldGenerator : IWorldGenerator
{
    /// <summary>Default sea level Y coordinate.</summary>
    public const int SeaLevel = 62;

    private readonly IBlockRegistry _blocks;
    private readonly SimplexNoise2D _continentalNoise;
    private readonly SimplexNoise2D _erosionNoise;
    private readonly SimplexNoise2D _ridgeNoise;
    private readonly PerlinNoise _densityNoise3D;
    private readonly PerlinNoise _caveNoiseA;
    private readonly PerlinNoise _caveNoiseB;
    private readonly BiomeSampler _biomeSampler;
    private readonly OreVeinPopulator _orePopulator;
    private readonly MineshaftStructureGenerator _mineshaftGenerator;
    private readonly TreeFeaturePopulator _treePopulator;

    private readonly BlockStateId _air;
    private readonly BlockStateId _bedrock;
    private readonly BlockStateId _stone;
    private readonly BlockStateId _deepslate;
    private readonly BlockStateId _dirt;
    private readonly BlockStateId _grass;
    private readonly BlockStateId _grassSnowy;
    private readonly BlockStateId _sand;
    private readonly BlockStateId _sandstone;
    private readonly BlockStateId _water;

    /// <summary>
    /// Initializes a new instance of the <see cref="OverworldGenerator"/> class.
    /// </summary>
    public OverworldGenerator(long seed, IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        Seed = seed;
        _blocks = blocks;

        _continentalNoise = new SimplexNoise2D(seed ^ 0x1A2B3C4D5E6F0001L);
        _erosionNoise = new SimplexNoise2D(seed ^ 0x2B3C4D5E6F700002L);
        _ridgeNoise = new SimplexNoise2D(seed ^ 0x3C4D5E6F70810003L);
        _densityNoise3D = new PerlinNoise(seed ^ 0x4D5E6F7081920004L);
        _caveNoiseA = new PerlinNoise(seed ^ 0x5E6F708192A30005L);
        _caveNoiseB = new PerlinNoise(seed ^ 0x6F708192A3B40006L);
        _biomeSampler = new BiomeSampler(seed);
        _orePopulator = new OreVeinPopulator(seed, blocks);
        _mineshaftGenerator = new MineshaftStructureGenerator(seed, blocks);
        _treePopulator = new TreeFeaturePopulator(seed, blocks);

        _air = BlockStateId.Air;
        _bedrock = blocks.GetByKey(BuiltInBlocks.Bedrock).DefaultState;
        _stone = blocks.GetByKey(BuiltInBlocks.Stone).DefaultState;
        _deepslate = blocks.GetByKey(BuiltInBlocks.Deepslate).DefaultState;
        _dirt = blocks.GetByKey(BuiltInBlocks.Dirt).DefaultState;
        var grassDef = blocks.GetByKey(BuiltInBlocks.GrassBlock);
        _grass = grassDef.GetState(("snowy", "false"));
        _grassSnowy = grassDef.GetState(("snowy", "true"));
        _sand = blocks.GetByKey(BuiltInBlocks.Sand).DefaultState;
        _sandstone = blocks.GetByKey(BuiltInBlocks.Sandstone).DefaultState;
        _water = blocks.GetByKey(BuiltInBlocks.Water).GetState(("level", "0"));
    }

    /// <inheritdoc />
    public long Seed { get; }

    /// <inheritdoc />
    public ChunkColumn GenerateChunk(ChunkPos position)
    {
        var chunk = new ChunkColumn(position);
        var chunkWorldX = position.MinBlockX;
        var chunkWorldZ = position.MinBlockZ;

        Span<int> baseSurface = stackalloc int[256];
        Span<BiomeKind> biomes = stackalloc BiomeKind[256];

        for (var lz = 0; lz < 16; lz++)
        {
            for (var lx = 0; lx < 16; lx++)
            {
                var wx = chunkWorldX + lx;
                var wz = chunkWorldZ + lz;
                var idx = (lz << 4) | lx;
                baseSurface[idx] = ComputeColumnBaseHeight(wx, wz, out var biome);
                biomes[idx] = biome;
            }
        }

        PopulateBaseTerrainAndCaves(chunk, chunkWorldX, chunkWorldZ, baseSurface);
        ApplySurfaceStrata(chunk, chunkWorldX, chunkWorldZ, biomes);
        _orePopulator.PopulateChunk(chunk);
        _mineshaftGenerator.PopulateChunk(chunk, SampleRawSurfaceHeight);
        _treePopulator.PopulateChunk(chunk, SampleTreeGround);

        chunk.RecomputeHeightmaps(_blocks);
        LightingEngine.InitializeChunkLight(chunk, _blocks);
        chunk.ClearDirty();
        return chunk;
    }

    private int SampleRawSurfaceHeight(int worldX, int worldZ) =>
        ComputeColumnBaseHeight(worldX, worldZ, out _);

    private (bool Valid, int GroundY, BiomeKind Biome) SampleTreeGround(int worldX, int worldZ)
    {
        var baseH = ComputeColumnBaseHeight(worldX, worldZ, out var biome);
        if (biome == BiomeKind.Ocean || biome == BiomeKind.Desert)
        {
            return (false, baseH, biome);
        }

        if (_mineshaftGenerator.IsVerticalAccessShaftColumn(worldX, worldZ))
        {
            return (false, baseH, biome);
        }

        // Find exact deterministic solid terrain surface at (worldX, worldZ), accounting for 3D density overhangs & surface caves
        var topY = Math.Min(EngineConstants.WorldMaxY - 12, baseH + 16);
        var minSearchY = Math.Max(SeaLevel + 2, baseH - 12);
        for (var y = topY; y >= minSearchY; y--)
        {
            if (!IsTerrainSolidBeforeCaves(worldX, y, worldZ, baseH))
            {
                continue;
            }

            if (IsCaveCarved(worldX, y, worldZ, baseH))
            {
                return (false, y, biome);
            }

            if (biome == BiomeKind.Mountains && y >= 88)
            {
                return (false, y, biome);
            }

            return (true, y, biome);
        }

        return (false, baseH, biome);
    }

    private int ComputeColumnBaseHeight(int wx, int wz, out BiomeKind biome)
    {
        var continental = _continentalNoise.Fbm(wx * 0.0022, wz * 0.0022, octaves: 4);
        var erosion = _erosionNoise.Fbm(wx * 0.0045, wz * 0.0045, octaves: 3);
        var ridgeRaw = _ridgeNoise.Fbm(wx * 0.006, wz * 0.006, octaves: 4);
        var peaks = 1.0 - Math.Abs(ridgeRaw);
        peaks *= peaks;

        var elevation = 64.0 + (continental * 26.0) - (erosion * 10.0) + (peaks * Math.Max(0.0, continental + 0.15) * 42.0);
        var height = Math.Clamp((int)Math.Round(elevation), -40, 180);
        biome = _biomeSampler.Sample(wx, wz, continental);
        return height;
    }

    private bool IsTerrainSolidBeforeCaves(int wx, int y, int wz, int targetSurfaceY)
    {
        if (y <= EngineConstants.WorldMinY + 4)
        {
            return true;
        }

        if (y > targetSurfaceY + 14)
        {
            return false;
        }

        if (y < targetSurfaceY - 10)
        {
            return true;
        }

        var densityOffset = _densityNoise3D.Fbm(wx * 0.035, y * 0.045, wz * 0.035, octaves: 3) * 8.5;
        return (targetSurfaceY - y + densityOffset) > 0.0;
    }

    private bool IsCaveCarved(int wx, int y, int wz, int surfaceY)
    {
        if (y <= EngineConstants.WorldMinY + 5 || y > surfaceY + 4)
        {
            return false;
        }

        // Keep oceans watertight while allowing dramatic dry hillside & forest cave mouths!
        if (surfaceY <= SeaLevel + 2 && y >= surfaceY - 5)
        {
            return false;
        }

        var ca = _caveNoiseA.Sample(wx * 0.038, y * 0.048, wz * 0.038);
        var cb = _caveNoiseB.Sample(wx * 0.038, y * 0.048, wz * 0.038);
        var spaghetti = (ca * ca) + (cb * cb);
        var threshold = y > 52 ? 0.0095 : 0.0135;
        return spaghetti < threshold;
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
                    if (y == EngineConstants.WorldMinY || (y < EngineConstants.WorldMinY + 4 && ((wx * 31 + wz * 17 + y * 13) & 3) == 0))
                    {
                        chunk.SetBlockState(lx, y, lz, _bedrock);
                        continue;
                    }

                    if (IsTerrainSolidBeforeCaves(wx, y, wz, targetY))
                    {
                        if (IsCaveCarved(wx, y, wz, targetY))
                        {
                            continue;
                        }

                        var isDeep = y < 4 || (y < 10 && ((wx * 19 + wz * 23 + y * 7) & 3) <= (9 - y) / 2);
                        chunk.SetBlockState(lx, y, lz, isDeep ? _deepslate : _stone);
                    }
                    else if (y <= SeaLevel)
                    {
                        chunk.SetBlockState(lx, y, lz, _water);
                    }
                }
            }
        }
    }

    private void ApplySurfaceStrata(ChunkColumn chunk, int chunkWorldX, int chunkWorldZ, ReadOnlySpan<BiomeKind> biomes)
    {
        for (var lz = 0; lz < 16; lz++)
        {
            for (var lx = 0; lx < 16; lx++)
            {
                var biome = biomes[(lz << 4) | lx];
                var wx = chunkWorldX + lx;
                var wz = chunkWorldZ + lz;
                var depthRemaining = 0;

                for (var y = EngineConstants.WorldMaxY - 1; y >= EngineConstants.WorldMinY + 5; y--)
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
                        var above = y + 1 < EngineConstants.WorldMaxY ? chunk.GetBlockState(lx, y + 1, lz) : _air;
                        var underWater = above == _water;
                        if (underWater || biome == BiomeKind.Desert || (y <= SeaLevel + 1 && !_mineshaftGenerator.IsVerticalAccessShaftColumn(wx, wz)))
                        {
                            chunk.SetBlockState(lx, y, lz, _sand);
                            depthRemaining = 3;
                        }
                        else
                        {
                            var topBlock = biome == BiomeKind.Mountains && y >= 88 ? _grassSnowy : _grass;
                            chunk.SetBlockState(lx, y, lz, topBlock);
                            depthRemaining = 3;
                        }
                    }
                    else
                    {
                        var subBlock = biome == BiomeKind.Desert ? (depthRemaining == 1 ? _sandstone : _sand) : _dirt;
                        chunk.SetBlockState(lx, y, lz, subBlock);
                        depthRemaining--;
                    }
                }
            }
        }
    }
}
