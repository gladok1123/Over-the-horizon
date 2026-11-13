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
/// Horizon-style lock-free chunk manager with concentric-ring distance-prioritized parallel worldgen
/// (scaling cleanly up to 64-chunk view distances without full-grid allocations),
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

        _saveQueue = Channel.CreateBounded<ChunkColumn>(new BoundedChannelOptions(2048)
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
            RefreshColumnAndBorderNeighbors(chunkPos, column);
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

        var activeTickets = _tickets.Values.ToArray();

        // 1. Unload chunks no longer covered by any active ticket (zero 16,641-entry HashMap allocation!).
        foreach (var (packedKey, _) in _loadedChunks)
        {
            var pos = ChunkPos.UnpackInt64(packedKey);
            if (!IsCoveredByAnyTicket(pos, activeTickets) && _loadedChunks.TryRemove(packedKey, out var removed))
            {
                _ = _publishedSnapshots.TryRemove(packedKey, out _);
                if (removed.IsDirty)
                {
                    await _saveQueue.Writer.WriteAsync(removed, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        // 2. Concentric Chebyshev ring expansion (stops as soon as maxChunksToLoad missing chunks are found!).
        var batchPositions = SelectClosestMissingChunks(activeTickets, maxChunksToLoad);
        if (batchPositions.Length == 0)
        {
            return 0;
        }

        var results = new ChunkColumn[batchPositions.Length];
        await Parallel.ForAsync(
            0,
            batchPositions.Length,
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

        // Deterministic commit ordered by ChunkPos, including cross-chunk border light propagation!
        Array.Sort(results, static (a, b) => a.Position.CompareTo(b.Position));
        for (var i = 0; i < results.Length; i++)
        {
            var chunk = results[i];
            var key = chunk.Position.PackInt64();
            _loadedChunks[key] = chunk;
            _publishedSnapshots[key] = chunk.CreateSnapshot();
        }

        for (var i = 0; i < results.Length; i++)
        {
            var cp = results[i].Position;
            _ = _loadedChunks.TryGetValue(new ChunkPos(cp.X, cp.Z - 1).PackInt64(), out var n);
            _ = _loadedChunks.TryGetValue(new ChunkPos(cp.X, cp.Z + 1).PackInt64(), out var s);
            _ = _loadedChunks.TryGetValue(new ChunkPos(cp.X - 1, cp.Z).PackInt64(), out var w);
            _ = _loadedChunks.TryGetValue(new ChunkPos(cp.X + 1, cp.Z).PackInt64(), out var e);
            if (LightingEngine.PropagateCrossChunkLight(results[i], n, s, w, e, _registries.Blocks))
            {
                _publishedSnapshots[cp.PackInt64()] = results[i].CreateSnapshot();
            }
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

    private static bool IsCoveredByAnyTicket(ChunkPos pos, ReadOnlySpan<ChunkTicket> tickets)
    {
        for (var i = 0; i < tickets.Length; i++)
        {
            if (tickets[i].Center.ChessboardDistance(pos) <= tickets[i].Radius)
            {
                return true;
            }
        }

        return false;
    }

    private ChunkPos[] SelectClosestMissingChunks(ReadOnlySpan<ChunkTicket> tickets, int maxCount)
    {
        if (tickets.IsEmpty || maxCount <= 0)
        {
            return [];
        }

        var maxRadius = 0;
        for (var i = 0; i < tickets.Length; i++)
        {
            if (tickets[i].Radius > maxRadius)
            {
                maxRadius = tickets[i].Radius;
            }
        }

        var selected = new List<ChunkPos>(maxCount);
        var seen = new HashSet<long>(maxCount * 2);
        var ringScratch = new List<ChunkPos>(64);

        for (var ring = 0; ring <= maxRadius && selected.Count < maxCount; ring++)
        {
            ringScratch.Clear();
            for (var t = 0; t < tickets.Length; t++)
            {
                var ticket = tickets[t];
                if (ring > ticket.Radius)
                {
                    continue;
                }

                if (ring == 0)
                {
                    TryAddRingCandidate(ticket.Center, ring, tickets, seen, ringScratch);
                    continue;
                }

                for (var dx = -ring; dx <= ring; dx++)
                {
                    TryAddRingCandidate(new ChunkPos(ticket.Center.X + dx, ticket.Center.Z - ring), ring, tickets, seen, ringScratch);
                    TryAddRingCandidate(new ChunkPos(ticket.Center.X + dx, ticket.Center.Z + ring), ring, tickets, seen, ringScratch);
                }

                for (var dz = -ring + 1; dz <= ring - 1; dz++)
                {
                    TryAddRingCandidate(new ChunkPos(ticket.Center.X - ring, ticket.Center.Z + dz), ring, tickets, seen, ringScratch);
                    TryAddRingCandidate(new ChunkPos(ticket.Center.X + ring, ticket.Center.Z + dz), ring, tickets, seen, ringScratch);
                }
            }

            if (ringScratch.Count == 0)
            {
                continue;
            }

            ringScratch.Sort();
            for (var i = 0; i < ringScratch.Count && selected.Count < maxCount; i++)
            {
                selected.Add(ringScratch[i]);
            }
        }

        return selected.ToArray();
    }

    private void TryAddRingCandidate(
        ChunkPos pos,
        int currentRing,
        ReadOnlySpan<ChunkTicket> tickets,
        HashSet<long> seen,
        List<ChunkPos> ringScratch)
    {
        var key = pos.PackInt64();
        if (_loadedChunks.ContainsKey(key) || seen.Contains(key))
        {
            return;
        }

        // Ensure minimum Chebyshev distance across all tickets equals currentRing
        for (var i = 0; i < tickets.Length; i++)
        {
            if (tickets[i].Center.ChessboardDistance(pos) < currentRing && tickets[i].Center.ChessboardDistance(pos) <= tickets[i].Radius)
            {
                return;
            }
        }

        _ = seen.Add(key);
        ringScratch.Add(pos);
    }

    private void RefreshColumnAndBorderNeighbors(ChunkPos centerPos, ChunkColumn center)
    {
        _ = _loadedChunks.TryGetValue(new ChunkPos(centerPos.X, centerPos.Z - 1).PackInt64(), out var north);
        _ = _loadedChunks.TryGetValue(new ChunkPos(centerPos.X, centerPos.Z + 1).PackInt64(), out var south);
        _ = _loadedChunks.TryGetValue(new ChunkPos(centerPos.X - 1, centerPos.Z).PackInt64(), out var west);
        _ = _loadedChunks.TryGetValue(new ChunkPos(centerPos.X + 1, centerPos.Z).PackInt64(), out var east);

        LightingEngine.RefreshChunkLight(center, _registries.Blocks);
        _ = LightingEngine.PropagateCrossChunkLight(center, north, south, west, east, _registries.Blocks);
        _publishedSnapshots[centerPos.PackInt64()] = center.CreateSnapshot();

        PropagateIntoNeighbor(north, centerPos.X, centerPos.Z - 1);
        PropagateIntoNeighbor(south, centerPos.X, centerPos.Z + 1);
        PropagateIntoNeighbor(west, centerPos.X - 1, centerPos.Z);
        PropagateIntoNeighbor(east, centerPos.X + 1, centerPos.Z);
    }

    private void PropagateIntoNeighbor(ChunkColumn? neighbor, int nx, int nz)
    {
        if (neighbor is null)
        {
            return;
        }

        _ = _loadedChunks.TryGetValue(new ChunkPos(nx, nz - 1).PackInt64(), out var nn);
        _ = _loadedChunks.TryGetValue(new ChunkPos(nx, nz + 1).PackInt64(), out var ns);
        _ = _loadedChunks.TryGetValue(new ChunkPos(nx - 1, nz).PackInt64(), out var nw);
        _ = _loadedChunks.TryGetValue(new ChunkPos(nx + 1, nz).PackInt64(), out var ne);

        LightingEngine.RefreshChunkLight(neighbor, _registries.Blocks);
        _ = LightingEngine.PropagateCrossChunkLight(neighbor, nn, ns, nw, ne, _registries.Blocks);
        _publishedSnapshots[new ChunkPos(nx, nz).PackInt64()] = neighbor.CreateSnapshot();
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
