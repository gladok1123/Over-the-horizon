using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Shared.Hashing;
using OverHorizon.World.Generation;
using OverHorizon.World.Lighting;
using OverHorizon.World.Serialization;
using OverHorizon.World.Storage;

namespace OverHorizon.World.ChunkManagement;

/// <summary>
/// Horizon-style lock-free chunk manager with distance-prioritized parallel worldgen,
/// double-buffered <see cref="ChunkSnapshot"/>s, and <see cref="Channel{T}"/>-backed async persistence.
/// </summary>
public sealed class ChunkManager : IChunkManager, IAsyncDisposable, IDisposable
{
    private readonly CoreRegistries _registries;
    private readonly IWorldGenerator _generator;
    private readonly IChunkStorage _storage;

    private readonly ConcurrentDictionary<long, ChunkTicket> _tickets = new();
    private readonly ConcurrentDictionary<long, ChunkColumn> _loadedChunks = new();
    private readonly ConcurrentDictionary<long, ChunkSnapshot> _publishedSnapshots = new();
    private readonly Channel<ChunkColumn> _saveQueue;
    private readonly CancellationTokenSource _backgroundCts = new();
    private readonly Task _saveWorkerTask;
    private int _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkManager"/> class.
    /// </summary>
    /// <param name="registries">Frozen domain registries.</param>
    /// <param name="generator">Thread-safe chunk generator.</param>
    /// <param name="storage">Async chunk storage backend.</param>
    public ChunkManager(
        CoreRegistries registries,
        IWorldGenerator generator,
        IChunkStorage storage)
    {
        ArgumentNullException.ThrowIfNull(registries);
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(storage);

        _registries = registries;
        _generator = generator;
        _storage = storage;

        _saveQueue = Channel.CreateBounded<ChunkColumn>(new BoundedChannelOptions(512)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });

