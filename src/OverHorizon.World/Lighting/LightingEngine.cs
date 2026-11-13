using System.Buffers;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.World.Storage;

namespace OverHorizon.World.Lighting;

/// <summary>
/// Deterministic, zero-allocation (pooled) sky and block light propagation engine for <see cref="ChunkColumn"/>s.
/// </summary>
public static partial class LightingEngine
{
    /// <summary>Minimum voxel illumination level (pitch-dark cave).</summary>
    public const byte MinLightLevel = 0;

    /// <summary>Maximum voxel illumination level (direct sunlight / lantern).</summary>
    public const byte MaxLightLevel = 15;

    private const int InitialQueueCapacity = 16_384;

    /// <summary>
    /// Computes dynamic block light (<c>0..15</c>) contributed at <paramref name="samplePosition"/> by a held
    /// light source (e.g. a Torch with emission <c>14</c> or Lantern with emission <c>15</c>) at <paramref name="emitterPosition"/>.
    /// </summary>
    public static byte ComputeDynamicLightLevel(Vec3d emitterPosition, byte emittedLight, Vec3d samplePosition)
    {
        if (emittedLight == 0)
        {
            return MinLightLevel;
        }

        var clampedEmission = Math.Min(emittedLight, MaxLightLevel);
        var dist = emitterPosition.DistanceTo(samplePosition);
        var falloff = (int)Math.Floor(dist);
        var remaining = clampedEmission - falloff;
        return remaining <= 0 ? MinLightLevel : (byte)Math.Min(remaining, MaxLightLevel);
    }

    /// <summary>
    /// Combines sky light (<c>0..15</c>), static block light (<c>0..15</c>), and dynamic held-item light (<c>0..15</c>)
    /// into the effective voxel illumination level on <c>[0.0, 15.0]</c>.
    /// </summary>
    public static float ComputeEffectiveLightLevel(byte skyLight, byte blockLight, byte dynamicLight, float skyBrightness = 1.0f)
    {
        var scaledSky = Math.Clamp(skyLight, MinLightLevel, MaxLightLevel) * Math.Clamp(skyBrightness, 0.0f, 1.0f);
        var maxBlock = (float)Math.Max(
            Math.Clamp(blockLight, MinLightLevel, MaxLightLevel),
            Math.Clamp(dynamicLight, MinLightLevel, MaxLightLevel));
        return Math.Clamp(MathF.Max(scaledSky, maxBlock), 0.0f, 15.0f);
    }

    /// <summary>
    /// Maps a <c>0..15</c> light level onto the clean-room Minecraft perceptual brightness curve (<c>0.045..1.0</c>),
    /// ensuring pitch-black caves (<c>0</c>) render genuinely dark while level <c>15</c> reaches full brightness.
    /// </summary>
    public static float LightLevelToBrightness(float lightLevel0To15)
    {
        var normalized = Math.Clamp(lightLevel0To15, 0.0f, 15.0f) / 15.0f;
        var curved = normalized / (3.0f - (2.0f * normalized));
        return 0.045f + (curved * 0.955f);
    }

    /// <summary>
    /// Computes full sky light and emissive block light propagation across <paramref name="chunk"/>.
    /// </summary>
    /// <param name="chunk">Target chunk column (heightmaps must already be up to date).</param>
    /// <param name="blocks">Frozen block registry providing opacity and emission metadata.</param>
    public static void InitializeChunkLight(ChunkColumn chunk, IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(blocks);

        for (var s = 0; s < ChunkColumn.SectionCount; s++)
        {
            chunk.GetSection(s).ClearLight();
        }

        var queue = ArrayPool<int>.Shared.Rent(InitialQueueCapacity);
        try
        {
            var head = 0;
            var tail = 0;

            SeedSkyLightColumns(chunk, ref queue, ref tail);
            PropagateLightQueue(chunk, blocks, isSkyLight: true, ref queue, ref head, ref tail);

            head = 0;
            tail = 0;
            SeedEmissiveBlockSources(chunk, blocks, ref queue, ref tail);
            PropagateLightQueue(chunk, blocks, isSkyLight: false, ref queue, ref head, ref tail);
        }
        finally
        {
            ArrayPool<int>.Shared.Return(queue, clearArray: false);
        }
    }

    /// <summary>
    /// Updates sky and block light in <paramref name="chunk"/> after a block modification.
    /// </summary>
    /// <param name="chunk">Modified chunk column.</param>
    /// <param name="blocks">Frozen block registry.</param>
    public static void RefreshChunkLight(ChunkColumn chunk, IBlockRegistry blocks) =>
        InitializeChunkLight(chunk, blocks);

