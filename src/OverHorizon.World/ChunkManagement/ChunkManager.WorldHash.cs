using System.Threading;
using OverHorizon.Shared.Hashing;

namespace OverHorizon.World.ChunkManagement;

/// <summary>
/// Deterministic world-hash helpers for <see cref="ChunkManager"/> (split out so the manager itself
/// stays under the 500-line architecture budget).
/// </summary>
public sealed partial class ChunkManager
{
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
}
