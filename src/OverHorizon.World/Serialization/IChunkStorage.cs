using OverHorizon.Core.Math;
using OverHorizon.World.Storage;

namespace OverHorizon.World.Serialization;

/// <summary>
/// Asynchronous persistence backend for loading and saving <see cref="ChunkColumn"/>s.
/// </summary>
public interface IChunkStorage
{
    /// <summary>
    /// Attempts to load a previously persisted chunk from storage.
    /// </summary>
    /// <param name="position">Chunk coordinate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Loaded <see cref="ChunkColumn"/>, or <see langword="null"/> if not yet generated.</returns>
    ValueTask<ChunkColumn?> LoadChunkAsync(ChunkPos position, CancellationToken cancellationToken);

    /// <summary>
    /// Persists a <see cref="ChunkColumn"/> to storage asynchronously.
    /// </summary>
    /// <param name="chunk">Chunk column to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Asynchronous save operation.</returns>
    ValueTask SaveChunkAsync(ChunkColumn chunk, CancellationToken cancellationToken);
}
