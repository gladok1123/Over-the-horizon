using OverHorizon.Core.Math;
using OverHorizon.Modding.Commands;

namespace OverHorizon.Server;

internal sealed class ConnectedPlayerState(
    long entityId,
    string username,
    ChunkPos initialChunk,
    Vec3d initialPosition,
    PermissionLevel initialPermission)
{
    public long EntityId { get; } = entityId;

    public string Username { get; } = username;

    public ChunkPos CenterChunk { get; set; } = initialChunk;

    public Vec3d LastPosition { get; set; } = initialPosition;

    public float YawDegrees { get; set; }

    public float PitchDegrees { get; set; }

    public PermissionLevel Permission { get; set; } = initialPermission;

    public int NextTeleportId { get; set; } = 1;

    public HashSet<long> SentChunks { get; } = [];
}
