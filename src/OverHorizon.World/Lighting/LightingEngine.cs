using System.Buffers;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.World.Storage;

namespace OverHorizon.World.Lighting;

/// <summary>
/// Deterministic, zero-allocation (pooled) sky and block light propagation engine for <see cref="ChunkColumn"/>s.
/// </summary>
public static class LightingEngine
{
    private const int InitialQueueCapacity = 16_384;

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
