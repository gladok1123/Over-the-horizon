using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.World.Storage;

namespace OverHorizon.World.Generation;

/// <summary>
/// Deterministic Ruined Portal generator for the Nether: broken Obsidian portal frames half-buried in Netherrack,
/// ringed by Magma Block slag and a Lava puddle, with a Gold Block buried under one upright (vanilla parity,
/// built exclusively from blocks that already ship with the engine so no synthetic textures are needed).
/// </summary>
public sealed partial class NetherRuinedPortalGenerator
{
    /// <summary>Region edge length in blocks (<c>384</c> = 24 chunks) between Ruined Portal attempts.</summary>
    public const int RegionSizeBlocks = 384;

    /// <summary>Half-extent of the largest ruin footprint, used to cull regions that cannot touch a chunk.</summary>
    public const int RuinRadius = 16;

    private const int MinBuildY = DimensionRules.NetherFloorY + 2;
    private const int MaxBuildY = DimensionRules.NetherCeilingY - 3;

    private readonly long _seed;
    private readonly BlockState _air;
    private readonly BlockState _obsidian;
    private readonly BlockState _netherrack;
    private readonly BlockState _magma;
    private readonly BlockState _lava;
    private readonly BlockState _goldBlock;

    /// <summary>
    /// Initializes a new instance of the <see cref="NetherRuinedPortalGenerator"/> class.
    /// </summary>
    /// <param name="worldSeed">64-bit world seed.</param>
    /// <param name="blocks">Frozen block registry.</param>
    public NetherRuinedPortalGenerator(long worldSeed, IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        _seed = worldSeed;
        _air = BlockState.Air;
        _obsidian = blocks.GetDefaultState(BuiltInBlocks.Obsidian);
        _netherrack = blocks.GetDefaultState(BuiltInBlocks.Netherrack);
        _magma = blocks.GetDefaultState(BuiltInBlocks.MagmaBlock);
        _lava = blocks.GetDefaultState(BuiltInBlocks.Lava);
        _goldBlock = blocks.GetDefaultState(BuiltInBlocks.GoldBlock);
    }

    /// <summary>
    /// Resolves the Ruined Portal anchored to region (<paramref name="rx"/>, <paramref name="rz"/>) when one exists.
    /// </summary>
    /// <param name="rx">Region X index.</param>
    /// <param name="rz">Region Z index.</param>
    /// <param name="originX">Receives the world X of the frame centre.</param>
    /// <param name="originZ">Receives the world Z of the frame centre.</param>
    /// <param name="baseY">Receives the world Y of the frame footing.</param>
    /// <returns><see langword="true"/> when this region holds a Ruined Portal.</returns>
    public bool TryGetRuinOrigin(int rx, int rz, out int originX, out int originZ, out int baseY)
    {
        var active = Hash(_seed, rx, rz, 0x5121_0000) % 3UL == 0UL;
        var hx = Hash(_seed, rx, rz, 0x5121_0001);
        var hz = Hash(_seed, rx, rz, 0x5121_0002);
        var hy = Hash(_seed, rx, rz, 0x5121_0003);

        var span = RegionSizeBlocks - (RuinRadius * 2);
        originX = (rx * RegionSizeBlocks) + (int)(hx % (ulong)span) + RuinRadius;
        originZ = (rz * RegionSizeBlocks) + (int)(hz % (ulong)span) + RuinRadius;
        baseY = DimensionRules.NetherLavaSeaY + 2 + (int)(hy % 26UL);
        return active;
    }

