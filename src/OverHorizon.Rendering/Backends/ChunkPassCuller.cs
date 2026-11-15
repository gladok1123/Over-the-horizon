using System.Numerics;
using OverHorizon.Core.Math;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Threading;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// The multi-threaded frustum and distance culling stage shared by every render backend. It walks the resident
/// chunk columns, keeps the sections whose world bounds survive the frustum and the horizontal distance cutoff,
/// and fills <see cref="SectionCullScratch{TSection}"/> with the camera-relative draw lists the render thread
/// then replays in order. Culling is pure arithmetic with no graphics API calls, so it is safe (and
/// profitable) to fan out across the worker pool.
/// </summary>
internal static class ChunkPassCuller
{
    /// <summary>
    /// Culls every resident chunk column and fills <paramref name="scratch"/> with the visible draw lists.
    /// </summary>
    /// <typeparam name="TSection">Backend-specific uploaded section type.</typeparam>
    /// <param name="columns">Snapshot array of every resident chunk column.</param>
    /// <param name="columnCount">Number of valid entries in <paramref name="columns"/>.</param>
    /// <param name="frustum">Camera frustum used for the visibility tests.</param>
    /// <param name="cameraPos">Camera world position.</param>
    /// <param name="maxHorizDistSq">Squared horizontal render distance cutoff.</param>
    /// <param name="scratch">Per-worker staging buffers that receive the results.</param>
    /// <param name="workerCount">Number of workers to use (<c>1</c> keeps the job on the calling thread).</param>
    public static void CullVisibleSections<TSection>(
        ChunkColumnSlots<TSection>[] columns,
        int columnCount,
        in Frustum frustum,
        Vec3d cameraPos,
        float maxHorizDistSq,
        SectionCullScratch<TSection> scratch,
        int workerCount)
        where TSection : class, IResidentSection, IDisposable
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(scratch);

        scratch.BeginFrame();

        // An `in` parameter cannot be captured by a lambda, so the frustum is copied into a local first.
        var cullFrustum = frustum;
        var workers = Math.Clamp(workerCount, 1, scratch.WorkerCount);
        if (workers <= 1)
        {
            CullColumnRange(columns, columnCount, in cullFrustum, cameraPos, maxHorizDistSq, scratch, 0);
        }
        else
        {
            Parallel.For(
                0,
                workers,
                worker => CullColumnRange(
                    columns,
                    columnCount,
                    in cullFrustum,
                    cameraPos,
                    maxHorizDistSq,
                    scratch,
                    worker,
                    workers));
        }

        scratch.Merge();
    }

    private static void CullColumnRange<TSection>(
        ChunkColumnSlots<TSection>[] columns,
        int columnCount,
        in Frustum frustum,
        Vec3d cameraPos,
        float maxHorizDistSq,
        SectionCullScratch<TSection> scratch,
        int workerIndex,
        int workerCount = 1)
        where TSection : class, IResidentSection, IDisposable
    {
        var (start, end) = RenderParallelism.RangeFor(columnCount, workerCount, workerIndex);
        var opaque = scratch.OpaqueBucket(workerIndex);
        var translucent = scratch.TranslucentBucket(workerIndex);
        var visible = 0;
        var culled = 0;

        for (var i = start; i < end; i++)
        {
            var col = columns[i];
            var mask = col.PresentMask;
            if (mask == 0)
            {
                continue;
            }

            var dx = (float)((col.ChunkPosition.X * 16.0) + 8.0 - cameraPos.X);
            var dz = (float)((col.ChunkPosition.Z * 16.0) + 8.0 - cameraPos.Z);
            if (((dx * dx) + (dz * dz)) > maxHorizDistSq || !frustum.Intersects(col.ActiveWorldBounds))
            {
                culled += BitOperations.PopCount(mask);
                continue;
            }

            while (mask != 0)
            {
                var slot = BitOperations.TrailingZeroCount(mask);
                mask &= mask - 1;
                var section = col.GetSlot(slot);
                if (section is null)
                {
                    continue;
                }

                if (!frustum.Intersects(section.WorldBounds))
                {
                    culled++;
                    continue;
                }

                visible++;
                var relOffset = ComputeCameraRelativeOffset(section.SectionPosition, cameraPos);

                if (section.OpaqueIndexCount > 0)
                {
                    opaque.Add(new CulledSectionDraw<TSection>(section, relOffset, 0.0f));
                }

                if (section.TranslucentIndexCount > 0)
                {
                    var center = relOffset + new Vector3(8.0f, 8.0f, 8.0f);
                    translucent.Add(new CulledSectionDraw<TSection>(section, relOffset, center.LengthSquared()));
                }
            }
        }

        scratch.AddCounts(workerIndex, visible, culled);
    }

    private static Vector3 ComputeCameraRelativeOffset(SectionPos sectionPos, Vec3d cameraPos) =>
        new(
            (float)((sectionPos.X * 16.0) - cameraPos.X),
            (float)((sectionPos.Y * 16.0) - cameraPos.Y),
            (float)((sectionPos.Z * 16.0) - cameraPos.Z));
}