        _saveWorkerTask = RunSaveWorkerAsync(_backgroundCts.Token);
    }

    /// <inheritdoc />
    public int LoadedChunkCount => _loadedChunks.Count;

    /// <inheritdoc />
    public void UpsertTicket(ChunkTicket ticket)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (ticket.Radius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticket), "Ticket radius must be non-negative.");
        }

        _tickets[ticket.OwnerId] = ticket;
    }

    /// <inheritdoc />
    public bool RemoveTicket(long ownerId)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _tickets.TryRemove(ownerId, out _);
    }

    /// <inheritdoc />
    public bool TryGetSnapshot(ChunkPos position, [MaybeNullWhen(false)] out ChunkSnapshot snapshot) =>
        _publishedSnapshots.TryGetValue(position.PackInt64(), out snapshot);

    /// <inheritdoc />
    public bool SetBlockState(BlockPos worldPos, BlockState newState, long tickNumber = 0)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (worldPos.Y < EngineConstants.WorldMinY || worldPos.Y >= EngineConstants.WorldMaxY)
        {
            return false;
        }

        var chunkPos = worldPos.ToChunkPos();
        var key = chunkPos.PackInt64();
        if (!_loadedChunks.TryGetValue(key, out var column))
        {
            return false;
        }

        var lx = worldPos.SectionLocalX;
        var lz = worldPos.SectionLocalZ;
        var previous = column.SetBlockState(lx, worldPos.Y, lz, newState, _registries.Blocks);
        if (previous != newState)
        {
            column.LastModifiedTick = tickNumber;
            LightingEngine.RefreshChunkLight(column, _registries.Blocks);
            _publishedSnapshots[key] = column.CreateSnapshot();
        }

        return true;
    }

    /// <inheritdoc />
    public async ValueTask<int> ProcessTicketsAsync(int maxChunksToLoad, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();

        if (maxChunksToLoad <= 0)
        {
            return 0;
        }

        var desiredDistances = ComputeDesiredChunksWithMinDistance();

        // 1. Unload chunks no longer covered by any active ticket.
        foreach (var (packedKey, existingChunk) in _loadedChunks)
        {
            if (!desiredDistances.ContainsKey(packedKey) && _loadedChunks.TryRemove(packedKey, out var removed))
            {
                _ = _publishedSnapshots.TryRemove(packedKey, out _);
                if (removed.IsDirty)
                {
                    await _saveQueue.Writer.WriteAsync(removed, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        // 2. Prioritize missing chunks by minimum Chebyshev distance to any ticket center, then ChunkPos order.
        var priorityQueue = new PriorityQueue<ChunkPos, (long Distance, ChunkPos Pos)>();
        foreach (var (packedKey, minDist) in desiredDistances)
        {
            if (!_loadedChunks.ContainsKey(packedKey))
            {
                var pos = ChunkPos.UnpackInt64(packedKey);
                priorityQueue.Enqueue(pos, (minDist, pos));
            }
        }

        var batchCount = Math.Min(maxChunksToLoad, priorityQueue.Count);
        if (batchCount == 0)
        {
            return 0;
        }

        var batchPositions = new ChunkPos[batchCount];
        for (var i = 0; i < batchCount; i++)
        {
            batchPositions[i] = priorityQueue.Dequeue();
        }

        var results = new ChunkColumn[batchCount];
        await Parallel.ForAsync(
            0,
            batchCount,
            cancellationToken,
            async (index, ct) =>
            {
                var pos = batchPositions[index];
                var loaded = await _storage.LoadChunkAsync(pos, ct).ConfigureAwait(false);
                if (loaded is null)
                {
                    loaded = new ChunkColumn(pos);
                    _generator.GenerateChunk(loaded);
                }

                results[index] = loaded;
            }).ConfigureAwait(false);

        // Deterministic commit ordered by ChunkPos.
        Array.Sort(results, static (a, b) => a.Position.CompareTo(b.Position));
        for (var i = 0; i < results.Length; i++)
        {
            var chunk = results[i];
            var key = chunk.Position.PackInt64();
            _loadedChunks[key] = chunk;
            _publishedSnapshots[key] = chunk.CreateSnapshot();
        }

        return results.Length;
    }

    /// <inheritdoc />
    public async ValueTask<int> SaveDirtyChunksAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();

        var saved = 0;
        foreach (var (_, chunk) in _loadedChunks)
        {
            if (chunk.IsDirty)
            {
                await _storage.SaveChunkAsync(chunk, cancellationToken).ConfigureAwait(false);
                saved++;
            }
        }

        return saved;
    }

    /// <inheritdoc />
    public ulong ComputeLoadedWorldHash()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        var chunks = _loadedChunks.Values
            .OrderBy(static c => c.Position)
            .ToArray();

        var hasher = Fnv1a64.Create();
        hasher.AppendInt32(chunks.Length);
        for (var i = 0; i < chunks.Length; i++)
        {
            hasher.AppendInt64(unchecked((long)chunks[i].ComputeDeterministicHash()));
        }

        return hasher.Value;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _saveQueue.Writer.TryComplete();
        await _saveWorkerTask.ConfigureAwait(false);
        await _backgroundCts.CancelAsync().ConfigureAwait(false);
        _backgroundCts.Dispose();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _saveQueue.Writer.TryComplete();
        _backgroundCts.Cancel();
        _backgroundCts.Dispose();
    }

    private Dictionary<long, long> ComputeDesiredChunksWithMinDistance()
    {
        var desired = new Dictionary<long, long>();
        foreach (var (_, ticket) in _tickets)
        {
            var r = ticket.Radius;
            for (var dz = -r; dz <= r; dz++)
            {
                for (var dx = -r; dx <= r; dx++)
                {
                    var pos = new ChunkPos(ticket.Center.X + dx, ticket.Center.Z + dz);
                    var key = pos.PackInt64();
                    var dist = ticket.Center.ChessboardDistance(pos);

                    if (!desired.TryGetValue(key, out var existing) || dist < existing)
                    {
                        desired[key] = dist;
                    }
                }
            }
        }

        return desired;
    }

    private async Task RunSaveWorkerAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var dirtyChunk in _saveQueue.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                await _storage.SaveChunkAsync(dirtyChunk, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Graceful background shutdown.
        }
    }
}
