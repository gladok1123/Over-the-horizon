using OverHorizon.Core.Math;
using OverHorizon.Rendering.Threading;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Multi-threaded tessellation stages for <see cref="SilkOpenGlOverlayRenderer"/>: entity batches are sliced
/// across the worker pool into per-worker vertex buckets and merged back in deterministic order, and the cloud
/// deck is tessellated one band of grid rows per worker (split out to respect the 500-line architecture budget).
/// </summary>
internal sealed partial class SilkOpenGlOverlayRenderer
{
    /// <summary>Minimum entity count per worker before an entity tessellation job is split.</summary>
    private const int EntityMinItemsPerWorker = 24;

    /// <summary>Minimum particle count per worker before the particle tessellation job is split.</summary>
    private const int ParticleMinItemsPerWorker = 48;

    /// <summary>Minimum cloud grid rows per worker before the cloud tessellation job is split.</summary>
    private const int CloudMinRowsPerWorker = 3;

    /// <summary>
    /// Tessellates a per-frame entity batch into <see cref="_worldTriangleVertices"/>, fanning the work out over
    /// the worker pool when the batch is large enough to pay for the hand-off.
    /// </summary>
    /// <typeparam name="T">Visual element type.</typeparam>
    /// <param name="items">Batch of visual elements (may be <see langword="null"/>).</param>
    /// <param name="minItemsPerWorker">Minimum items per worker before splitting.</param>
    /// <param name="append">Callback that emits one slice into a worker bucket.</param>
    private void ParallelAppend<T>(
        IReadOnlyList<T>? items,
        int minItemsPerWorker,
        Action<List<OverlayVertex>, IReadOnlyList<T>> append)
    {
        if (items is null || items.Count == 0)
        {
            return;
        }

        var workers = RenderParallelism.ResolveWorkerCount(items.Count, minItemsPerWorker, _maxRenderThreads);
        if (workers <= 1)
        {
            append(_worldTriangleVertices, items);
            return;
        }

        var sink = _worldSink;
        var capped = Math.Min(workers, sink.WorkerCount);
        sink.Reset();
        Parallel.For(0, capped, worker =>
        {
            var (start, end) = RenderParallelism.RangeFor(items.Count, capped, worker);
            if (end <= start)
            {
                return;
            }

            append(sink.Bucket(worker), new ListSlice<T>(items, start, end - start));
        });

        sink.DrainTo(_worldTriangleVertices);
    }

    /// <summary>
    /// Tessellates the drifting cloud deck across the worker pool, one contiguous band of grid rows per worker.
    /// </summary>
    /// <param name="cameraPos">Camera world position.</param>
    /// <param name="worldTimeTicks">World time used for the day/night tint.</param>
    /// <param name="driftOffset">Cloud drift offset in blocks.</param>
    /// <param name="skyBrightness">Current sky brightness multiplier.</param>
    private void AppendWorldCloudsParallel(Vec3d cameraPos, long worldTimeTicks, float driftOffset, float skyBrightness)
    {
        var workers = RenderParallelism.ResolveWorkerCount(
            CloudGridExtent,
            CloudMinRowsPerWorker,
            _maxRenderThreads);

        if (workers <= 1)
        {
            AppendWorldClouds(_cloudVertices, cameraPos, worldTimeTicks, driftOffset, skyBrightness);
            return;
        }

        var sink = _cloudSink;
        var capped = Math.Min(workers, sink.WorkerCount);
        sink.Reset();
        Parallel.For(0, capped, worker =>
        {
            var (start, end) = RenderParallelism.RangeFor(CloudGridExtent, capped, worker);
            if (end <= start)
            {
                return;
            }

            AppendWorldClouds(
                sink.Bucket(worker),
                cameraPos,
                worldTimeTicks,
                driftOffset,
                skyBrightness,
                start - CloudGridRadius,
                end - CloudGridRadius);
        });

        sink.DrainTo(_cloudVertices);
    }
}