    /// <summary>
    /// Stamps every Ruined Portal overlapping <paramref name="chunk"/> into the already-generated Nether terrain.
    /// </summary>
    /// <param name="chunk">Target chunk column (terrain must already be generated).</param>
    public void PopulateChunk(ChunkColumn chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        var cMinX = chunk.Position.MinBlockX;
        var cMinZ = chunk.Position.MinBlockZ;
        var cMaxX = cMinX + 15;
        var cMaxZ = cMinZ + 15;

        var minRx = FloorDiv(cMinX - RuinRadius, RegionSizeBlocks);
        var maxRx = FloorDiv(cMaxX + RuinRadius, RegionSizeBlocks);
        var minRz = FloorDiv(cMinZ - RuinRadius, RegionSizeBlocks);
        var maxRz = FloorDiv(cMaxZ + RuinRadius, RegionSizeBlocks);

        for (var rz = minRz; rz <= maxRz; rz++)
        {
            for (var rx = minRx; rx <= maxRx; rx++)
            {
                if (!TryGetRuinOrigin(rx, rz, out var originX, out var originZ, out var baseY))
                {
                    continue;
                }

                if (cMaxX < originX - RuinRadius || cMinX > originX + RuinRadius ||
                    cMaxZ < originZ - RuinRadius || cMinZ > originZ + RuinRadius)
                {
                    continue;
                }

                BuildRuin(chunk, cMinX, cMinZ, originX, originZ, baseY);
            }
        }
    }

    private void BuildRuin(ChunkColumn chunk, int cMinX, int cMinZ, int originX, int originZ, int baseY)
    {
        var shape = Hash(_seed, originX, originZ, 0x5121_0004);
        var width = (int)(shape % 2UL) == 0 ? 5 : 7;
        var height = 4 + (int)((shape >> 8) % 3UL);
        var half = width / 2;

        for (var dz = -half; dz <= half; dz++)
        {
            for (var dx = -half; dx <= half; dx++)
            {
                var onOutline = Math.Abs(dx) == half || Math.Abs(dz) == half;
                if (!onOutline)
                {
                    continue;
                }

                for (var h = 0; h < height; h++)
                {
                    var decay = Hash(_seed, originX + dx, originZ + dz, 0x5121_0005 + h);
                    if (decay % 100UL < 24UL)
                    {
                        continue;
                    }

                    var block = decay % 100UL < 30UL ? _magma : _obsidian;
                    Set(chunk, cMinX, cMinZ, originX + dx, baseY + h, originZ + dz, block);
                }
            }
        }

        for (var dz = -half; dz <= half; dz++)
        {
            for (var dx = -half; dx <= half; dx++)
            {
                Clear(chunk, cMinX, cMinZ, originX + dx, baseY + height, originZ + dz);
                for (var d = 1; d <= 2; d++)
                {
                    Set(chunk, cMinX, cMinZ, originX + dx, baseY - d, originZ + dz, _netherrack);
                }
            }
        }

        Set(chunk, cMinX, cMinZ, originX - half, baseY - 1, originZ - half, _goldBlock);
        BuildLavaPuddle(chunk, cMinX, cMinZ, originX, originZ, baseY, half);
    }

    private void BuildLavaPuddle(ChunkColumn chunk, int cMinX, int cMinZ, int originX, int originZ, int baseY, int half)
    {
        var side = Hash(_seed, originX, originZ, 0x5121_0006);
        var dx0 = (int)(side % 3UL) - 1;
        var dz0 = (int)((side >> 4) % 3UL) - 1;
        if (dx0 == 0 && dz0 == 0)
        {
            dz0 = 1;
        }

        var poolX = originX + (dx0 * (half + 3));
        var poolZ = originZ + (dz0 * (half + 3));

        for (var dz = -1; dz <= 1; dz++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (Hash(_seed, poolX + dx, poolZ + dz, 0x5121_0007) % 100UL < 22UL)
                {
                    continue;
                }

                Set(chunk, cMinX, cMinZ, poolX + dx, baseY - 1, poolZ + dz, _lava);
            }
        }
    }

    private void Clear(ChunkColumn chunk, int cMinX, int cMinZ, int wx, int wy, int wz) =>
        Set(chunk, cMinX, cMinZ, wx, wy, wz, _air);

    private static void Set(ChunkColumn chunk, int cMinX, int cMinZ, int wx, int wy, int wz, BlockState state)
    {
        var lx = wx - cMinX;
        var lz = wz - cMinZ;
        if ((uint)lx >= 16U || (uint)lz >= 16U || wy < MinBuildY || wy > MaxBuildY)
        {
            return;
        }

        _ = chunk.SetBlockStateRaw(lx, wy, lz, state);
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
