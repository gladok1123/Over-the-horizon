using System.Buffers;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.World.Storage;

namespace OverHorizon.World.Lighting;

/// <summary>
/// Bounded vertical-band relighting for zero-stutter single-block edits.
/// </summary>
public static partial class LightingEngine
{
    /// <summary>
    /// Recomputes sky and block light strictly inside the vertical section band covering <c>[minWorldY, maxWorldY]</c>,
    /// preserving untouched sections' Copy-on-Write <see cref="ChunkSectionSnapshot"/> caches.
    /// </summary>
    public static void RefreshChunkLightBand(ChunkColumn chunk, int minWorldY, int maxWorldY, IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(blocks);

        var minSec = Math.Clamp((minWorldY - EngineConstants.WorldMinY) >> 4, 0, ChunkColumn.SectionCount - 1);
        var maxSec = Math.Clamp((maxWorldY - EngineConstants.WorldMinY) >> 4, 0, ChunkColumn.SectionCount - 1);
        var bandMinY = EngineConstants.WorldMinY + (minSec << 4);
        var bandMaxYExclusive = EngineConstants.WorldMinY + ((maxSec + 1) << 4);

        for (var s = minSec; s <= maxSec; s++)
        {
            chunk.GetSection(s).ClearLight();
        }

        var queue = ArrayPool<int>.Shared.Rent(4096);
        try
        {
            var head = 0;
            var tail = 0;
            SeedSkyBand(chunk, bandMinY, bandMaxYExclusive, ref queue, ref tail);
            SeedBoundaryPlaneIntoBand(chunk, blocks, isSky: true, bandMinY, bandMaxYExclusive, ref queue, ref tail);
            PropagateLightBand(chunk, blocks, isSkyLight: true, bandMinY, bandMaxYExclusive, ref queue, ref head, ref tail);

            head = 0;
            tail = 0;
            SeedBlockSourcesInBand(chunk, blocks, minSec, maxSec, ref queue, ref tail);
            SeedBoundaryPlaneIntoBand(chunk, blocks, isSky: false, bandMinY, bandMaxYExclusive, ref queue, ref tail);
            PropagateLightBand(chunk, blocks, isSkyLight: false, bandMinY, bandMaxYExclusive, ref queue, ref head, ref tail);
        }
        finally
        {
            ArrayPool<int>.Shared.Return(queue, clearArray: false);
        }
    }

    /// <summary>
    /// Propagates cardinal neighbor border light into <paramref name="center"/> strictly inside <c>[minWorldY, maxWorldY]</c>.
    /// </summary>
    public static bool PropagateCrossChunkLightBand(
        ChunkColumn center,
        ChunkColumn? north,
        ChunkColumn? south,
        ChunkColumn? west,
        ChunkColumn? east,
        int minWorldY,
        int maxWorldY,
        IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(center);
        ArgumentNullException.ThrowIfNull(blocks);

        if (north is null && south is null && west is null && east is null)
        {
            return false;
        }

        var minSec = Math.Clamp((minWorldY - EngineConstants.WorldMinY) >> 4, 0, ChunkColumn.SectionCount - 1);
        var maxSec = Math.Clamp((maxWorldY - EngineConstants.WorldMinY) >> 4, 0, ChunkColumn.SectionCount - 1);
        var bandMinY = EngineConstants.WorldMinY + (minSec << 4);
        var bandMaxYExclusive = EngineConstants.WorldMinY + ((maxSec + 1) << 4);

        var queue = ArrayPool<int>.Shared.Rent(4096);
        try
        {
            var changed = false;
            for (var pass = 0; pass < 2; pass++)
            {
                var isSky = pass == 0;
                var head = 0;
                var tail = 0;

                SeedBandSeam(center, north, blocks, isSky, true, 0, 15, bandMinY, bandMaxYExclusive, ref queue, ref tail);
                SeedBandSeam(center, south, blocks, isSky, true, 15, 0, bandMinY, bandMaxYExclusive, ref queue, ref tail);
                SeedBandSeam(center, west, blocks, isSky, false, 0, 15, bandMinY, bandMaxYExclusive, ref queue, ref tail);
                SeedBandSeam(center, east, blocks, isSky, false, 15, 0, bandMinY, bandMaxYExclusive, ref queue, ref tail);

                if (tail > 0)
                {
                    changed = true;
                    PropagateLightBand(center, blocks, isSky, bandMinY, bandMaxYExclusive, ref queue, ref head, ref tail);
                }
            }

            return changed;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(queue, clearArray: false);
        }
    }

