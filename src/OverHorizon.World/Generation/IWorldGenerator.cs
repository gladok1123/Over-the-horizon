using OverHorizon.World.Storage;

namespace OverHorizon.World.Generation;

/// <summary>
/// Thread-safe deterministic world chunk generator.
/// </summary>
public interface IWorldGenerator
{
    /// <summary>
    /// Gets the 64-bit deterministic seed of this generator.
    /// </summary>
    long WorldSeed { get; }

    /// <summary>
    /// Populates terrain, caves, surface layers, features, heightmaps, and initial lighting in <paramref name="chunk"/>.
    /// Safe to invoke concurrently across worker threads for distinct <see cref="ChunkColumn"/>s.
    /// </summary>
    /// <param name="chunk">Target chunk column to populate.</param>
    void GenerateChunk(ChunkColumn chunk);
}
