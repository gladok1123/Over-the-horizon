using OverHorizon.Core;
using OverHorizon.Core.Biomes;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Registry;
using OverHorizon.Shared.Random;
using OverHorizon.World.Lighting;
using OverHorizon.World.Storage;

namespace OverHorizon.World.Generation;

/// <summary>
/// Deterministic Nether chunk generator producing a roofed <c>0..128</c> netherrack labyrinth,
/// an <c>Y=31</c> lava sea, five Nether biomes, quartz/gold/ancient-debris veins, glowstone ceiling
/// clusters, Basalt Delta pillars, and Crimson/Warped wart growths.
/// </summary>
public sealed partial class NetherGenerator : IWorldGenerator
{
    /// <summary>World Y of the Nether lava sea surface.</summary>
    public const int LavaSeaY = DimensionRules.NetherLavaSeaY;

    /// <summary>Exclusive upper bound of Nether terrain (the bedrock roof sits at <see cref="RoofY"/>).</summary>
    public const int CeilingY = DimensionRules.NetherCeilingY;

    /// <summary>World Y of the bedrock roof.</summary>
    public const int RoofY = CeilingY - 1;

    /// <summary>World Y of the bedrock floor.</summary>
    public const int FloorY = DimensionRules.NetherFloorY;

    private readonly CoreRegistries _registries;
    private readonly NetherBiomeSampler _biomeSampler;
    private readonly NetherFortressGenerator _fortressGenerator;
    private readonly PerlinNoise _caveNoiseA;
    private readonly PerlinNoise _caveNoiseB;
    private readonly PerlinNoise _caveNoiseC;
    private readonly PerlinNoise _warpNoise;

    private readonly BlockState _bedrock;
    private readonly BlockState _netherrack;
    private readonly BlockState _soulSand;
    private readonly BlockState _soulSoil;
    private readonly BlockState _basalt;
    private readonly BlockState _gravel;
    private readonly BlockState _magma;
    private readonly BlockState _quartzOre;
    private readonly BlockState _goldOre;
    private readonly BlockState _ancientDebris;
    private readonly BlockState _glowstone;
    private readonly BlockState _lava;
    private readonly BlockState _netherWart;
    private readonly BlockState _warpedWart;
    private readonly BlockState _shroomlight;

    /// <summary>
    /// Initializes a new instance of the <see cref="NetherGenerator"/> class.
    /// </summary>
    /// <param name="worldSeed">64-bit world seed.</param>
    /// <param name="registries">Frozen domain registries.</param>
    public NetherGenerator(long worldSeed, CoreRegistries registries)
    {
        ArgumentNullException.ThrowIfNull(registries);

        WorldSeed = worldSeed;
        _registries = registries;
        _biomeSampler = new NetherBiomeSampler(worldSeed, registries.Biomes);

        var seed = unchecked((ulong)worldSeed);
        _caveNoiseA = new PerlinNoise(SplitMix64.Mix(seed ^ 0x1A1A_0001UL));
        _caveNoiseB = new PerlinNoise(SplitMix64.Mix(seed ^ 0x1A1A_0002UL));
        _caveNoiseC = new PerlinNoise(SplitMix64.Mix(seed ^ 0x1A1A_0003UL));
        _warpNoise = new PerlinNoise(SplitMix64.Mix(seed ^ 0x1A1A_0004UL));

        var blocks = registries.Blocks;
        _fortressGenerator = new NetherFortressGenerator(worldSeed, blocks);
        _bedrock = blocks.GetDefaultState(BuiltInBlocks.Bedrock);
        _netherrack = blocks.GetDefaultState(BuiltInBlocks.Netherrack);
        _soulSand = blocks.GetDefaultState(BuiltInBlocks.SoulSand);
        _soulSoil = blocks.GetDefaultState(BuiltInBlocks.SoulSoil);
        _basalt = blocks.GetDefaultState(BuiltInBlocks.Basalt);
        _gravel = blocks.GetDefaultState(BuiltInBlocks.Gravel);
        _magma = blocks.GetDefaultState(BuiltInBlocks.MagmaBlock);
        _quartzOre = blocks.GetDefaultState(BuiltInBlocks.NetherQuartzOre);
        _goldOre = blocks.GetDefaultState(BuiltInBlocks.NetherGoldOre);
        _ancientDebris = blocks.GetDefaultState(BuiltInBlocks.AncientDebris);
        _glowstone = blocks.GetDefaultState(BuiltInBlocks.Glowstone);
        _lava = blocks.GetDefaultState(BuiltInBlocks.Lava);
        _netherWart = blocks.GetDefaultState(BuiltInBlocks.NetherWartBlock);
        _warpedWart = blocks.GetDefaultState(BuiltInBlocks.WarpedWartBlock);
        _shroomlight = blocks.GetDefaultState(BuiltInBlocks.Shroomlight);
    }

