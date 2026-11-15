using OverHorizon.Rendering.Threading;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Reusable per-worker staging buffers for the multi-threaded chunk culling stage. Workers append into their
/// own lists (never sharing a list), the counters are kept in separate array slots to avoid write contention,
/// and the owning renderer merges everything back in deterministic worker order once per frame.
/// </summary>
/// <typeparam name="TSection">Backend-specific uploaded section type.</typeparam>
internal sealed class SectionCullScratch<TSection>
    where TSection : class, IResidentSection
{
    private readonly List<CulledSectionDraw<TSection>>[] _opaque;
    private readonly List<CulledSectionDraw<TSection>>[] _translucent;
    private readonly int[] _visible;
    private readonly int[] _culled;

    /// <summary>
    /// Initializes a new instance of the <see cref="SectionCullScratch{TSection}"/> class.
    /// </summary>
    /// <param name="workerCapacity">Maximum number of workers this scratch can serve.</param>
    public SectionCullScratch(int workerCapacity)
    {
        var workers = Math.Clamp(workerCapacity, 1, RenderParallelism.MaxSupportedWorkers);
        _opaque = new List<CulledSectionDraw<TSection>>[workers];
        _translucent = new List<CulledSectionDraw<TSection>>[workers];
        _visible = new int[workers];
        _culled = new int[workers];

        for (var i = 0; i < workers; i++)
        {
            _opaque[i] = new List<CulledSectionDraw<TSection>>(256);
            _translucent[i] = new List<CulledSectionDraw<TSection>>(64);
        }
    }

    /// <summary>Gets the number of worker slots provisioned.</summary>
    public int WorkerCount => _opaque.Length;

    /// <summary>Merged opaque draw list rebuilt every frame by <see cref="Merge"/>.</summary>
    public List<CulledSectionDraw<TSection>> Opaque { get; } = new(1024);

    /// <summary>Merged translucent draw list rebuilt every frame by <see cref="Merge"/>.</summary>
    public List<CulledSectionDraw<TSection>> Translucent { get; } = new(256);

    /// <summary>Gets the total number of sections that passed the frustum test on the last cull.</summary>
    public int VisibleCount { get; private set; }

    /// <summary>Gets the total number of sections rejected by the frustum or distance test on the last cull.</summary>
    public int CulledCount { get; private set; }

    /// <summary>
    /// Gets the per-worker opaque staging list for <paramref name="workerIndex"/>.
    /// </summary>
    /// <param name="workerIndex">Zero-based worker index.</param>
    /// <returns>The list that worker exclusively owns.</returns>
    public List<CulledSectionDraw<TSection>> OpaqueBucket(int workerIndex) => _opaque[workerIndex];

    /// <summary>
    /// Gets the per-worker translucent staging list for <paramref name="workerIndex"/>.
    /// </summary>
    /// <param name="workerIndex">Zero-based worker index.</param>
    /// <returns>The list that worker exclusively owns.</returns>
    public List<CulledSectionDraw<TSection>> TranslucentBucket(int workerIndex) => _translucent[workerIndex];

    /// <summary>Clears every worker bucket and counter, ready for a new frame.</summary>
    public void BeginFrame()
    {
        for (var i = 0; i < _opaque.Length; i++)
        {
            _opaque[i].Clear();
            _translucent[i].Clear();
            _visible[i] = 0;
            _culled[i] = 0;
        }
    }

    /// <summary>
    /// Adds culling bookkeeping for one worker.
    /// </summary>
    /// <param name="workerIndex">Zero-based worker index.</param>
    /// <param name="visible">Sections accepted by this worker.</param>
    /// <param name="culled">Sections rejected by this worker.</param>
    public void AddCounts(int workerIndex, int visible, int culled)
    {
        _visible[workerIndex] += visible;
        _culled[workerIndex] += culled;
    }

    /// <summary>
    /// Concatenates every worker bucket into <see cref="Opaque"/> and <see cref="Translucent"/> in worker order
    /// and recomputes the aggregated counters.
    /// </summary>
    public void Merge()
    {
        Opaque.Clear();
        Translucent.Clear();

        var visible = 0;
        var culled = 0;
        for (var i = 0; i < _opaque.Length; i++)
        {
            if (_opaque[i].Count > 0)
            {
                Opaque.AddRange(_opaque[i]);
            }

            if (_translucent[i].Count > 0)
            {
                Translucent.AddRange(_translucent[i]);
            }

            visible += _visible[i];
            culled += _culled[i];
        }

        VisibleCount = visible;
        CulledCount = culled;
    }
}
