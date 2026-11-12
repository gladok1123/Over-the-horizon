using Microsoft.Extensions.Logging;
using OverHorizon.Core.Math;
using OverHorizon.Networking.Packets;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Rendering.Hud;

namespace OverHorizon.Client;

/// <summary>
/// Tracks active remote multiplayer avatars with frame-rate-independent position, shortest-angle yaw/pitch,
/// and limb-swing interpolation between 20 TPS server movement packets.
/// </summary>
internal sealed class ClientRemotePlayerTracker
{
    private const double SnapTeleportDistanceSq = 144.0; // Snap immediately when > 12 blocks (e.g. /tp)
    private const double InterpolationSharpness = 16.0;

    private readonly Dictionary<long, InterpolatedPeerState> _peers = [];
    private readonly List<RemotePlayerAvatar> _snapshot = [];
    private readonly ILogger _logger;

    public ClientRemotePlayerTracker(ILogger logger) => _logger = logger;

    public int Count => _peers.Count;

    public bool TryApply(IClientboundPacket packet)
    {
        switch (packet)
        {
            case RemotePlayerSpawnPacket spawnPkt:
                _peers[spawnPkt.EntityId] = new InterpolatedPeerState(
                    spawnPkt.EntityId,
                    spawnPkt.Username,
                    spawnPkt.Position,
                    spawnPkt.Position,
                    spawnPkt.YawDegrees,
                    spawnPkt.YawDegrees,
                    spawnPkt.PitchDegrees,
                    spawnPkt.PitchDegrees,
                    WalkPhase: 0.0f,
                    HorizontalSpeed: 0.0f);
                ClientLog.RemotePlayerJoined(_logger, spawnPkt.Username, spawnPkt.EntityId);
                return true;

            case RemotePlayerMovePacket movePkt when _peers.TryGetValue(movePkt.EntityId, out var prev):
                var delta = movePkt.Position - prev.TargetPosition;
                var horizDist = MathF.Sqrt((float)((delta.X * delta.X) + (delta.Z * delta.Z)));
                var snap = (movePkt.Position - prev.RenderPosition).LengthSquared > SnapTeleportDistanceSq;

                _peers[movePkt.EntityId] = prev with
                {
                    RenderPosition = snap ? movePkt.Position : prev.RenderPosition,
                    TargetPosition = movePkt.Position,
                    RenderYaw = snap ? movePkt.YawDegrees : prev.RenderYaw,
                    TargetYaw = movePkt.YawDegrees,
                    RenderPitch = snap ? movePkt.PitchDegrees : prev.RenderPitch,
                    TargetPitch = movePkt.PitchDegrees,
                    HorizontalSpeed = snap ? 0.0f : Math.Clamp(horizDist * 20.0f, 0.0f, 8.0f),
                };
                return true;

            case RemotePlayerDespawnPacket despawnPkt:
                if (_peers.Remove(despawnPkt.EntityId))
                {
                    ClientLog.RemotePlayerLeft(_logger, despawnPkt.EntityId);
                }

                return true;

            default:
                return false;
        }
    }

    public void StepInterpolation(double deltaSeconds)
    {
        if (_peers.Count == 0)
        {
            return;
        }

        var dt = Math.Clamp(deltaSeconds, 0.0005, 0.1);
        var blend = 1.0 - Math.Exp(-InterpolationSharpness * dt);
        var blendF = (float)blend;

        var keys = new List<long>(_peers.Count);
        foreach (var k in _peers.Keys)
        {
            keys.Add(k);
        }

        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            var s = _peers[key];

            var nextPos = new Vec3d(
                s.RenderPosition.X + ((s.TargetPosition.X - s.RenderPosition.X) * blend),
                s.RenderPosition.Y + ((s.TargetPosition.Y - s.RenderPosition.Y) * blend),
                s.RenderPosition.Z + ((s.TargetPosition.Z - s.RenderPosition.Z) * blend));

            var movedStep = nextPos - s.RenderPosition;
            var stepHoriz = MathF.Sqrt((float)((movedStep.X * movedStep.X) + (movedStep.Z * movedStep.Z)));

            var nextYaw = LerpAngleDegrees(s.RenderYaw, s.TargetYaw, blendF);
            var nextPitch = s.RenderPitch + ((s.TargetPitch - s.RenderPitch) * blendF);
            var nextWalkPhase = stepHoriz > 1e-4f
                ? s.WalkPhase + (stepHoriz * 3.1f)
                : DecayPhaseToRest(s.WalkPhase, (float)dt * 10.0f);

            _peers[key] = s with
            {
                RenderPosition = nextPos,
                RenderYaw = nextYaw,
                RenderPitch = nextPitch,
                WalkPhase = nextWalkPhase,
                HorizontalSpeed = s.HorizontalSpeed * (float)Math.Exp(-8.0 * dt),
            };
        }
    }

    public IReadOnlyList<RemotePlayerAvatar> BuildSnapshot()
    {
        _snapshot.Clear();
        foreach (var s in _peers.Values)
        {
            _snapshot.Add(new RemotePlayerAvatar(
                s.EntityId,
                s.Username,
                s.RenderPosition,
                s.RenderYaw,
                s.RenderPitch,
                s.WalkPhase));
        }

        return _snapshot;
    }

    private static float LerpAngleDegrees(float current, float target, float t)
    {
        var diff = (target - current) % 360.0f;
        if (diff > 180.0f)
        {
            diff -= 360.0f;
        }
        else if (diff < -180.0f)
        {
            diff += 360.0f;
        }

        return (current + (diff * t)) % 360.0f;
    }

    private static float DecayPhaseToRest(float phase, float factor)
    {
        var mod = phase % MathF.PI;
        if (MathF.Abs(mod) < 0.02f)
        {
            return 0.0f;
        }

        return phase - (mod * MathF.Min(1.0f, factor));
    }

    private readonly record struct InterpolatedPeerState(
        long EntityId,
        string Username,
        Vec3d RenderPosition,
        Vec3d TargetPosition,
        float RenderYaw,
        float TargetYaw,
        float RenderPitch,
        float TargetPitch,
        float WalkPhase,
        float HorizontalSpeed);
}
