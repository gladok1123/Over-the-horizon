using Microsoft.Extensions.Logging;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Networking.Packets;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Textures;
using OverHorizon.World.Lighting;

namespace OverHorizon.Client;

/// <summary>
/// Tracks active remote multiplayer avatars with frame-rate-independent position, shortest-angle yaw/pitch,
/// world sky/block lighting, 3D Armor, held Bow/Shield/Sword/Block attachments, Left/Right arm swing, and Spectator mode.
/// </summary>
internal sealed class ClientRemotePlayerTracker
{
    private const double SnapTeleportDistanceSq = 144.0;
    private const double InterpolationSharpness = 16.0;
    private const float SwingDuration = 0.24f;

    private readonly Dictionary<long, InterpolatedPeerState> _peers = [];
    private readonly List<RemotePlayerAvatar> _snapshot = [];
    private readonly ILogger _logger;

    public ClientRemotePlayerTracker(ILogger logger) => _logger = logger;

    public int Count => _peers.Count;

    public void Clear() => _peers.Clear();

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
                    HorizontalSpeed: 0.0f,
                    HurtFlash: 0.0f,
                    Health: 20.0f,
                    GameModeByte: 0,
                    MainHandItemId: 0,
                    OffhandItemId: 0,
                    Armor: default,
                    ActionFlags: 0,
                    BowChargeNorm: 0.0f,
                    LastSwingCounter: 0,
                    SwingTimer: 0.0f,
                    SwingIsOffhand: false,
                    RidingMinecartId: -1);
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

            case RemotePlayerEquipmentPacket eqPkt when _peers.TryGetValue(eqPkt.EntityId, out var prevEq):
                var armor = new PlayerArmorVisual(
                    HelmetTier: (byte)(eqPkt.PackedArmorLower & 0x0F),
                    ChestplateTier: (byte)((eqPkt.PackedArmorLower >> 4) & 0x0F),
                    LeggingsTier: (byte)(eqPkt.PackedArmorUpper & 0x0F),
                    BootsTier: (byte)((eqPkt.PackedArmorUpper >> 4) & 0x0F));
                var newSwing = eqPkt.SwingCounter != prevEq.LastSwingCounter;
                _peers[eqPkt.EntityId] = prevEq with
                {
                    GameModeByte = eqPkt.GameModeByte,
                    MainHandItemId = eqPkt.MainHandItemId,
                    OffhandItemId = eqPkt.OffhandItemId,
                    Armor = armor,
                    ActionFlags = eqPkt.ActionFlags,
                    BowChargeNorm = Math.Clamp(eqPkt.BowChargeByte / 255.0f, 0.0f, 1.0f),
                    LastSwingCounter = eqPkt.SwingCounter,
                    SwingTimer = newSwing ? SwingDuration : prevEq.SwingTimer,
                    SwingIsOffhand = newSwing ? (eqPkt.ActionFlags & 8) != 0 : prevEq.SwingIsOffhand,
                    RidingMinecartId = eqPkt.RidingMinecartId,
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

    public bool TryApplyCombatFeedback(long targetEntityId, float newHealth, bool wasRespawned, out Vec3d peerPos)
    {
        if (!_peers.TryGetValue(targetEntityId, out var peer))
        {
            peerPos = Vec3d.Zero;
            return false;
        }

        peerPos = peer.RenderPosition;
        _peers[targetEntityId] = peer with
        {
            HurtFlash = wasRespawned ? 0.0f : 1.0f,
            Health = wasRespawned ? 20.0f : Math.Clamp(newHealth, 0.0f, 20.0f),
        };
        return true;
    }

    public bool TryRaycastPeer(Vec3d eyePos, Vec3d forward, double maxReach, out long hitEntityId, out double hitDistance)
    {
        hitEntityId = 0;
        hitDistance = maxReach + 1.0;

        var dirLen = forward.Length;
        if (dirLen < 1e-6 || _peers.Count == 0) return false;

        var dir = new Vec3d(forward.X / dirLen, forward.Y / dirLen, forward.Z / dirLen);
        var found = false;

        foreach (var peer in _peers.Values)
        {
            // Spectator peers are intangible; Creative peers are immune on the server too!
            if (peer.GameModeByte == (byte)GameMode.Spectator) continue;

            var min = new Vec3d(peer.RenderPosition.X - 0.42, peer.RenderPosition.Y - 0.05, peer.RenderPosition.Z - 0.42);
            var max = new Vec3d(peer.RenderPosition.X + 0.42, peer.RenderPosition.Y + 1.90, peer.RenderPosition.Z + 0.42);

            if (IntersectRayAabb(eyePos, dir, min, max, maxReach, out var dist) && dist < hitDistance)
            {
                hitDistance = dist;
                hitEntityId = peer.EntityId;
                found = true;
            }
        }

        return found;
    }

    public void StepInterpolation(double deltaSeconds)
    {
        if (_peers.Count == 0) return;

        var dt = Math.Clamp(deltaSeconds, 0.0005, 0.1);
        var blend = 1.0 - Math.Exp(-InterpolationSharpness * dt);
        var blendF = (float)blend;

        var keys = new List<long>(_peers.Count);
        foreach (var k in _peers.Keys) keys.Add(k);

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
            var nextWalkPhase = (s.RidingMinecartId < 0 && stepHoriz > 1e-4f)
                ? s.WalkPhase + (stepHoriz * 3.1f)
                : DecayPhaseToRest(s.WalkPhase, (float)dt * 10.0f);
            var nextHurt = MathF.Max(0.0f, s.HurtFlash - ((float)dt * 2.4f));
            var nextSwing = MathF.Max(0.0f, s.SwingTimer - (float)dt);

            _peers[key] = s with
            {
                RenderPosition = nextPos,
                RenderYaw = nextYaw,
                RenderPitch = nextPitch,
                WalkPhase = nextWalkPhase,
                HorizontalSpeed = s.HorizontalSpeed * (float)Math.Exp(-8.0 * dt),
                HurtFlash = nextHurt,
                SwingTimer = nextSwing,
            };
        }
    }

    public IReadOnlyList<RemotePlayerAvatar> BuildSnapshot(
        ClientWorldCache? worldCache = null,
        float skyBrightness = 1.0f,
        Vec3d localCamPos = default,
        byte localHeldTorch = 0,
        IRegistry<ItemDefinition>? items = null,
        TextureAtlasData? atlas = null)
    {
        _snapshot.Clear();
        foreach (var p in _peers.Values)
        {
            var bright = 1.0f;
            if (worldCache is not null)
            {
                var samplePos = (p.RenderPosition + new Vec3d(0.0, 1.2, 0.0)).ToBlockPos();
                var (sky, blk) = worldCache.GetLight(samplePos);
                var effSky = (byte)Math.Clamp((int)MathF.Round(sky * skyBrightness), 0, 15);
                var dist = (float)p.RenderPosition.DistanceTo(localCamPos);
                var localTorch = localHeldTorch > dist ? (byte)Math.Clamp((int)MathF.Round(localHeldTorch - dist), 0, 15) : (byte)0;
                var peerHeldEmit = ResolvePeerHeldLight(p, items);
                bright = LightingEngine.LightLevelToBrightness(LightingEngine.ComputeEffectiveLightLevel(effSky, blk, Math.Max(localTorch, peerHeldEmit)));
            }

            var mainTile = -1;
            var mainBlockTile = -1;
            var mainIsBlock = false;
            var mainIsShield = false;
            var mainIsBow = false;
            if (items is not null && atlas is not null && p.MainHandItemId > 0 && p.MainHandItemId < items.Count)
            {
                var def = items.GetById(p.MainHandItemId);
                mainIsShield = def.Key == BuiltInItems.Shield;
                mainIsBow = def.Key == BuiltInItems.Bow;
                mainTile = atlas.GetItemTileIndex(def.Key);
                if (def.PlacedBlockKey is { } bk && bk != BuiltInBlocks.Torch && bk != BuiltInBlocks.RedstoneTorch && bk != BuiltInBlocks.Rail && bk != BuiltInBlocks.PoweredRail)
                {
                    mainIsBlock = true;
                    mainBlockTile = atlas.GetTileIndex(bk, Direction.East);
                }
            }

            var offTile = -1;
            var offIsShield = false;
            if (items is not null && atlas is not null && p.OffhandItemId > 0 && p.OffhandItemId < items.Count)
            {
                var offDef = items.GetById(p.OffhandItemId);
                offIsShield = offDef.Key == BuiltInItems.Shield;
                offTile = atlas.GetItemTileIndex(offDef.Key);
            }

            var swingProg = p.SwingTimer > 0.0f ? 1.0f - (p.SwingTimer / SwingDuration) : 0.0f;
            _snapshot.Add(new RemotePlayerAvatar(
                p.EntityId,
                p.Username,
                p.RenderPosition,
                p.RenderYaw,
                p.RenderPitch,
                p.WalkPhase,
                p.HurtFlash,
                p.Health)
            {
                LightBrightness = bright,
                Armor = p.Armor,
                MainHandSpriteTile = mainTile,
                MainHandIsShield = mainIsShield,
                MainHandIsBow = mainIsBow,
                MainHandIsBlock3D = mainIsBlock,
                MainHandBlockTile = mainBlockTile,
                OffhandIsShield = offIsShield,
                OffhandSpriteTile = offTile,
                IsBlockingShield = (p.ActionFlags & 1) != 0,
                IsDrawingBow = (p.ActionFlags & 2) != 0,
                BowCharge = p.BowChargeNorm,
                HandSwingProgress = swingProg,
                LeftHandSwing = p.SwingIsOffhand,
                IsSneaking = (p.ActionFlags & 16) != 0,
                IsSpectator = p.GameModeByte == (byte)GameMode.Spectator,
                IsRidingMinecart = p.RidingMinecartId >= 0,
            });
        }

        return _snapshot;
    }

    private static byte ResolvePeerHeldLight(in InterpolatedPeerState p, IRegistry<ItemDefinition>? items)
    {
        if (items is null) return 0;
        byte m = p.MainHandItemId > 0 && p.MainHandItemId < items.Count ? items.GetById(p.MainHandItemId).HeldLightEmission : (byte)0;
        byte o = p.OffhandItemId > 0 && p.OffhandItemId < items.Count ? items.GetById(p.OffhandItemId).HeldLightEmission : (byte)0;
        return Math.Max(m, o);
    }

    private static float LerpAngleDegrees(float current, float target, float t)
    {
        var diff = (target - current) % 360.0f;
        if (diff > 180.0f) diff -= 360.0f;
        else if (diff < -180.0f) diff += 360.0f;
        return current + (diff * t);
    }

    private static float DecayPhaseToRest(float phase, float step)
    {
        var wrapped = phase % MathF.PI;
        if (MathF.Abs(wrapped) <= step) return phase - wrapped;
        return wrapped > MathF.PI * 0.5f ? phase + step : phase - step;
    }

    private static bool IntersectRayAabb(Vec3d origin, Vec3d dir, Vec3d min, Vec3d max, double maxDist, out double tHit)
    {
        var tMin = 0.0;
        var tMax = maxDist;

        for (var axis = 0; axis < 3; axis++)
        {
            var (o, d, bMin, bMax) = axis switch
            {
                0 => (origin.X, dir.X, min.X, max.X),
                1 => (origin.Y, dir.Y, min.Y, max.Y),
                _ => (origin.Z, dir.Z, min.Z, max.Z),
            };

            if (Math.Abs(d) < 1e-8)
            {
                if (o < bMin || o > bMax)
                {
                    tHit = 0;
                    return false;
                }
            }
            else
            {
                var inv = 1.0 / d;
                var t1 = (bMin - o) * inv;
                var t2 = (bMax - o) * inv;
                if (t1 > t2) (t1, t2) = (t2, t1);
                tMin = Math.Max(tMin, t1);
                tMax = Math.Min(tMax, t2);
                if (tMin > tMax)
                {
                    tHit = 0;
                    return false;
                }
            }
        }

        tHit = tMin;
        return true;
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
        float HorizontalSpeed,
        float HurtFlash,
        float Health,
        byte GameModeByte,
        ushort MainHandItemId,
        ushort OffhandItemId,
        PlayerArmorVisual Armor,
        byte ActionFlags,
        float BowChargeNorm,
        byte LastSwingCounter,
        float SwingTimer,
        bool SwingIsOffhand,
        int RidingMinecartId);
}
