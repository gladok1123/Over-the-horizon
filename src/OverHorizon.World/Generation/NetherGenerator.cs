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

    /// <summary>
    /// World Y of the top of the great Nether cavern: the tall open void hanging over the lava sea that hosts
    /// Nether Fortresses (<c>78</c>). Everything below it is shore rock, everything above is roof labyrinth.
    /// </summary>
    public const int GreatCavernTopY = 78;

    /// <summary>
    /// World Y of the top of the walkable shore rock capping the lava sea (<c>39</c>), so the Nether keeps a
    /// solid floor to travel on instead of dropping the player straight into lava.
    /// </summary>
    public const int ShoreRockTopY = LavaSeaY + 8;

    /// <summary>World Y where the rocky ceiling starts closing over the cavern (<c>102</c>).</summary>
    public const int CeilingRockY = RoofY - 25;

    private readonly CoreRegistries _registries;
    private readonly NetherBiomeSampler _biomeSampler;
    private readonly NetherFortressGenerator _fortressGenerator;
    private readonly NetherRuinedPortalGenerator _ruinedPortalGenerator;
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
        _ruinedPortalGenerator = new NetherRuinedPortalGenerator(worldSeed, blocks);
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

        // Noodle tunnels thread every band of the dimension, including the rocky ceiling.
        var a = _caveNoiseA.SampleFractal3D(worldX * 0.021, worldY * 0.033, worldZ * 0.021, octaves: 3);
        var b = _caveNoiseB.SampleFractal3D(worldX * 0.021, worldY * 0.033, worldZ * 0.021, octaves: 3);
        if (((a * a) + (b * b)) < 0.016)
        {
            return true;
        }

        // A broad air field with a vertical profile: a deep lava ocean, a walkable shore, one very tall
        // great cavern, a roof labyrinth, and a solid ceiling. This is what gives the Nether its space.
        var air = _caveNoiseC.Sample3D((worldX * 0.017) + 11.5, (worldY * 0.026) - 7.25, (worldZ * 0.017) + 3.75);
        return (air + OpennessBias(worldY)) > 0.0;
    }

    /// <summary>
    /// Returns the vertical openness bias applied to the Nether air field at <paramref name="worldY"/>:
    /// positive values carve the cell open, negative values leave it as netherrack.
    /// </summary>
    /// <param name="worldY">World Y block coordinate.</param>
    /// <returns>Openness bias in roughly <c>-0.35..0.45</c>.</returns>
    public static double OpennessBias(int worldY)
    {
        if (worldY <= LavaSeaY - 6)
        {
            return 0.36;
        }

        if (worldY <= LavaSeaY)
        {
            return 0.16;
        }

        if (worldY <= ShoreRockTopY)
        {
            return -0.30;
        }

        if (worldY <= GreatCavernTopY)
        {
            return 0.30;
        }

        return worldY <= CeilingRockY ? 0.02 : -0.30;
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
        _ruinedPortalGenerator.PopulateChunk(chunk);

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

    private static uint Hash(int x, int y, int z) =>
        unchecked((uint)(((x * 73856093) ^ (y * 19349663) ^ (z * 83492791)) & 0x7FFF_FFFF));
}