    /// <inheritdoc />
    public long WorldSeed { get; }

    /// <summary>
    /// Samples the Nether biome raw ID at world block coordinates (X, Z).
    /// </summary>
    /// <param name="worldX">World X block coordinate.</param>
    /// <param name="worldZ">World Z block coordinate.</param>
    /// <returns>Dense biome registry ID.</returns>
    public ushort SampleBiomeId(int worldX, int worldZ) => _biomeSampler.SampleBiomeId(worldX, worldZ);

    /// <summary>
    /// Evaluates whether the Nether cell at (<paramref name="worldX"/>, <paramref name="worldY"/>, <paramref name="worldZ"/>)
    /// is carved open by cave noise (<see langword="false"/> means solid netherrack).
    /// </summary>
    /// <param name="worldX">World X block coordinate.</param>
    /// <param name="worldY">World Y block coordinate.</param>
    /// <param name="worldZ">World Z block coordinate.</param>
    /// <returns><see langword="true"/> when the cell is open air or lava.</returns>
    public bool IsCarved(int worldX, int worldY, int worldZ)
    {
        if (worldY <= FloorY + 2 || worldY >= RoofY - 2)
        {
            return false;
        }

        var a = _caveNoiseA.SampleFractal3D(worldX * 0.028, worldY * 0.045, worldZ * 0.028, octaves: 3);
        var b = _caveNoiseB.SampleFractal3D(worldX * 0.028, worldY * 0.045, worldZ * 0.028, octaves: 3);
        var spaghetti = (a * a) + (b * b);
        if (spaghetti < 0.020)
        {
            return true;
        }

        var cheese = _caveNoiseC.Sample3D((worldX * 0.021) + 11.5, (worldY * 0.031) - 7.25, (worldZ * 0.021) + 3.75);
        return cheese > 0.58;
    }

    /// <inheritdoc />
    public void GenerateChunk(ChunkColumn chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        var originX = chunk.Position.MinBlockX;
        var originZ = chunk.Position.MinBlockZ;

        Span<ushort> columnBiomes = stackalloc ushort[256];
        for (var lz = 0; lz < 16; lz++)
        {
            for (var lx = 0; lx < 16; lx++)
            {
                columnBiomes[(lz << 4) | lx] = _biomeSampler.SampleBiomeId(originX + lx, originZ + lz);
            }
        }

        WriteBiomes(chunk, columnBiomes);
        PopulateStoneAndLava(chunk, originX, originZ);
        ApplyBiomeSurfaces(chunk, originX, originZ, columnBiomes);
        PopulateVeinsAndFeatures(chunk, originX, originZ, columnBiomes);
        _fortressGenerator.PopulateChunk(chunk);

        chunk.RecomputeHeightmaps(_registries.Blocks);
        LightingEngine.InitializeChunkLight(chunk, _registries.Blocks);
        chunk.MarkClean();
    }

    private static void WriteBiomes(ChunkColumn chunk, ReadOnlySpan<ushort> columnBiomes)
    {
        for (var s = 0; s < ChunkColumn.SectionCount; s++)
        {
            var sec = chunk.GetSection(s);
            for (var qz = 0; qz < 4; qz++)
            {
                for (var qx = 0; qx < 4; qx++)
                {
                    var sample = columnBiomes[((qz * 4) << 4) | (qx * 4)];
                    for (var qy = 0; qy < 4; qy++)
                    {
                        sec.SetBiomeId(qx, qy, qz, sample);
                    }
                }
            }
        }
    }

    private void PopulateStoneAndLava(ChunkColumn chunk, int originX, int originZ)
    {
        for (var lz = 0; lz < 16; lz++)
        {
            var wz = originZ + lz;
            for (var lx = 0; lx < 16; lx++)
            {
                var wx = originX + lx;
                for (var y = FloorY; y < CeilingY; y++)
                {
                    var isShell = y <= FloorY + 1 || y >= RoofY - 1;
                    if (IsCarved(wx, y, wz))
                    {
                        if (!isShell && y <= LavaSeaY)
                        {
                            _ = chunk.SetBlockStateRaw(lx, y, lz, _lava);
                        }

                        continue;
                    }

                    _ = chunk.SetBlockStateRaw(lx, y, lz, isShell ? _bedrock : _netherrack);
                }
            }
        }
    }

