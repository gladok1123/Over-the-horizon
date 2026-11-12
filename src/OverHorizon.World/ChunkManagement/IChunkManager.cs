using System.Diagnostics.CodeAnalysis;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.World.Storage;

namespace OverHorizon.World.ChunkManagement;

/// <summary>
/// Manages chunk tickets, distance-prioritized parallel loading/worldgen, Copy-on-Write snapshots, and async persistence.
/// </summary>
public interface IChunkManager
{
    /// <summary>Gets the number of currently loaded chunks.</summary>
    int LoadedChunkCount { get; }

    /// <summary>
    /// Adds or updates a <see cref="ChunkTicket"/> for <see cref="ChunkTicket.OwnerId"/>.
    /// </summary>
    /// <param name="ticket">Ticket specification.</param>
    void UpsertTicket(ChunkTicket ticket);

    /// <summary>
    /// Removes the ticket associated with <paramref name="ownerId"/>.
    /// </summary>
    /// <param name="ownerId">Ticket owner identifier.</param>
    /// <returns><see langword="true"/> if a ticket was removed.</returns>
    bool RemoveTicket(long ownerId);

    /// <summary>
    /// Attempts to retrieve an immutable <see cref="ChunkSnapshot"/> for lock-free concurrent reading.
    /// </summary>
    /// <param name="position">Chunk coordinate.</param>
    /// <param name="snapshot">Published snapshot when loaded.</param>
    /// <returns><see langword="true"/> if the chunk is loaded.</returns>
    bool TryGetSnapshot(ChunkPos position, [MaybeNullWhen(false)] out ChunkSnapshot snapshot);

    /// <summary>
    /// Applies a block modification to a loaded chunk on the main thread, refreshing heightmaps, light, and snapshot.
    /// </summary>
    /// <param name="worldPos">World block position.</param>
    /// <param name="newState">New block state.</param>
    /// <param name="tickNumber">Current simulation tick.</param>
    /// <returns><see langword="true"/> if the target chunk was loaded and updated.</returns>
    bool SetBlockState(BlockPos worldPos, BlockState newState, long tickNumber = 0);

    /// <summary>
    /// Evaluates active tickets, loads or generates up to <paramref name="maxChunksToLoad"/> highest-priority chunks in parallel,
    /// and unloads unticketed chunks (saving dirty ones).
    /// </summary>
    /// <param name="maxChunksToLoad">Maximum chunks to load/generate in this step.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of newly loaded or generated chunks.</returns>
    ValueTask<int> ProcessTicketsAsync(int maxChunksToLoad, CancellationToken cancellationToken);

    /// <summary>
    /// Asynchronously flushes all dirty loaded chunks to storage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of saved chunks.</returns>
    ValueTask<int> SaveDirtyChunksAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Computes a deterministic 64-bit FNV-1a hash across all currently loaded chunks ordered by <see cref="ChunkPos"/>.
    /// </summary>
    /// <returns>Deterministic 64-bit world chunk hash.</returns>
    ulong ComputeLoadedWorldHash();
}
