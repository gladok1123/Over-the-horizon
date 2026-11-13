using System.Collections.Concurrent;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Mobs;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Networking.Packets.Serverbound;
using OverHorizon.Networking.Sessions;

namespace OverHorizon.Server;

/// <summary>
/// Server-authoritative PvP melee combat evaluator handling Creative/Spectator immunity, frontal Shield deflection,
/// armor mitigation, reach checks, 10-tick hurt immunity, vanilla knockback vectors, and respawn synchronization.
/// </summary>
internal static class ServerPvpCombatHandler
{
    private const double MaxPvpReachDistanceSquared = 5.2 * 5.2;
    private const long HurtImmunityTicks = 10L;
    private const double HorizontalKnockbackSpeed = 8.8;
    private const double SprintKnockbackSpeed = 12.2;
    private const double VerticalKnockbackSpeed = 4.6;

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
        if (!pvpEnabled ||
            attack.TargetEntityId == attackerState.EntityId ||
            attackerState.GameMode == GameMode.Spectator)
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

        // Creative and Spectator players are strictly immune to PvP damage and death!
        if (victimState.IsInvulnerable)
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

        // Frontal Shield block deflects melee attacks completely!
        if (WorldMobManager.IsFrontalShieldBlock(victimState.IsBlockingShield, victimState.LookForward, victimState.LastPosition, attackerState.LastPosition))
        {
            victimState.LastHurtTick = tickNumber - (HurtImmunityTicks / 2);
            broadcaster.BroadcastToPlaySessions(new InventoryGrantAndEventPacket(0, 0, EventKind: 2, victimState.LastPosition));
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

        baseDamage *= victimState.GetArmorDamageMultiplier();

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

        var kbSpeed = attackerState.IsSprinting ? SprintKnockbackSpeed : HorizontalKnockbackSpeed;
        var knockback = new Vec3d(
            dirX * kbSpeed,
            VerticalKnockbackSpeed,
            dirZ * kbSpeed);

        ApplyPlayerDamageAndCheckDeath(
            victimState,
            targetSessionId,
            attackerState.EntityId,
            baseDamage,
            knockback,
            tickNumber,
            $"[PvP] {victimState.Username} was slain by {attackerSession.Username}" +
            ((attack.HeldItemId > 0 && registries.Items.TryGetById(attack.HeldItemId, out var weapon))
                ? $" using [{weapon.EffectiveName}]!"
                : "!"),
            gameplay,
            broadcaster);
    }

    public static void ApplyPlayerDamageAndCheckDeath(
        ConnectedPlayerState victimState,
        long targetSessionId,
        long attackerEntityId,
        float damage,
        Vec3d knockback,
        long tickNumber,
        string deathAnnouncement,
        IGameplaySubsystem gameplay,
        INetworkBroadcaster broadcaster)
    {
        if (victimState.IsInvulnerable || damage <= 0.0f)
        {
            return;
        }

        victimState.LastHurtTick = tickNumber;
        victimState.Health = Math.Max(0.0f, victimState.Health - damage);
        var died = victimState.Health <= 0.01f;

        broadcaster.BroadcastToPlaySessions(new EntityCombatEventPacket(
            victimState.EntityId,
            attackerEntityId,
            died ? ConnectedPlayerState.MaxPlayerHealth : victimState.Health,
            knockback,
            died));

        if (!died)
        {
            return;
        }

        broadcaster.BroadcastToPlaySessions(new SystemChatPacket(deathAnnouncement));

        victimState.RidingMinecartId = 0;
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
        broadcaster.BroadcastToPlaySessions(new RemotePlayerMovePacket(
            victimState.EntityId,
            victimState.SpawnPosition,
            0.0f,
            0.0f));
    }
}
