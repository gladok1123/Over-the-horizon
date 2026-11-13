using System.Numerics;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Mobs;
using OverHorizon.Gameplay.Redstone;
using OverHorizon.Gameplay.Survival;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Networking.Packets.Serverbound;
using OverHorizon.Networking.Sessions;

namespace OverHorizon.Server;

/// <summary>
/// Entity stepping, Minecart riding, Mob/Arrow combat, Void damage, and snapshot helpers for <see cref="ServerPlaySessionBridge"/>.
/// </summary>
public sealed partial class ServerPlaySessionBridge
{
    private void StepMultiplayerMinecarts(double dtSeconds, INetworkBroadcaster broadcaster)
    {
        if (_minecarts.ActiveMinecarts.Count == 0) return;

        long activeRiderEntityId = 0;
        var riderSteer = 0.0f;
        var riderLook = Vector3.UnitZ;
        foreach (var (_, pl) in _players)
        {
            if (pl.RidingMinecartId != 0)
            {
                activeRiderEntityId = pl.EntityId;
                riderSteer = pl.RiderForwardSteer;
                var yawRad = pl.YawDegrees * (MathF.PI / 180.0f);
                riderLook = new Vector3(MathF.Sin(yawRad), 0.0f, -MathF.Cos(yawRad));
                break;
            }
        }

        _ = _minecarts.Step(dtSeconds, _world.GetBlockState, activeRiderEntityId, riderSteer, riderLook);
        foreach (var (sid, pl) in _players)
        {
            if (pl.RidingMinecartId == 0) continue;
            var cart = FindCartById(pl.RidingMinecartId);
            if (cart is null || !cart.HasPlayerRider)
            {
                pl.RidingMinecartId = 0;
                continue;
            }

            var seatPos = new Vec3d(cart.FeetPosition.X, cart.FeetPosition.Y + 0.18, cart.FeetPosition.Z);
            pl.LastPosition = seatPos;
            _ = _gameplay.TrySetEntityKinematics(pl.EntityId, seatPos, cart.Velocity, onGround: true);
            BroadcastToPeers(sid, broadcaster, new RemotePlayerMovePacket(pl.EntityId, seatPos, pl.YawDegrees, pl.PitchDegrees));
        }
    }

    private void StepMultiplayerMobsAndArrows(
        double dtSeconds,
        long tickNumber,
        List<(BlockPos Position, ushort BlockStateId)> blockChanges,
        INetworkBroadcaster broadcaster)
    {
        ConnectedPlayerState? primaryTarget = null;
        foreach (var (_, pl) in _players)
        {
            if (pl.GameMode != GameMode.Spectator)
            {
                primaryTarget = pl;
                break;
            }
        }

        if (primaryTarget is not null)
        {
            var isSurvival = primaryTarget.GameMode == GameMode.Survival;
            var mobStep = _mobs.Step(
                dtSeconds,
                _world.GetBlockState,
                primaryTarget.LastPosition,
                WorldTime.DaylightFactor,
                isSurvival,
                _arrows,
                new List<Vec3d>(),
                out var creeperBlastPos,
                out var fuseHissed,
                primaryTarget.IsBlockingShield,
                primaryTarget.YawDegrees);

            for (var i = 0; i < fuseHissed.Count; i++)
            {
                broadcaster.BroadcastToPlaySessions(new InventoryGrantAndEventPacket(0, 0, 3, fuseHissed[i]));
            }

            if (creeperBlastPos is { } blastCenter)
            {
                var broken = new List<(BlockPos Pos, BlockState State)>();
                PrimedTntAndWorldTicker.ApplySphereExplosion(
                    _registries.Blocks,
                    _world.GetBlockState,
                    (pos, st) =>
                    {
                        if (!_world.SetBlockState(pos, st, tickNumber)) return false;
                        blockChanges.Add((pos, st.Id));
                        _redstone.NotifyBlockChanged(pos);
                        return true;
                    },
                    blastCenter,
                    radius: 3.1,
                    broken);
                broadcaster.BroadcastToPlaySessions(new InventoryGrantAndEventPacket(0, 0, 1, blastCenter));
            }

            if (mobStep.ShieldBlockedDamage)
            {
                broadcaster.BroadcastToPlaySessions(new InventoryGrantAndEventPacket(0, 0, 2, primaryTarget.LastPosition));
            }
            else if (mobStep.DamageToPlayer > 0.01f && FindSessionId(primaryTarget.EntityId) is { } targetSid)
            {
                var scaled = mobStep.DamageToPlayer * primaryTarget.GetArmorDamageMultiplier();
                ServerPvpCombatHandler.ApplyPlayerDamageAndCheckDeath(
                    primaryTarget, targetSid, 0, scaled, mobStep.KnockbackToPlayer, tickNumber,
                    $"{primaryTarget.Username} was slain by a monster!", _gameplay, broadcaster);
            }
        }

        if (_arrows.ActiveArrows.Count == 0) return;

        var arrowTargetPos = primaryTarget?.LastPosition ?? new Vec3d(0, -9999, 0);
        var canHitPlayer = primaryTarget is { IsInvulnerable: false };
        var arrowRes = _arrows.Step(
            dtSeconds,
            _world.GetBlockState,
            _mobs,
            arrowTargetPos,
            canHitPlayer,
            primaryTarget?.EntityId ?? 0,
            primaryTarget?.IsBlockingShield ?? false,
            primaryTarget?.YawDegrees ?? 0.0f);

        for (var i = 0; i < arrowRes.LootDrops.Count; i++)
        {
            _ = _droppedItems.SpawnDrop(arrowRes.LootDrops[i].Loot, arrowRes.LootDrops[i].Pos);
        }

        if (arrowRes.ShieldDeflected && primaryTarget is not null)
        {
            broadcaster.BroadcastToPlaySessions(new InventoryGrantAndEventPacket(0, 0, 2, primaryTarget.LastPosition));
        }
        else if (arrowRes.DamageToPlayer > 0.01f && primaryTarget is not null && FindSessionId(primaryTarget.EntityId) is { } hitSid)
        {
            var scaled = arrowRes.DamageToPlayer * primaryTarget.GetArmorDamageMultiplier();
            ServerPvpCombatHandler.ApplyPlayerDamageAndCheckDeath(
                primaryTarget, hitSid, 0, scaled, arrowRes.KnockbackToPlayer, tickNumber,
                $"{primaryTarget.Username} was shot by an arrow!", _gameplay, broadcaster);
        }
    }