    /// <summary>
    /// Propagates sky light and emissive block light from up to four cardinal neighbor columns into <paramref name="center"/>,
    /// eliminating dark lighting seams across chunk boundaries.
    /// </summary>
    /// <param name="center">Target chunk column to receive border light.</param>
    /// <param name="north">Optional neighbor at <c>(X, Z - 1)</c>.</param>
    /// <param name="south">Optional neighbor at <c>(X, Z + 1)</c>.</param>
    /// <param name="west">Optional neighbor at <c>(X - 1, Z)</c>.</param>
    /// <param name="east">Optional neighbor at <c>(X + 1, Z)</c>.</param>
    /// <param name="blocks">Frozen block registry.</param>
    /// <returns><see langword="true"/> if at least one light nibble in <paramref name="center"/> increased.</returns>
    public static bool PropagateCrossChunkLight(
        ChunkColumn center,
        ChunkColumn? north,
        ChunkColumn? south,
        ChunkColumn? west,
        ChunkColumn? east,
        IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(center);
        ArgumentNullException.ThrowIfNull(blocks);

        if (north is null && south is null && west is null && east is null)
        {
            return false;
        }

        var queue = ArrayPool<int>.Shared.Rent(InitialQueueCapacity);
        try
        {
            var changed = false;
            for (var pass = 0; pass < 2; pass++)
            {
                var isSky = pass == 0;
                var head = 0;
                var tail = 0;

                SeedSeamFromNeighbor(center, north, blocks, isSky, isZSeam: true, centerEdge: 0, neighborEdge: 15, ref queue, ref tail);
                SeedSeamFromNeighbor(center, south, blocks, isSky, isZSeam: true, centerEdge: 15, neighborEdge: 0, ref queue, ref tail);
                SeedSeamFromNeighbor(center, west, blocks, isSky, isZSeam: false, centerEdge: 0, neighborEdge: 15, ref queue, ref tail);
                SeedSeamFromNeighbor(center, east, blocks, isSky, isZSeam: false, centerEdge: 15, neighborEdge: 0, ref queue, ref tail);

                if (tail > 0)
                {
                    changed = true;
                    PropagateLightQueue(center, blocks, isSky, ref queue, ref head, ref tail);
                }
            }

            return changed;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(queue, clearArray: false);
        }
    }

    private static void SeedSeamFromNeighbor(
        ChunkColumn center,
        ChunkColumn? neighbor,
        IBlockRegistry blocks,
        bool isSky,
        bool isZSeam,
        int centerEdge,
        int neighborEdge,
        ref int[] queue,
        ref int tail)
    {
        if (neighbor is null)
        {
            return;
        }

        for (var y = EngineConstants.WorldMinY; y < EngineConstants.WorldMaxY; y++)
        {
            for (var i = 0; i < 16; i++)
            {
                var nx = isZSeam ? i : neighborEdge;
                var nz = isZSeam ? neighborEdge : i;
                var srcLevel = isSky ? neighbor.GetSkyLight(nx, y, nz) : neighbor.GetBlockLight(nx, y, nz);
                if (srcLevel <= 1)
                {
                    continue;
                }

                var cx = isZSeam ? i : centerEdge;
                var cz = isZSeam ? centerEdge : i;
                var meta = blocks.GetStateMetadata(center.GetBlockState(cx, y, cz));
                if (meta.IsOpaque)
                {
                    continue;
                }

                var candidate = srcLevel - (meta.IsFluid ? 2 : 1);
                var existing = isSky ? center.GetSkyLight(cx, y, cz) : center.GetBlockLight(cx, y, cz);
                if (candidate <= existing)
                {
                    continue;
                }

                var nextByte = (byte)candidate;
                if (isSky)
                {
                    center.SetSkyLight(cx, y, cz, nextByte);
                }
                else
                {
                    center.SetBlockLight(cx, y, cz, nextByte);
                }

                if (nextByte > 1)
                {
                    Enqueue(ref queue, ref tail, PackCoord(cx, y, cz));
                }
            }
        }
    }

    private static void SeedSkyLightColumns(ChunkColumn chunk, ref int[] queue, ref int tail)
    {
        for (var z = 0; z < 16; z++)
        {
            for (var x = 0; x < 16; x++)
            {
                var stopY = chunk.GetHeight(HeightmapType.LightBlocking, x, z);
                var maxNeighborStopY = GetMaxNeighborLightBlockingHeight(chunk, x, z);

                for (var y = EngineConstants.WorldMaxY - 1; y >= stopY; y--)
                {
                    chunk.SetSkyLight(x, y, z, 15);
                    if (y < maxNeighborStopY || y == stopY)
                    {
                        Enqueue(ref queue, ref tail, PackCoord(x, y, z));
                    }
                }
            }
        }
    }

