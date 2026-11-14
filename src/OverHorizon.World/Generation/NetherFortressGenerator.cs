using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.World.Storage;

namespace OverHorizon.World.Generation;

/// <summary>
/// Deterministic Nether Fortress structure generator: a two-storey Nether Brick keep, four enclosed corridor wings,
/// a Blaze Monster Spawner hall, a Soul Sand Nether Wart farm, a chest vault, and parapet bridges that span the
/// lava sea. Fortresses are laid out on the vanilla <c>27x27</c> chunk region grid (one per region).
/// </summary>
public sealed partial class NetherFortressGenerator
{
    /// <summary>Region edge length in blocks (<c>432</c> = 27 chunks, matching vanilla Nether Fortress spacing).</summary>
    public const int RegionSizeBlocks = 432;

    /// <summary>Half-extent of the largest fortress footprint, used to cull regions that cannot touch a chunk.</summary>
    public const int FortressRadius = 64;

    private const int MinBuildY = DimensionRules.NetherFloorY + 2;
    private const int MaxBuildY = DimensionRules.NetherCeilingY - 3;

    /// <summary>Vertical gap between the keep ground floor and its upper storey.</summary>
    private const int StoreyHeight = 6;

    private readonly long _seed;
    private readonly BlockState _air;
    private readonly BlockState _bricks;
    private readonly BlockState _soulSand;
    private readonly BlockState _glowstone;
    private readonly BlockState _spawner;
    private readonly BlockState _chest;
    private readonly BlockState _lava;

    /// <summary>
    /// Initializes a new instance of the <see cref="NetherFortressGenerator"/> class.
    /// </summary>
    /// <param name="worldSeed">64-bit world seed.</param>
    /// <param name="blocks">Frozen block registry.</param>
    public NetherFortressGenerator(long worldSeed, IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        _seed = worldSeed;
        _air = BlockState.Air;
        _bricks = blocks.GetDefaultState(BuiltInBlocks.NetherBricks);
        _soulSand = blocks.GetDefaultState(BuiltInBlocks.SoulSand);
        _glowstone = blocks.GetDefaultState(BuiltInBlocks.Glowstone);
        _spawner = blocks.GetDefaultState(BuiltInBlocks.MonsterSpawner);
        _chest = blocks.GetDefaultState(BuiltInBlocks.Chest);
        _lava = blocks.GetDefaultState(BuiltInBlocks.Lava);
    }

    /// <summary>
    /// Resolves the hub centre and ground-floor Y of the fortress anchored to region (<paramref name="rx"/>, <paramref name="rz"/>).
    /// </summary>
    /// <param name="rx">Region X index.</param>
    /// <param name="rz">Region Z index.</param>
    /// <param name="hubX">Receives the world X of the keep centre.</param>
    /// <param name="hubZ">Receives the world Z of the keep centre.</param>
    /// <param name="baseY">Receives the world Y of the ground floor block layer.</param>
    public void GetFortressOrigin(int rx, int rz, out int hubX, out int hubZ, out int baseY)
    {
        var span = RegionSizeBlocks - (FortressRadius * 2);
        var hx = Hash(_seed, rx, rz, 0x4E46_0001);
        var hz = Hash(_seed, rx, rz, 0x4E46_0002);
        var hy = Hash(_seed, rx, rz, 0x4E46_0003);

        hubX = (rx * RegionSizeBlocks) + (int)(hx % (ulong)span) + FortressRadius;
        hubZ = (rz * RegionSizeBlocks) + (int)(hz % (ulong)span) + FortressRadius;
        baseY = DimensionRules.NetherLavaSeaY + 24 + ((int)(hy % 5UL) * 4);
    }

    /// <summary>
    /// Carves every fortress overlapping <paramref name="chunk"/> into the already-generated Nether terrain.
    /// </summary>
    /// <param name="chunk">Target chunk column (terrain must already be generated).</param>
    public void PopulateChunk(ChunkColumn chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        var cMinX = chunk.Position.MinBlockX;
        var cMinZ = chunk.Position.MinBlockZ;
        var cMaxX = cMinX + 15;
        var cMaxZ = cMinZ + 15;

        var minRx = FloorDiv(cMinX - FortressRadius, RegionSizeBlocks);
        var maxRx = FloorDiv(cMaxX + FortressRadius, RegionSizeBlocks);
        var minRz = FloorDiv(cMinZ - FortressRadius, RegionSizeBlocks);
        var maxRz = FloorDiv(cMaxZ + FortressRadius, RegionSizeBlocks);

        for (var rz = minRz; rz <= maxRz; rz++)
        {
            for (var rx = minRx; rx <= maxRx; rx++)
            {
                GetFortressOrigin(rx, rz, out var hubX, out var hubZ, out var baseY);
                if (cMaxX < hubX - FortressRadius || cMinX > hubX + FortressRadius ||
                    cMaxZ < hubZ - FortressRadius || cMinZ > hubZ + FortressRadius)
                {
                    continue;
                }

                PopulateFortress(chunk, cMinX, cMinZ, hubX, hubZ, baseY);
            }
        }
    }