    private void StepDroppedItemsAndVoidDamage(double dtSeconds, long tickNumber, INetworkBroadcaster broadcaster)
    {
        var collectors = new List<(long OwnerKey, Vec3d FeetPos)>(_players.Count);
        foreach (var (sid, pl) in _players)
        {
            if (pl.GameMode != GameMode.Spectator)
            {
                collectors.Add((sid, pl.LastPosition));
            }

            if (pl.GameMode == GameMode.Survival &&
                pl.LastPosition.Y < EngineConstants.WorldMinY - 12.0 &&
                tickNumber - pl.LastVoidDamageTick >= 10L)
            {
                pl.LastVoidDamageTick = tickNumber;
                ServerPvpCombatHandler.ApplyPlayerDamageAndCheckDeath(
                    pl, sid, 0, 4.0f, Vec3d.Zero, tickNumber,
                    $"{pl.Username} fell out of the world!", _gameplay, broadcaster);
            }
        }

        if (_droppedItems.ActiveDrops.Count > 0)
        {
            var collected = new List<(long OwnerKey, ItemStack Stack, Vec3d Position)>();
            _droppedItems.StepMultiTarget(dtSeconds, _world.GetBlockState, _registries.Blocks, collectors, collected);
            for (var i = 0; i < collected.Count; i++)
            {
                var c = collected[i];
                _ = broadcaster.SendToSession(c.OwnerKey, new InventoryGrantAndEventPacket(c.Stack.ItemId, c.Stack.Count, 0, c.Position));
            }
        }
    }

    private void ApplyExplosionDamageToPlayers(Vec3d center, long tickNumber, INetworkBroadcaster broadcaster)
    {
        foreach (var (sid, pl) in _players)
        {
            if (pl.IsInvulnerable) continue;
            var dist = pl.LastPosition.DistanceTo(center);
            if (dist >= 6.5) continue;

            if (pl.IsBlockingShield && WorldMobManager.IsFrontalShieldBlock(pl.LastPosition, pl.YawDegrees, center))
            {
                broadcaster.BroadcastToPlaySessions(new InventoryGrantAndEventPacket(0, 0, 2, pl.LastPosition));
                continue;
            }

            var falloff = (float)(1.0 - (dist / 6.5));
            var dmg = MathF.Round(((falloff * falloff) + falloff) * 16.0f * pl.GetArmorDamageMultiplier());
            var dir = pl.LastPosition - center;
            var len = Math.Max(0.2, dir.Length);
            var kb = new Vec3d((dir.X / len) * 9.0 * falloff, 5.2 * falloff, (dir.Z / len) * 9.0 * falloff);
            ServerPvpCombatHandler.ApplyPlayerDamageAndCheckDeath(
                pl, sid, 0, dmg, kb, tickNumber, $"{pl.Username} blew up!", _gameplay, broadcaster);
        }
    }

