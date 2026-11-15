using OverHorizon.Rendering.Backends;

namespace OverHorizon.Rendering.Threading;

/// <summary>
/// Per-worker staging buffers that let several threads tessellate into the same logical vertex stream at the
/// same time. Each worker appends into its own bucket (so no list is ever touched concurrently) and
/// <see cref="DrainTo"/> concatenates the buckets back into the real destination in deterministic worker order.
/// </summary>
internal sealed class ParallelVertexSink
{
    private readonly List<OverlayVertex>[] _buckets;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParallelVertexSink"/> class.
    /// </summary>
    /// <param name="workerCount">Number of worker buckets to provision.</param>
    /// <param name="capacityPerWorker">Initial capacity reserved for each bucket.</param>
    public ParallelVertexSink(int workerCount, int capacityPerWorker = 512)
    {
        var workers = Math.Clamp(workerCount, 1, RenderParallelism.MaxSupportedWorkers);
        _buckets = new List<OverlayVertex>[workers];
        for (var i = 0; i < workers; i++)
        {
            _buckets[i] = new List<OverlayVertex>(capacityPerWorker);
        }
    }

    /// <summary>Gets the number of provisioned worker buckets.</summary>
    public int WorkerCount => _buckets.Length;

    /// <summary>Gets the total number of vertices currently staged across every bucket.</summary>
    public int TotalCount
    {
        get
        {
            var total = 0;
            for (var i = 0; i < _buckets.Length; i++)
            {
                total += _buckets[i].Count;
            }

            return total;
        }
    }

    /// <summary>
    /// Gets the exclusive write bucket for <paramref name="workerIndex"/>.
    /// </summary>
    /// <param name="workerIndex">Zero-based worker index.</param>
    /// <returns>The list that worker may append into.</returns>
    public List<OverlayVertex> Bucket(int workerIndex) => _buckets[workerIndex];

    /// <summary>Clears every bucket without releasing their capacity, ready for the next frame.</summary>
    public void Reset()
    {
        for (var i = 0; i < _buckets.Length; i++)
        {
            _buckets[i].Clear();
        }
    }

    /// <summary>
    /// Appends every staged bucket to <paramref name="destination"/> in worker order.
    /// </summary>
    /// <param name="destination">Real vertex list the geometry is finally uploaded from.</param>
    /// <returns>Number of vertices appended.</returns>
    public int DrainTo(List<OverlayVertex> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var total = 0;
        for (var i = 0; i < _buckets.Length; i++)
        {
            var bucket = _buckets[i];
            if (bucket.Count == 0)
            {
                continue;
            }

            destination.AddRange(bucket);
            total += bucket.Count;
        }

        return total;
    }
}