    private void PopulateFortress(ChunkColumn chunk, int cMinX, int cMinZ, int hubX, int hubZ, int baseY)
    {
        // 1. Lava-spanning parapet bridges first - the enclosed wings and keep are stamped over them afterwards.
        BuildBridgeAlongX(chunk, cMinX, cMinZ, hubX - 56, hubX + 56, hubZ, hubX, baseY);
        BuildBridgeAlongZ(chunk, cMinX, cMinZ, hubZ - 56, hubZ + 56, hubX, hubZ, baseY);

        // 2. Four enclosed Nether Brick corridor wings radiating from the keep.
        BuildCorridorAlongX(chunk, cMinX, cMinZ, hubX - 26, hubX - 5, hubZ, baseY);
        BuildCorridorAlongX(chunk, cMinX, cMinZ, hubX + 5, hubX + 26, hubZ, baseY);
        BuildCorridorAlongZ(chunk, cMinX, cMinZ, hubZ - 26, hubZ - 5, hubX, baseY);
        BuildCorridorAlongZ(chunk, cMinX, cMinZ, hubZ + 5, hubZ + 26, hubX, baseY);

        // 3. Central two-storey keep (overwrites the bridge/corridor crossings so the junctions stay sealed).
        BuildKeep(chunk, cMinX, cMinZ, hubX, hubZ, baseY);

        // 4. Wing terminus rooms: Nether Wart farm, Blaze spawner hall, and chest vault.
        BuildWartFarm(chunk, cMinX, cMinZ, hubX - 34, hubX - 27, hubZ - 6, hubZ + 6, baseY);
        BuildBlazeSpawnerHall(chunk, cMinX, cMinZ, hubX + 27, hubX + 35, hubZ - 4, hubZ + 4, baseY);
        BuildTreasureVault(chunk, cMinX, cMinZ, hubX - 4, hubX + 4, hubZ - 34, hubZ - 27, baseY);
    }

    private void BuildBridgeAlongX(
        ChunkColumn chunk,
        int cMinX,
        int cMinZ,
        int x0,
        int x1,
        int cz,
        int hubX,
        int baseY)
    {
        for (var wx = x0; wx <= x1; wx++)
        {
            for (var dz = -2; dz <= 2; dz++)
            {
                var edge = Math.Abs(dz) == 2;
                Set(chunk, cMinX, cMinZ, wx, baseY, cz + dz, _bricks);
                Set(chunk, cMinX, cMinZ, wx, baseY + 1, cz + dz, edge ? _bricks : _air);
                Set(chunk, cMinX, cMinZ, wx, baseY + 2, cz + dz, _air);
                Set(chunk, cMinX, cMinZ, wx, baseY + 3, cz + dz, _air);
                Set(chunk, cMinX, cMinZ, wx, baseY + 4, cz + dz, _air);
            }

            if (((wx - hubX) % 5) != 0)
            {
                continue;
            }

            BuildSupportLeg(chunk, cMinX, cMinZ, wx, cz - 2, baseY);
            BuildSupportLeg(chunk, cMinX, cMinZ, wx, cz + 2, baseY);
        }
    }

    private void BuildBridgeAlongZ(
        ChunkColumn chunk,
        int cMinX,
        int cMinZ,
        int z0,
        int z1,
        int cx,
        int hubZ,
        int baseY)
    {
        for (var wz = z0; wz <= z1; wz++)
        {
            for (var dx = -2; dx <= 2; dx++)
            {
                var edge = Math.Abs(dx) == 2;
                Set(chunk, cMinX, cMinZ, cx + dx, baseY, wz, _bricks);
                Set(chunk, cMinX, cMinZ, cx + dx, baseY + 1, wz, edge ? _bricks : _air);
                Set(chunk, cMinX, cMinZ, cx + dx, baseY + 2, wz, _air);
                Set(chunk, cMinX, cMinZ, cx + dx, baseY + 3, wz, _air);
                Set(chunk, cMinX, cMinZ, cx + dx, baseY + 4, wz, _air);
            }

            if (((wz - hubZ) % 5) != 0)
            {
                continue;
            }

            BuildSupportLeg(chunk, cMinX, cMinZ, cx - 2, wz, baseY);
            BuildSupportLeg(chunk, cMinX, cMinZ, cx + 2, wz, baseY);
        }
    }

    /// <summary>Drops a Nether Brick pillar below a bridge deck until it meets solid ground (or lava) like vanilla bridge legs.</summary>
    private void BuildSupportLeg(ChunkColumn chunk, int cMinX, int cMinZ, int wx, int wz, int baseY)
    {
        for (var y = baseY - 1; y >= baseY - 40; y--)
        {
            var current = Get(chunk, cMinX, cMinZ, wx, y, wz);
            if (!current.IsAir && current != _lava)
            {
                return;
            }

            Set(chunk, cMinX, cMinZ, wx, y, wz, _bricks);
        }
    }

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

    private static BlockState Get(ChunkColumn chunk, int cMinX, int cMinZ, int wx, int wy, int wz)
    {
        var lx = wx - cMinX;
        var lz = wz - cMinZ;
        if ((uint)lx >= 16U || (uint)lz >= 16U || wy < MinBuildY || wy > MaxBuildY)
        {
            return BlockState.Air;
        }

        return chunk.GetBlockState(lx, wy, lz);
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
