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
    public const float MaxPlayerHealth = 20.0f;

    public long EntityId { get; } = entityId;

    public string Username { get; } = username;

    public Vec3d SpawnPosition { get; } = initialPosition;

    public ChunkPos CenterChunk { get; set; } = initialChunk;

    public Vec3d LastPosition { get; set; } = initialPosition;

    public float YawDegrees { get; set; }

    public float PitchDegrees { get; set; }

    public float Health { get; set; } = MaxPlayerHealth;

    public long LastHurtTick { get; set; } = -100L;

    public PermissionLevel Permission { get; set; } = initialPermission;

    public int NextTeleportId { get; set; } = 1;

    public HashSet<long> SentChunks { get; } = [];
}
