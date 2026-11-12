using Microsoft.Extensions.Logging;
using OverHorizon.Networking.Packets;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Rendering.Hud;

namespace OverHorizon.Client;

/// <summary>
/// Tracks active remote multiplayer avatars from serverbound/clientbound presence packets.
/// </summary>
internal sealed class ClientRemotePlayerTracker
{
    private readonly Dictionary<long, RemotePlayerAvatar> _avatars = [];
    private readonly List<RemotePlayerAvatar> _snapshot = [];
    private readonly ILogger _logger;

    public ClientRemotePlayerTracker(ILogger logger) => _logger = logger;

    public int Count => _avatars.Count;

    public bool TryApply(IClientboundPacket packet)
    {
        switch (packet)
        {
            case RemotePlayerSpawnPacket spawnPkt:
                _avatars[spawnPkt.EntityId] = new RemotePlayerAvatar(
                    spawnPkt.EntityId,
                    spawnPkt.Username,
                    spawnPkt.Position,
                    spawnPkt.YawDegrees,
                    spawnPkt.PitchDegrees,
                    WalkPhase: 0.0f);
                ClientLog.RemotePlayerJoined(_logger, spawnPkt.Username, spawnPkt.EntityId);
                return true;

            case RemotePlayerMovePacket movePkt when _avatars.TryGetValue(movePkt.EntityId, out var prev):
                var movedDist = (float)(movePkt.Position - prev.FeetPosition).Length;
                _avatars[movePkt.EntityId] = prev with
                {
                    FeetPosition = movePkt.Position,
                    YawDegrees = movePkt.YawDegrees,
                    PitchDegrees = movePkt.PitchDegrees,
                    WalkPhase = prev.WalkPhase + (movedDist * 2.8f),
                };
                return true;

            case RemotePlayerDespawnPacket despawnPkt:
                if (_avatars.Remove(despawnPkt.EntityId))
                {
                    ClientLog.RemotePlayerLeft(_logger, despawnPkt.EntityId);
                }

                return true;

            default:
                return false;
        }
    }

    public IReadOnlyList<RemotePlayerAvatar> BuildSnapshot()
    {
        _snapshot.Clear();
        foreach (var avatar in _avatars.Values)
        {
            _snapshot.Add(avatar);
        }

        return _snapshot;
    }
}
