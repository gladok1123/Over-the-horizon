namespace OverHorizon.Rendering.Threading;

/// <summary>
/// Central CPU work-distribution policy shared by every per-frame render job (frustum culling, overlay
/// geometry assembly, cloud tessellation). It decides how many worker threads a job may borrow and splits
/// an item range across them, always degrading gracefully to a single thread for tiny workloads where the
/// parallel hand-off would cost more than the work itself.
/// </summary>
public static class RenderParallelism
{
    /// <summary>Default minimum number of work items a worker must receive before a job is split at all.</summary>
    public const int DefaultMinItemsPerWorker = 48;

    /// <summary>Upper bound on borrowed workers so a 128-thread server never spawns a thundering herd for a frame.</summary>
    public const int MaxSupportedWorkers = 16;

    /// <summary>
    /// Gets the number of logical processors available to this process, clamped into <c>[1, <see cref="MaxSupportedWorkers"/>]</c>.
    /// </summary>
    public static int AutoWorkerCount =>
        Math.Clamp(Environment.ProcessorCount, 1, MaxSupportedWorkers);

    /// <summary>
    /// Resolves how many workers a job over <paramref name="workItems"/> should use.
    /// </summary>
    /// <param name="workItems">Total number of independent work items in the job.</param>
    /// <param name="minItemsPerWorker">Minimum items per worker; the job stays single-threaded below this.</param>
    /// <param name="maxThreads">Caller-imposed worker cap (<c>0</c> or negative means <see cref="AutoWorkerCount"/>).</param>
    /// <returns>A worker count in <c>[0, <see cref="MaxSupportedWorkers"/>]</c>; <c>0</c> means there is nothing to do.</returns>
    public static int ResolveWorkerCount(int workItems, int minItemsPerWorker = DefaultMinItemsPerWorker, int maxThreads = 0)
    {
        if (workItems <= 0)
        {
            return 0;
        }

        var cap = maxThreads <= 0 ? AutoWorkerCount : Math.Clamp(maxThreads, 1, MaxSupportedWorkers);
        var effectiveMin = Math.Max(1, minItemsPerWorker);
        var affordable = Math.Max(1, workItems / effectiveMin);
        return Math.Min(cap, affordable);
    }

    /// <summary>
    /// Computes the contiguous half-open item range <c>[Start, End)</c> owned by one worker so every item is
    /// processed exactly once and the split stays perfectly balanced.
    /// </summary>
    /// <param name="total">Total number of work items.</param>
    /// <param name="workerCount">Number of workers the job was split across.</param>
    /// <param name="workerIndex">Zero-based worker index.</param>
    /// <returns>The half-open range owned by <paramref name="workerIndex"/>.</returns>
    public static (int Start, int End) RangeFor(int total, int workerCount, int workerIndex)
    {
        if (total <= 0 || workerCount <= 0)
        {
            return (0, 0);
        }

        var baseSize = total / workerCount;
        var remainder = total % workerCount;
        var start = (workerIndex * baseSize) + Math.Min(workerIndex, remainder);
        var end = start + baseSize + (workerIndex < remainder ? 1 : 0);
        return (start, Math.Min(end, total));
    }
}
