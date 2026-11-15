using System.Collections;
using OverHorizon.Core;
using OverHorizon.Core.Math;
using OverHorizon.Shared.Hashing;

namespace OverHorizon.World.ChunkManagement;

/// <summary>
/// Owns one independent <see cref="ChunkManager"/> (and therefore one generator + storage backend)
/// per <see cref="DimensionId"/>, letting the Overworld and the Nether stream, generate, and persist
/// completely separately while sharing a single world tick.
/// </summary>
public sealed class DimensionWorldCollection : IAsyncDisposable, IDisposable, IEnumerable<KeyValuePair<DimensionId, ChunkManager>>
{
    private readonly Dictionary<DimensionId, ChunkManager> _managers = [];
    private int _disposed;

    /// <summary>
    /// Registers a dimension-scoped chunk manager, replacing any previous registration.
    /// </summary>
    /// <param name="dimension">Dimension identifier.</param>
    /// <param name="manager">Chunk manager backing that dimension.</param>
    public void Register(DimensionId dimension, ChunkManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        _managers[dimension] = manager;
    }

    /// <summary>
    /// Resolves the chunk manager responsible for <paramref name="dimension"/>.
    /// </summary>
    /// <param name="dimension">Dimension identifier.</param>
    /// <returns>The registered <see cref="ChunkManager"/>.</returns>
    public ChunkManager Get(DimensionId dimension)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        if (_managers.TryGetValue(dimension, out var manager))
        {
            return manager;
        }

        if (_managers.TryGetValue(DimensionId.Overworld, out var overworld))
        {
            return overworld;
        }

        throw new InvalidOperationException($"No chunk manager is registered for dimension '{dimension}'.");
    }

    /// <summary>Gets the chunk manager responsible for <paramref name="dimension"/>.</summary>
    /// <param name="dimension">Dimension identifier.</param>
    public ChunkManager this[DimensionId dimension] => Get(dimension);

    /// <summary>Gets the registered dimensions in a stable order.</summary>
    public IReadOnlyList<DimensionId> Dimensions => SortedDimensions();

    /// <summary>Gets the total number of loaded chunks across every dimension.</summary>
    public int LoadedChunkCount
    {
        get
        {
            var total = 0;
            foreach (var manager in _managers.Values)
            {
                total += manager.LoadedChunkCount;
            }

            return total;
        }
    }

    /// <summary>
    /// Computes a deterministic 64-bit hash across every loaded chunk of every dimension.
    /// </summary>
    /// <returns>Combined deterministic world hash.</returns>
    public ulong ComputeLoadedWorldHash()
    {
        var hasher = Fnv1a64.Create();
        foreach (var dimension in SortedDimensions())
        {
            hasher.AppendInt32((int)dimension);
            hasher.AppendInt64(unchecked((long)Get(dimension).ComputeLoadedWorldHash()));
        }

        return hasher.Value;
    }

    /// <summary>
    /// Loads or generates pending chunks in every dimension.
    /// </summary>
    /// <param name="maxChunksToLoad">Maximum chunks to load per dimension.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Total number of chunks loaded.</returns>
    public async ValueTask<int> ProcessTicketsAsync(int maxChunksToLoad, CancellationToken cancellationToken)
    {
        var total = 0;
        foreach (var dimension in SortedDimensions())
        {
            total += await Get(dimension).ProcessTicketsAsync(maxChunksToLoad, cancellationToken).ConfigureAwait(false);
        }

        return total;
    }

    /// <summary>
    /// Flushes every dirty chunk in every dimension to storage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of saved chunks.</returns>
    public async ValueTask<int> SaveDirtyChunksAsync(CancellationToken cancellationToken)
    {
        var total = 0;
        foreach (var dimension in SortedDimensions())
        {
            total += await Get(dimension).SaveDirtyChunksAsync(cancellationToken).ConfigureAwait(false);
        }

        return total;
    }

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<DimensionId, ChunkManager>> GetEnumerator() =>
        _managers.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        foreach (var manager in _managers.Values)
        {
            await manager.DisposeAsync().ConfigureAwait(false);
        }

        _managers.Clear();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        foreach (var manager in _managers.Values)
        {
            manager.Dispose();
        }

        _managers.Clear();
    }

    private DimensionId[] SortedDimensions()
    {
        var ids = new DimensionId[_managers.Count];
        _managers.Keys.CopyTo(ids, 0);
        Array.Sort(ids, static (a, b) => ((byte)a).CompareTo((byte)b));
        return ids;
    }
}