    private static void SeedSkyBand(ChunkColumn chunk, int bandMinY, int bandMaxYExclusive, ref int[] queue, ref int tail)
    {
        for (var z = 0; z < 16; z++)
        {
            for (var x = 0; x < 16; x++)
            {
                var stopY = chunk.GetHeight(HeightmapType.LightBlocking, x, z);
                if (stopY >= bandMaxYExclusive) continue;

                var maxNeighborStopY = GetMaxNeighborLightBlockingHeight(chunk, x, z);
                var startY = bandMaxYExclusive - 1;
                var endY = Math.Max(stopY, bandMinY);
                for (var y = startY; y >= endY; y--)
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

    private static void SeedBlockSourcesInBand(ChunkColumn chunk, IBlockRegistry blocks, int minSec, int maxSec, ref int[] queue, ref int tail)
    {
        for (var s = minSec; s <= maxSec; s++)
        {
            var sec = chunk.GetSection(s);
            if (sec.IsOnlyAir) continue;
            var baseY = EngineConstants.WorldMinY + (s << 4);
            for (var ly = 0; ly < 16; ly++)
            {
                for (var lz = 0; lz < 16; lz++)
                {
                    for (var lx = 0; lx < 16; lx++)
                    {
                        var st = sec.GetBlockState(lx, ly, lz);
                        if (st.IsAir) continue;
                        var em = blocks.GetStateMetadata(st).LightEmission;
                        if (em > 0)
                        {
                            sec.SetBlockLight(lx, ly, lz, em);
                            if (em > 1) Enqueue(ref queue, ref tail, PackCoord(lx, baseY + ly, lz));
                        }
                    }
                }
            }
        }
    }

    private static void SeedBoundaryPlaneIntoBand(
        ChunkColumn chunk,
        IBlockRegistry blocks,
        bool isSky,
        int bandMinY,
        int bandMaxYExclusive,
        ref int[] queue,
        ref int tail)
    {
        if (bandMinY > EngineConstants.WorldMinY)
        {
            SeedHorizontalPlane(chunk, blocks, isSky, srcY: bandMinY - 1, dstY: bandMinY, ref queue, ref tail);
        }

        if (bandMaxYExclusive < EngineConstants.WorldMaxY)
        {
            SeedHorizontalPlane(chunk, blocks, isSky, srcY: bandMaxYExclusive, dstY: bandMaxYExclusive - 1, ref queue, ref tail);
        }
    }

    private static void SeedHorizontalPlane(
        ChunkColumn chunk,
        IBlockRegistry blocks,
        bool isSky,
        int srcY,
        int dstY,
        ref int[] queue,
        ref int tail)
    {
        for (var z = 0; z < 16; z++)
        {
            for (var x = 0; x < 16; x++)
            {
                var src = isSky ? chunk.GetSkyLight(x, srcY, z) : chunk.GetBlockLight(x, srcY, z);
                if (src <= 1) continue;
                var meta = blocks.GetStateMetadata(chunk.GetBlockState(x, dstY, z));
                if (meta.IsOpaque) continue;
                var cand = src - (meta.IsFluid ? 2 : 1);
                var cur = isSky ? chunk.GetSkyLight(x, dstY, z) : chunk.GetBlockLight(x, dstY, z);
                if (cand > cur)
                {
                    if (isSky) chunk.SetSkyLight(x, dstY, z, (byte)cand);
                    else chunk.SetBlockLight(x, dstY, z, (byte)cand);
                    if (cand > 1) Enqueue(ref queue, ref tail, PackCoord(x, dstY, z));
                }
            }
        }
    }

    private static void SeedBandSeam(
        ChunkColumn center,
        ChunkColumn? neighbor,
        IBlockRegistry blocks,
        bool isSky,
        bool isZSeam,
        int centerEdge,
        int neighborEdge,
        int bandMinY,
        int bandMaxYExclusive,
        ref int[] queue,
        ref int tail)
    {
        if (neighbor is null) return;
        for (var y = bandMinY; y < bandMaxYExclusive; y++)
        {
            for (var i = 0; i < 16; i++)
            {
                var nx = isZSeam ? i : neighborEdge;
                var nz = isZSeam ? neighborEdge : i;
                var src = isSky ? neighbor.GetSkyLight(nx, y, nz) : neighbor.GetBlockLight(nx, y, nz);
                if (src <= 1) continue;
                var cx = isZSeam ? i : centerEdge;
                var cz = isZSeam ? centerEdge : i;
                var meta = blocks.GetStateMetadata(center.GetBlockState(cx, y, cz));
                if (meta.IsOpaque) continue;
                var cand = src - (meta.IsFluid ? 2 : 1);
                var cur = isSky ? center.GetSkyLight(cx, y, cz) : center.GetBlockLight(cx, y, cz);
                if (cand > cur)
                {
                    if (isSky) center.SetSkyLight(cx, y, cz, (byte)cand);
                    else center.SetBlockLight(cx, y, cz, (byte)cand);
                    if (cand > 1) Enqueue(ref queue, ref tail, PackCoord(cx, y, cz));
                }
            }
        }
    }

    private static void PropagateLightBand(
        ChunkColumn chunk,
        IBlockRegistry blocks,
        bool isSkyLight,
        int bandMinY,
        int bandMaxYExclusive,
        ref int[] queue,
        ref int head,
        ref int tail)
    {
        var dirs = DirectionExtensions.All;
        while (head < tail)
        {
            UnpackCoord(queue[head++], out var x, out var y, out var z);
            var cur = isSkyLight ? chunk.GetSkyLight(x, y, z) : chunk.GetBlockLight(x, y, z);
            if (cur <= 1) continue;

            for (var d = 0; d < dirs.Length; d++)
            {
                var n = dirs[d].Normal();
                var nx = x + n.X;
                var ny = y + n.Y;
                var nz = z + n.Z;
                if ((uint)nx >= 16U || (uint)nz >= 16U || ny < bandMinY || ny >= bandMaxYExclusive) continue;

                var meta = blocks.GetStateMetadata(chunk.GetBlockState(nx, ny, nz));
                if (meta.IsOpaque) continue;
                var cand = cur - (meta.IsFluid ? 2 : 1);
                if (cand <= 0) continue;

                var existing = isSkyLight ? chunk.GetSkyLight(nx, ny, nz) : chunk.GetBlockLight(nx, ny, nz);
                if (cand > existing)
                {
                    if (isSkyLight) chunk.SetSkyLight(nx, ny, nz, (byte)cand);
                    else chunk.SetBlockLight(nx, ny, nz, (byte)cand);
                    if (cand > 1) Enqueue(ref queue, ref tail, PackCoord(nx, ny, nz));
                }
            }
        }
    }
}