    private void PopulateVeinsAndFeatures(ChunkColumn chunk, int originX, int originZ, ReadOnlySpan<ushort> columnBiomes)
    {
        var rng = new Xoroshiro128PlusPlus(
            unchecked((ulong)WorldSeed ^ 0x9E37_79B9_7F4A_7C15UL),
            unchecked(((ulong)(uint)originX << 32) | (ulong)(uint)originZ));

        for (var i = 0; i < 14; i++)
        {
            PlaceVein(chunk, originX, originZ, ref rng, 10, 118, _quartzOre);
        }

        for (var i = 0; i < 6; i++)
        {
            PlaceVein(chunk, originX, originZ, ref rng, 10, 117, _goldOre);
        }

        if (rng.NextInt32(100) < 22)
        {
            var deep = rng.NextInt32(100) < 60;
            PlaceVein(chunk, originX, originZ, ref rng, deep ? 8 : 20, deep ? 24 : 119, _ancientDebris);
        }

        for (var i = 0; i < 5; i++)
        {
            PlaceVein(chunk, originX, originZ, ref rng, 23, LavaSeaY + 6, _magma);
        }

        for (var i = 0; i < 4; i++)
        {
            PlaceVein(chunk, originX, originZ, ref rng, 6, LavaSeaY + 4, _gravel);
        }

        PlaceGlowstoneClusters(chunk, originX, originZ, ref rng);
        PlaceFungalGrowths(chunk, originX, originZ, columnBiomes, ref rng);
        PlaceBasaltPillars(chunk, originX, originZ, columnBiomes);
    }

    private void PlaceVein(
        ChunkColumn chunk,
        int originX,
        int originZ,
        ref Xoroshiro128PlusPlus rng,
        int minY,
        int maxY,
        BlockState state)
    {
        var wx = originX + rng.NextInt32(16);
        var wz = originZ + rng.NextInt32(16);
        var wy = minY + rng.NextInt32(Math.Max(1, maxY - minY));
        SetIfNetherrack(chunk, originX, originZ, wx, wy, wz, state);

        // Walk a short random worm so clusters read as real ore pockets instead of isolated dots.
        var count = 2 + rng.NextInt32(5);
        for (var i = 0; i < count; i++)
        {
            wx += rng.NextInt32(3) - 1;
            wy += rng.NextInt32(3) - 1;
            wz += rng.NextInt32(3) - 1;
            if (wy < minY || wy > maxY)
            {
                continue;
            }

            SetIfNetherrack(chunk, originX, originZ, wx, wy, wz, state);
        }
    }

    private void PlaceGlowstoneClusters(ChunkColumn chunk, int originX, int originZ, ref Xoroshiro128PlusPlus rng)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var wx = originX + rng.NextInt32(16);
            var wz = originZ + rng.NextInt32(16);
            var wy = 60 + rng.NextInt32(RoofY - 66);
            if (!TryFindCeiling(chunk, originX, originZ, wx, wy, wz, out var ceilingY))
            {
                continue;
            }

