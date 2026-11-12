using OverHorizon.Core.Math;
using OverHorizon.Modding.Commands;

namespace OverHorizon.Server;

internal sealed class ConnectedPlayerState(
    long entityId,
    string username,
    ChunkPos initialChunk,
    PermissionLevel initialPermission)
{
    public long EntityId { get; } = entityId;

    public string Username { get; } = username;

    public ChunkPos CenterChunk { get; set; } = initialChunk;

    public PermissionLevel Permission { get; set; } = initialPermission;

    public int NextTeleportId { get; set; } = 1;

    public HashSet<long> SentChunks { get; } = [];
}