    private void HandleMountOrDismountMinecart(long sessionId, ConnectedPlayerState state, PlayerWorldInteractionPacket p, INetworkBroadcaster broadcaster)
    {
        if (p.AuxValue != 0)
        {
            DismountPlayerIfRiding(state);
            BroadcastToPeers(sessionId, broadcaster, state.ToEquipmentPacket());
            return;
        }

        var rayDir = new Vector3((float)p.DirectionOrTarget.X, (float)p.DirectionOrTarget.Y, (float)p.DirectionOrTarget.Z);
        var cart = _minecarts.TryMountAlongRay(p.Origin, rayDir, 5.5);
        if (cart is not null)
        {
            DismountPlayerIfRiding(state);
            cart.HasPlayerRider = true;
            cart.RiderEntityId = state.EntityId;
            state.RidingMinecartId = cart.Id;
            BroadcastToPeers(sessionId, broadcaster, state.ToEquipmentPacket());
        }
    }

    private void HandleComponentInteraction(long sessionId, ConnectedPlayerState state, PlayerWorldInteractionPacket p, long tickNumber, INetworkBroadcaster broadcaster)
    {
        var pos = p.DirectionOrTarget.ToBlockPos();
        var cur = _world.GetBlockState(pos);
        if (cur.IsAir) return;

        var changed = new List<(BlockPos Position, ushort BlockStateId)>();
        var ignited = new List<BlockPos>();
        var sounds = new List<(BlockPos Pos, RedstoneSoundKind Sound)>();
        if (_redstone.TryInteract(_world.GetBlockState, (bp, st) =>
            {
                if (_world.SetBlockState(bp, st, tickNumber)) changed.Add((bp, st.Id));
            }, pos, ignited, sounds))
        {
            for (var i = 0; i < ignited.Count; i++)
            {
                if (_tntAndWorld.IgniteBlock(_world.GetBlockState, (bp, st) => _world.SetBlockState(bp, st, tickNumber), ignited[i]))
                {
                    changed.Add((ignited[i], BlockState.Air.Id));
                }
            }

            ServerWorldTimeAndBlockSync.BroadcastBlockChanges(changed, broadcaster);
            return;
        }

        _registries.Items.TryGetById((ushort)Math.Clamp(p.AuxValue, 0, ushort.MaxValue), out var heldItem);
        if (ComposterAndTrapdoorMechanics.TryInteract(
                _registries, _world.GetBlockState,
                (bp, st) =>
                {
                    if (!_world.SetBlockState(bp, st, tickNumber)) return false;
                    changed.Add((bp, st.Id));
                    return true;
                },
                pos, heldItem, out _, out var spawnedBoneMeal, out _))
        {
            if (spawnedBoneMeal && _registries.Items.TryGet(BuiltInItems.BoneMeal, out var bm))
            {
                _ = _droppedItems.SpawnDrop(new ItemStack(bm.NumericId, 1), new Vec3d(pos.X + 0.5, pos.Y + 1.05, pos.Z + 0.5), pickupDelay: 0.15f);
            }

            ServerWorldTimeAndBlockSync.BroadcastBlockChanges(changed, broadcaster);
            return;
        }

        if (heldItem?.Key == BuiltInItems.FlintAndSteel &&
            _tntAndWorld.IgniteBlock(_world.GetBlockState, (bp, st) => _world.SetBlockState(bp, st, tickNumber), pos))
        {
            changed.Add((pos, BlockState.Air.Id));
            broadcaster.BroadcastToPlaySessions(new InventoryGrantAndEventPacket(0, 0, 3, new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5)));
            ServerWorldTimeAndBlockSync.BroadcastBlockChanges(changed, broadcaster);
        }