            var radius = 1 + rng.NextInt32(2);
            for (var dy = 0; dy <= radius; dy++)
            {
                for (var dz = -radius; dz <= radius; dz++)
                {
                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        if (((dx * dx) + (dy * dy) + (dz * dz)) > (radius * radius) + 1)
                        {
                            continue;
                        }

                        if (dy > 0 && ((dx * dx) + (dz * dz)) > 1)
                        {
                            continue;
                        }

                        SetIfNetherrackOrAir(chunk, originX, originZ, wx + dx, ceilingY - dy, wz + dz, _glowstone);
                    }
                }
            }
        }
    }

    private void PlaceFungalGrowths(
        ChunkColumn chunk,
        int originX,
        int originZ,
        ReadOnlySpan<ushort> columnBiomes,
        ref Xoroshiro128PlusPlus rng)
    {
        for (var lz = 0; lz < 16; lz++)
        {
            for (var lx = 0; lx < 16; lx++)
            {
                var biomeKey = _biomeSampler.GetBiome(columnBiomes[(lz << 4) | lx]).Key;
                var isCrimson = biomeKey == BuiltInBiomes.CrimsonForest;
                var isWarped = biomeKey == BuiltInBiomes.WarpedForest;
                if ((!isCrimson && !isWarped) || rng.NextInt32(100) >= 7)
                {
                    continue;
                }

                var wx = originX + lx;
                var wz = originZ + lz;
                if (!TryFindFloor(chunk, originX, originZ, wx, wz, out var floorY))
                {
                    continue;
                }

                var wart = isCrimson ? _netherWart : _warpedWart;
                var height = 1 + rng.NextInt32(3);
                for (var i = 0; i < height; i++)
                {
                    SetIfNetherrackOrAir(chunk, originX, originZ, wx, floorY + i, wz, wart);
                }

                if (isWarped && rng.NextInt32(100) < 45)
                {
                    SetIfNetherrackOrAir(chunk, originX, originZ, wx, floorY + height, wz, _shroomlight);
                }
            }
        }
    }

    private void PlaceBasaltPillars(
        ChunkColumn chunk,
        int originX,
        int originZ,
        ReadOnlySpan<ushort> columnBiomes)
    {
        for (var lz = 0; lz < 16; lz++)
        {
            for (var lx = 0; lx < 16; lx++)
            {
                var biomeKey = _biomeSampler.GetBiome(columnBiomes[(lz << 4) | lx]).Key;
                if (biomeKey != BuiltInBiomes.BasaltDeltas)
                {
                    continue;
                }

                var wx = originX + lx;
                var wz = originZ + lz;
                var warp = _warpNoise.Sample3D(wx * 0.02, 0.0, wz * 0.02);
                if (warp < 0.34 || !TryFindFloor(chunk, originX, originZ, wx, wz, out var floorY))
                {
                    continue;
                }

                var height = 2 + (int)((Hash(wx, floorY, wz) >> 7) % 7);
                for (var i = 0; i < height; i++)
                {
                    SetIfNetherrackOrAir(chunk, originX, originZ, wx, floorY + i, wz, _basalt);
                }
            }
        }
    }

    private static bool TryFindCeiling(
        ChunkColumn chunk,
        int originX,
        int originZ,
        int worldX,
        int startY,
        int worldZ,
        out int ceilingY)
    {
        var lx = worldX - originX;
        var lz = worldZ - originZ;
        for (var y = Math.Min(RoofY - 3, startY); y > FloorY + 3; y--)
        {
            if (chunk.GetBlockState(lx, y, lz).IsAir || chunk.GetBlockState(lx, y + 1, lz).IsAir)
            {
                continue;
            }

            if (chunk.GetBlockState(lx, y - 1, lz).IsAir)
            {
                ceilingY = y;
                return true;
            }
        }

        ceilingY = -1;
        return false;
    }

    private static bool TryFindFloor(ChunkColumn chunk, int originX, int originZ, int worldX, int worldZ, out int floorY)
    {
        var lx = worldX - originX;
        var lz = worldZ - originZ;
        for (var y = RoofY - 3; y > FloorY + 2; y--)
        {
            if (chunk.GetBlockState(lx, y, lz).IsAir && !chunk.GetBlockState(lx, y - 1, lz).IsAir)
            {
                floorY = y;
                return true;
            }
        }

        floorY = -1;
        return false;
    }

    private void SetIfNetherrack(
        ChunkColumn chunk,
        int originX,
        int originZ,
        int worldX,
        int worldY,
        int worldZ,
        BlockState state)
    {
        var lx = worldX - originX;
        var lz = worldZ - originZ;
        if ((uint)lx >= 16U || (uint)lz >= 16U || worldY < FloorY + 1 || worldY >= RoofY)
        {
            return;
        }

        if (chunk.GetBlockState(lx, worldY, lz) != _netherrack)
        {
            return;
        }

        _ = chunk.SetBlockStateRaw(lx, worldY, lz, state);
    }

    private void SetIfNetherrackOrAir(
        ChunkColumn chunk,
        int originX,
        int originZ,
        int worldX,
        int worldY,
        int worldZ,
        BlockState state)
    {
        var lx = worldX - originX;
        var lz = worldZ - originZ;
        if ((uint)lx >= 16U || (uint)lz >= 16U || worldY < FloorY + 1 || worldY >= RoofY - 1)
        {
            return;
        }

        var current = chunk.GetBlockState(lx, worldY, lz);
        if (current != _netherrack && !current.IsAir)
        {
            return;
        }

        _ = chunk.SetBlockStateRaw(lx, worldY, lz, state);
    }

    private static uint Hash(int x, int y, int z) =>
        unchecked((uint)(((x * 73856093) ^ (y * 19349663) ^ (z * 83492791)) & 0x7FFF_FFFF));
}
