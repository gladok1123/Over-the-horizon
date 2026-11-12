using System.Collections.Concurrent;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Networking.Packets.Serverbound;
using OverHorizon.Networking.Sessions;

namespace OverHorizon.Server;

/// <summary>
/// Server-authoritative PvP melee combat evaluator handling reach checks, 10-tick hurt immunity,
/// weapon &amp; critical damage scaling, knockback vectors, death announcements, and respawn synchronization.
/// </summary>
internal static class ServerPvpCombatHandler
{
    private const double MaxPvpReachDistanceSquared = 5.0 * 5.0;
    private const long HurtImmunityTicks = 10L;
    private const double HorizontalKnockbackSpeed = 6.8;
    private const double VerticalKnockbackSpeed = 4.4;

    public static void HandleAttack(
        PlayerSessionInfo attackerSession,
        ConnectedPlayerState attackerState,
        PlayerAttackEntityPacket attack,
        long tickNumber,
        bool pvpEnabled,
        ConcurrentDictionary<long, ConnectedPlayerState> players,
        CoreRegistries registries,
        IGameplaySubsystem gameplay,
        INetworkBroadcaster broadcaster)
    {
        if (!pvpEnabled || attack.TargetEntityId == attackerState.EntityId)
        {
            return;
        }

        long? victimSessionId = null;
        ConnectedPlayerState? victimState = null;
        foreach (var (sid, candidate) in players)
        {
            if (candidate.EntityId == attack.TargetEntityId)
            {
                victimSessionId = sid;
                victimState = candidate;
                break;
            }
        }

        if (victimState is null || victimSessionId is not { } targetSessionId)
        {
            return;
        }

        if (tickNumber - victimState.LastHurtTick < HurtImmunityTicks)
        {
            return;
        }

        var delta = victimState.LastPosition - attackerState.LastPosition;
        if (delta.LengthSquared > MaxPvpReachDistanceSquared)
        {
            return;
        }

        var baseDamage = 1.0f;
        if (attack.HeldItemId > 0 && registries.Items.TryGetById(attack.HeldItemId, out var heldDef))
        {
            baseDamage = Math.Clamp(heldDef.AttackDamage, 1.0f, 16.0f);
        }

        if (attack.IsCriticalHit)
        {
            baseDamage *= 1.5f;
        }

        var horizLen = Math.Sqrt((delta.X * delta.X) + (delta.Z * delta.Z));
        double dirX;
        double dirZ;
        if (horizLen > 1e-4)
        {
            dirX = delta.X / horizLen;
            dirZ = delta.Z / horizLen;
        }
        else
        {
            var yawRad = attackerState.YawDegrees * (Math.PI / 180.0);
            dirX = Math.Sin(yawRad);
            dirZ = -Math.Cos(yawRad);
        }

        var knockback = new Vec3d(
            dirX * HorizontalKnockbackSpeed,
            VerticalKnockbackSpeed,
            dirZ * HorizontalKnockbackSpeed);

        victimState.LastHurtTick = tickNumber;
        victimState.Health = Math.Max(0.0f, victimState.Health - baseDamage);
        var died = victimState.Health <= 0.01f;

        broadcaster.BroadcastToPlaySessions(new EntityCombatEventPacket(
            victimState.EntityId,
            attackerState.EntityId,
            died ? ConnectedPlayerState.MaxPlayerHealth : victimState.Health,
            knockback,
            died));

        if (!died)
        {
            return;
        }

        var weaponLabel = (attack.HeldItemId > 0 && registries.Items.TryGetById(attack.HeldItemId, out var weapon))
            ? $" using [{weapon.EffectiveName}]"
            : string.Empty;
        broadcaster.BroadcastToPlaySessions(
            new SystemChatPacket($"[PvP] {victimState.Username} was slain by {attackerSession.Username}{weaponLabel}!"));

        victimState.Health = ConnectedPlayerState.MaxPlayerHealth;
        victimState.LastPosition = victimState.SpawnPosition;
        _ = gameplay.TrySetEntityKinematics(victimState.EntityId, victimState.SpawnPosition, Vec3d.Zero, onGround: true);

        _ = broadcaster.SendToSession(
            targetSessionId,
            new SynchronizePlayerPositionPacket(
                victimState.SpawnPosition,
                0.0f,
                0.0f,
                victimState.NextTeleportId++));

        foreach (var (peerSid, _) in players)
        {
            if (peerSid != targetSessionId)
            {
                _ = broadcaster.SendToSession(
                    peerSid,
                    new RemotePlayerMovePacket(victimState.EntityId, victimState.SpawnPosition, 0.0f, 0.0f));
            }
        }
    }
}