        _ = sessionId;
        _ = state;
    }

    private void HandleAttackMobOrVehicle(long sessionId, ConnectedPlayerState state, PlayerWorldInteractionPacket p, INetworkBroadcaster broadcaster)
    {
        var rayDir = new Vector3((float)p.DirectionOrTarget.X, (float)p.DirectionOrTarget.Y, (float)p.DirectionOrTarget.Z);
        if ((p.CountValue & 0x04) != 0 && _minecarts.TryBreakAlongRay(p.Origin, rayDir, 5.5, out var cartPos))
        {
            foreach (var (_, peer) in _players)
            {
                if (FindCartById(peer.RidingMinecartId) is null) peer.RidingMinecartId = 0;
            }

            if (state.GameMode == GameMode.Survival && _registries.Items.TryGet(BuiltInItems.Minecart, out var mcDef))
            {
                _ = _droppedItems.SpawnDrop(new ItemStack(mcDef.NumericId, 1), cartPos, pickupDelay: 0.2f);
            }

            return;
        }

        var dmg = 1.0f;
        if (p.AuxValue > 0 && _registries.Items.TryGetById((ushort)p.AuxValue, out var weapon))
        {
            dmg = Math.Clamp(weapon.AttackDamage, 1.0f, 16.0f);
        }

        var crit = (p.CountValue & 0x01) != 0;
        var sprint = (p.CountValue & 0x02) != 0;
        var res = _mobs.TryAttackAlongRay(p.Origin, rayDir, 4.8, dmg, crit, out var loot, out var lootPos, sprint);
        if (res.Killed && !loot.IsEmpty)
        {
            _ = _droppedItems.SpawnDrop(loot, new Vec3d(lootPos.X, lootPos.Y + 0.35, lootPos.Z), pickupDelay: 0.2f);
        }

        _ = sessionId;
        _ = broadcaster;
    }

    private void DismountPlayerIfRiding(ConnectedPlayerState state)
    {
        if (state.RidingMinecartId == 0) return;
        var cart = FindCartById(state.RidingMinecartId);
        if (cart is not null)
        {
            cart.HasPlayerRider = false;
            cart.RiderEntityId = 0;
        }

        state.RidingMinecartId = 0;
        state.RiderForwardSteer = 0.0f;
    }

    private OverHorizon.Gameplay.Vehicles.MinecartEntity? FindCartById(int id)
    {
        for (var i = 0; i < _minecarts.ActiveMinecarts.Count; i++)
        {
            if (_minecarts.ActiveMinecarts[i].Id == id) return _minecarts.ActiveMinecarts[i];
        }

        return null;
    }

    private long? FindSessionId(long entityId)
    {
        foreach (var (sid, pl) in _players)
        {
            if (pl.EntityId == entityId) return sid;
        }

        return null;
    }

    private bool HasAnyWorldEntities() =>
        _droppedItems.ActiveDrops.Count > 0 ||
        _arrows.ActiveArrows.Count > 0 ||
        _minecarts.ActiveMinecarts.Count > 0 ||
        _mobs.ActiveMobs.Count > 0 ||
        _tntAndWorld.ActiveTnt.Count > 0 ||
        _tntAndWorld.FallingBlocks.Count > 0;

    private WorldEntitiesSnapshotPacket BuildWorldEntitiesSnapshot()
    {
        var drops = new (int, ushort, int, Vec3d, Vec3d)[_droppedItems.ActiveDrops.Count];
        for (var i = 0; i < drops.Length; i++)
        {
            var d = _droppedItems.ActiveDrops[i];
            drops[i] = (d.Id, d.Stack.ItemId, d.Stack.Count, d.Position, d.Velocity);
        }

        var arrows = new (int, Vec3d, float, float, float, byte)[_arrows.ActiveArrows.Count];
        for (var i = 0; i < arrows.Length; i++)
        {
            var a = _arrows.ActiveArrows[i];
            var flags = (byte)((a.StuckInGround ? 1 : 0) | (a.IsCritical ? 2 : 0) | (a.FromSkeleton ? 4 : 0));
            arrows[i] = (a.Id, a.Position, a.YawDegrees, a.PitchDegrees, a.ShakeTimer, flags);
        }

        var carts = new (int, Vec3d, float, float, float, long)[_minecarts.ActiveMinecarts.Count];
        for (var i = 0; i < carts.Length; i++)
        {
            var c = _minecarts.ActiveMinecarts[i];
            carts[i] = (c.Id, c.FeetPosition, c.YawDegrees, c.SlopePitchDegrees, c.HurtWobble, c.HasPlayerRider ? c.RiderEntityId : 0L);
        }

        var mobs = new (int, byte, Vec3d, float, float, float, float, byte)[_mobs.ActiveMobs.Count];
        for (var i = 0; i < mobs.Length; i++)
        {
            var m = _mobs.ActiveMobs[i];
            var flags = (byte)((m.IsDead ? 1 : 0) | (m.IsBurningInSun ? 2 : 0) | (m.SkeletonIsDrawingBow ? 4 : 0) | (m.EndermanAngry ? 8 : 0) | (m.SheepSheared ? 16 : 0));
            mobs[i] = (m.Id, (byte)m.Type, m.FeetPosition, m.YawDegrees, m.WalkPhase, m.HurtFlashTimer, m.CreeperFuseProgress, flags);
        }

        var tnts = new (int, Vec3d, bool)[_tntAndWorld.ActiveTnt.Count];
        for (var i = 0; i < tnts.Length; i++)
        {
            var t = _tntAndWorld.ActiveTnt[i];
            tnts[i] = (t.Id, t.FeetPosition, t.FlashWhite);
        }

        var falling = new (int, ushort, int, double, int)[_tntAndWorld.FallingBlocks.Count];
        for (var i = 0; i < falling.Length; i++)
        {
            var fb = _tntAndWorld.FallingBlocks[i];
            falling[i] = (fb.Id, fb.BlockState.Id, fb.BlockX, fb.CurrentY, fb.BlockZ);
        }

        return new WorldEntitiesSnapshotPacket(drops, arrows, carts, mobs, tnts, falling);
    }
}
