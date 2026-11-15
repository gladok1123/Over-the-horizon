using System.Collections.Concurrent;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.World.Storage;

namespace OverHorizon.World.Serialization;

/// <summary>
/// Lock-free in-memory NBT chunk storage for headless servers, tests, and benchmarks.
/// </summary>
public sealed class InMemoryChunkStorage : IChunkStorage
{
    private readonly CoreRegistries _registries;
    private readonly ConcurrentDictionary<long, byte[]> _blobs = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryChunkStorage"/> class.
    /// </summary>
    /// <param name="registries">Frozen domain registries.</param>
    public InMemoryChunkStorage(CoreRegistries registries)
    {
        ArgumentNullException.ThrowIfNull(registries);
        _registries = registries;
    }

    /// <summary>Gets the number of persisted chunks currently stored in memory.</summary>
    public int StoredChunkCount => _blobs.Count;

    /// <inheritdoc />
    public ValueTask<ChunkColumn?> LoadChunkAsync(ChunkPos position, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_blobs.TryGetValue(position.PackInt64(), out var bytes))
        {
            var chunk = ChunkNbtSerializer.Deserialize(bytes, _registries);
            return ValueTask.FromResult<ChunkColumn?>(chunk);
        }

        return ValueTask.FromResult<ChunkColumn?>(null);
    }

    /// <inheritdoc />
    public ValueTask SaveChunkAsync(ChunkColumn chunk, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        cancellationToken.ThrowIfCancellationRequested();

        var bytes = ChunkNbtSerializer.SerializeToBytes(chunk, _registries);
        _blobs[chunk.Position.PackInt64()] = bytes;
        chunk.MarkClean();
        return ValueTask.CompletedTask;
    }
}