    private static int GetMaxNeighborLightBlockingHeight(ChunkColumn chunk, int x, int z)
    {
        var maxH = EngineConstants.WorldMinY;
        if (x > 0)
        {
            maxH = Math.Max(maxH, chunk.GetHeight(HeightmapType.LightBlocking, x - 1, z));
        }

        if (x < 15)
        {
            maxH = Math.Max(maxH, chunk.GetHeight(HeightmapType.LightBlocking, x + 1, z));
        }

        if (z > 0)
        {
            maxH = Math.Max(maxH, chunk.GetHeight(HeightmapType.LightBlocking, x, z - 1));
        }

        if (z < 15)
        {
            maxH = Math.Max(maxH, chunk.GetHeight(HeightmapType.LightBlocking, x, z + 1));
        }

        return maxH;
    }

    private static void SeedEmissiveBlockSources(
        ChunkColumn chunk,
        IBlockRegistry blocks,
        ref int[] queue,
        ref int tail)
    {
        for (var s = 0; s < ChunkColumn.SectionCount; s++)
        {
            var sec = chunk.GetSection(s);
            if (sec.IsOnlyAir)
            {
                continue;
            }

            var baseY = EngineConstants.WorldMinY + (s << 4);
            for (var ly = 0; ly < 16; ly++)
            {
                for (var lz = 0; lz < 16; lz++)
                {
                    for (var lx = 0; lx < 16; lx++)
                    {
                        var state = sec.GetBlockState(lx, ly, lz);
                        if (state.IsAir)
                        {
                            continue;
                        }

                        var emission = blocks.GetStateMetadata(state).LightEmission;
                        if (emission > 0)
                        {
                            sec.SetBlockLight(lx, ly, lz, emission);
                            if (emission > 1)
                            {
                                Enqueue(ref queue, ref tail, PackCoord(lx, baseY + ly, lz));
                            }
                        }
                    }
                }
            }
        }
    }

    private static void PropagateLightQueue(
        ChunkColumn chunk,
        IBlockRegistry blocks,
        bool isSkyLight,
        ref int[] queue,
        ref int head,
        ref int tail)
    {
        var directions = DirectionExtensions.All;

        while (head < tail)
        {
            var packed = queue[head++];
            UnpackCoord(packed, out var x, out var y, out var z);

            var currentLevel = isSkyLight
                ? chunk.GetSkyLight(x, y, z)
                : chunk.GetBlockLight(x, y, z);

            if (currentLevel <= 1)
            {
                continue;
            }

            for (var d = 0; d < directions.Length; d++)
            {
                var normal = directions[d].Normal();
                var nx = x + normal.X;
                var ny = y + normal.Y;
                var nz = z + normal.Z;

                if ((uint)nx >= 16U || (uint)nz >= 16U || ny < EngineConstants.WorldMinY || ny >= EngineConstants.WorldMaxY)
                {
                    continue;
                }

                var neighborState = chunk.GetBlockState(nx, ny, nz);
                var meta = blocks.GetStateMetadata(neighborState);
                if (meta.IsOpaque)
                {
                    continue;
                }

                var stepCost = meta.IsFluid ? 2 : 1;
                var candidate = currentLevel - stepCost;
                if (candidate <= 0)
                {
                    continue;
                }

                var existing = isSkyLight
                    ? chunk.GetSkyLight(nx, ny, nz)
                    : chunk.GetBlockLight(nx, ny, nz);

                if (candidate > existing)
                {
                    var nextByte = (byte)candidate;
                    if (isSkyLight)
                    {
                        chunk.SetSkyLight(nx, ny, nz, nextByte);
                    }
                    else
                    {
                        chunk.SetBlockLight(nx, ny, nz, nextByte);
                    }

                    if (nextByte > 1)
                    {
                        Enqueue(ref queue, ref tail, PackCoord(nx, ny, nz));
                    }
                }
            }
        }
    }

    private static void Enqueue(ref int[] queue, ref int tail, int value)
    {
        if (tail >= queue.Length)
        {
            var grown = ArrayPool<int>.Shared.Rent(queue.Length * 2);
            queue.AsSpan(0, tail).CopyTo(grown);
            ArrayPool<int>.Shared.Return(queue, clearArray: false);
            queue = grown;
        }

        queue[tail++] = value;
    }

    private static int PackCoord(int localX, int worldY, int localZ) =>
        ((worldY - EngineConstants.WorldMinY) << 8) | (localZ << 4) | localX;

    private static void UnpackCoord(int packed, out int localX, out int worldY, out int localZ)
    {
        localX = packed & 15;
        localZ = (packed >> 4) & 15;
        worldY = (packed >> 8) + EngineConstants.WorldMinY;
    }
}
