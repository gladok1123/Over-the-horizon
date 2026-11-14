using System.Numerics;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Inventory;
using OverHorizon.Gameplay.Mobs;
using OverHorizon.Gameplay.Redstone;
using OverHorizon.Gameplay.Survival;
using OverHorizon.Gameplay.Vehicles;
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
        if (_minecarts.Minecarts.Count == 0) return;

        var playerFeet = Vec3d.Zero;
        var riderSteer = 0.0f;
        var riderLook = Vector3.UnitZ;
        foreach (var (_, pl) in _players)
        {
            playerFeet = pl.LastPosition;
            if (pl.RidingMinecartId != 0)
            {
                riderSteer = pl.RiderForwardSteer;
                riderLook = pl.LookForward;
                break;
            }
        }

        _minecarts.Step(dtSeconds, _collisionReader, _world.GetBlockState, playerFeet, riderLook, riderSteer);
        foreach (var (sid, pl) in _players)
        {
            if (pl.RidingMinecartId == 0) continue;
            var cart = FindCartById(pl.RidingMinecartId);
            if (cart is null || !cart.HasPlayerPassenger)
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
            var creeperBlasts = new List<Vec3d>();
            var fuseHissed = new List<Vec3d>();
            var shieldBlocked = new List<Vec3d>();
            var mobDmg = _mobs.Step(
                _collisionReader,
                ProbeServerSpawnSite,
                primaryTarget.LastPosition,
                primaryTarget.GameMode,
                primaryTarget.GetArmorDefensePoints(),
                WorldTime.SkyBrightness,
                (float)dtSeconds,
                creeperBlasts,
                (origin, vel) => _ = _arrows.SpawnSkeletonArrow(origin, vel),
                mobLandingHurtEvents: null,
                creeperFuseStartedPositions: fuseHissed,
                isPlayerBlockingShield: primaryTarget.IsBlockingShield,
                playerLookForward: primaryTarget.LookForward,
                shieldBlockedPositions: shieldBlocked,
                isFireBlock: p => _registries.Blocks.GetStateMetadata(_world.GetBlockState(p)).BlockKey == BuiltInBlocks.Fire);

            for (var i = 0; i < fuseHissed.Count; i++)
            {
                broadcaster.BroadcastToPlaySessions(new InventoryGrantAndEventPacket(0, 0, 3, fuseHissed[i]));
            }

            for (var i = 0; i < creeperBlasts.Count; i++)
            {
                var blastCenter = creeperBlasts[i];
                _ = _tntAndWorld.DetonateRaycastExplosion(
                    blastCenter,
                    power: 3.1f,
                    _world.GetBlockState,
                    (pos, st) =>
                    {
                        if (_world.SetBlockState(pos, st, tickNumber))
                        {
                            blockChanges.Add((pos, st.Id));
                            _redstone.NotifyBlockChanged(pos);
                        }
                    });
                broadcaster.BroadcastToPlaySessions(new InventoryGrantAndEventPacket(0, 0, 1, blastCenter));
            }

            if (shieldBlocked.Count > 0)
            {
                broadcaster.BroadcastToPlaySessions(new InventoryGrantAndEventPacket(0, 0, 2, primaryTarget.LastPosition));
            }

            var targetSid = FindSessionId(primaryTarget.EntityId);
            if (mobDmg > 0.01f && !primaryTarget.IsInvulnerable && targetSid.HasValue)
            {
                ServerPvpCombatHandler.ApplyPlayerDamageAndCheckDeath(
                    primaryTarget, targetSid.Value, 0, mobDmg, Vec3d.Zero, tickNumber,
                    $"{primaryTarget.Username} was slain by a monster!", _gameplay, broadcaster);
            }
        }

        if (_arrows.Arrows.Count == 0) return;

        var arrowTargetPos = primaryTarget?.LastPosition ?? new Vec3d(0, -9999, 0);
        var arrowMode = primaryTarget?.GameMode ?? GameMode.Spectator;
        var collectedLoot = new List<ItemStack>();
        var arrowImpacts = new List<Vec3d>();
        var shieldDeflected = new List<Vec3d>();
        var arrowDmg = _arrows.Step(
            dtSeconds,
            _collisionReader,
            _world.GetBlockState,
            _mobs,
            _registries.Items,
            arrowTargetPos,
            arrowMode,
            collectedLoot,
            arrowImpacts,
            primaryTarget?.IsBlockingShield ?? false,
            primaryTarget?.LookForward ?? Vector3.UnitZ,
            shieldDeflected);

        if (primaryTarget is not null)
        {
            var hitSid = FindSessionId(primaryTarget.EntityId);
            for (var i = 0; i < collectedLoot.Count; i++)
            {
                if (hitSid.HasValue)
                {
                    _ = broadcaster.SendToSession(hitSid.Value, new InventoryGrantAndEventPacket(collectedLoot[i].ItemId, collectedLoot[i].Count, 0, primaryTarget.LastPosition));
                }
            }

            if (shieldDeflected.Count > 0)
            {
                broadcaster.BroadcastToPlaySessions(new InventoryGrantAndEventPacket(0, 0, 2, primaryTarget.LastPosition));
            }
            else if (arrowDmg > 0.01f && !primaryTarget.IsInvulnerable && hitSid.HasValue)
            {
                var scaled = arrowDmg * primaryTarget.GetArmorDamageMultiplier();
                ServerPvpCombatHandler.ApplyPlayerDamageAndCheckDeath(
                    primaryTarget, hitSid.Value, 0, scaled, Vec3d.Zero, tickNumber,
                    $"{primaryTarget.Username} was shot by an arrow!", _gameplay, broadcaster);
            }
        }
    }

    private void StepDroppedItemsAndVoidDamage(double dtSeconds, long tickNumber, INetworkBroadcaster broadcaster)
    {
        foreach (var (sid, pl) in _players)
        {
            if (pl.GameMode is GameMode.Survival or GameMode.Creative &&
                pl.LastPosition.Y < EngineConstants.WorldMinY - 12.0 &&
                tickNumber - pl.LastVoidDamageTick >= 10L)
            {
                pl.LastVoidDamageTick = tickNumber;
                ServerPvpCombatHandler.ApplyPlayerDamageAndCheckDeath(
                    pl, sid, 0, 4.0f, Vec3d.Zero, tickNumber,
                    $"{pl.Username} fell out of the world!", _gameplay, broadcaster,
                    bypassInvulnerability: pl.GameMode == GameMode.Creative);
            }
        }

        if (_droppedItems.Items.Count == 0) return;

        _ = _droppedItems.Step(dtSeconds, _collisionReader, _registries.Items);
        var picked = new List<(int EntityId, ItemStack Picked)>();
        foreach (var (sid, pl) in _players)
        {
            picked.Clear();
            _droppedItems.CollectForMultiplayerPlayer(
                pl.LastPosition,
                pl.GameMode,
                _ => ItemStack.Empty,
                picked);
            for (var i = 0; i < picked.Count; i++)
            {
                _ = broadcaster.SendToSession(sid, new InventoryGrantAndEventPacket(picked[i].Picked.ItemId, picked[i].Picked.Count, 0, pl.LastPosition));
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

            if (WorldMobManager.IsFrontalShieldBlock(pl.IsBlockingShield, pl.LookForward, pl.LastPosition, center))
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
        if (p.CountValue == 0)
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
            cart.HasPlayerPassenger = true;
            state.RidingMinecartId = cart.Id;
            BroadcastToPeers(sessionId, broadcaster, state.ToEquipmentPacket());
            _ = broadcaster.SendToSession(sessionId, new InventoryGrantAndEventPacket(0, cart.Id, 5, cart.FeetPosition));
        }
    }

    private void HandleComponentInteraction(long sessionId, ConnectedPlayerState state, PlayerWorldInteractionPacket p, long tickNumber, INetworkBroadcaster broadcaster)
    {
        var pos = p.Origin.ToBlockPos();
        var cur = _world.GetBlockState(pos);
        if (cur.IsAir) return;

        var changed = new List<(BlockPos Position, ushort BlockStateId)>();
        var ignited = new List<BlockPos>();
        var sounds = new List<(BlockPos Pos, RedstoneSoundKind Sound)>();
        if (_redstone.TryInteractComponent(
                pos,
                _world.GetBlockState,
                (bp, st) =>
                {
                    if (_world.SetBlockState(bp, st, tickNumber)) changed.Add((bp, st.Id));
                }))
        {
            _redstone.EvaluateImmediate(
                _world.GetBlockState,
                (bp, st) =>
                {
                    if (_world.SetBlockState(bp, st, tickNumber)) changed.Add((bp, st.Id));
                },
                ignited,
                sounds);
            for (var i = 0; i < ignited.Count; i++)
            {
                if (_world.SetBlockState(ignited[i], BlockState.Air, tickNumber))
                {
                    changed.Add((ignited[i], BlockState.Air.Id));
                    _ = _tntAndWorld.SpawnPrimedTnt(ignited[i]);
                }
            }

            ServerWorldTimeAndBlockSync.BroadcastBlockChanges(changed, broadcaster);
            return;
        }

        _ = _registries.Items.TryGetById(Math.Clamp(p.AuxValue, 0, ushort.MaxValue), out var heldItem);
        if (ComposterAndTrapdoorMechanics.TryToggleTrapdoor(
                _registries.Blocks,
                pos,
                _world.GetBlockState,
                (bp, st) =>
                {
                    if (_world.SetBlockState(bp, st, tickNumber)) changed.Add((bp, st.Id));
                }))
        {
            ServerWorldTimeAndBlockSync.BroadcastBlockChanges(changed, broadcaster);
            return;
        }

        if (ComposterAndTrapdoorMechanics.TryInteractComposter(
                _registries.Blocks,
                _registries.Items,
                pos,
                _world.GetBlockState,
                (bp, st) =>
                {
                    if (_world.SetBlockState(bp, st, tickNumber)) changed.Add((bp, st.Id));
                },
                heldItem,
                out _,
                out var harvestedBoneMeal))
        {
            if (!harvestedBoneMeal.IsEmpty)
            {
                _ = _droppedItems.SpawnBlockDrop(new BlockPos(pos.X, pos.Y + 1, pos.Z), harvestedBoneMeal);
            }

            ServerWorldTimeAndBlockSync.BroadcastBlockChanges(changed, broadcaster);
            return;
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

            var mcId = _registries.Items.GetId(BuiltInItems.Minecart);
            if (state.GameMode == GameMode.Survival && mcId > 0)
            {
                _ = _droppedItems.SpawnScatterDrop(cartPos, new ItemStack((ushort)mcId, 1), pickupDelaySeconds: 0.25f);
            }

            return;
        }

        var dmg = 1.0f;
        if (p.AuxValue > 0 && _registries.Items.TryGetById(p.AuxValue, out var weapon))
        {
            dmg = Math.Clamp(weapon.AttackDamage, 1.0f, 16.0f);
        }

        var crit = (p.CountValue & 0x01) != 0;
        var sprint = (p.CountValue & 0x02) != 0;
        if (_mobs.TryAttackAlongRay(p.Origin, rayDir, 4.8, dmg, crit, _registries.Items, out var hitMob, out var drops, sprint) && hitMob is not null)
        {
            for (var i = 0; i < drops.Count; i++)
            {
                _ = _droppedItems.SpawnScatterDrop(hitMob.FeetPosition, drops[i], pickupDelaySeconds: 0.30f);
            }
        }

        _ = sessionId;
        _ = broadcaster;
    }

    private (byte SkyLight, byte BlockLight, bool IsGrassTop, bool IsSolidGround, bool IsClearSpace) ProbeServerSpawnSite(BlockPos feetPos)
    {
        var below = _world.GetBlockState(feetPos.Offset(Direction.Down));
        var feet = _world.GetBlockState(feetPos);
        var head = _world.GetBlockState(feetPos.Offset(Direction.Up));
        var belowMeta = _registries.Blocks.GetStateMetadata(below);
        return (15, 0, belowMeta.BlockKey == BuiltInBlocks.GrassBlock, belowMeta.IsSolid, feet.IsAir && head.IsAir);
    }

    private void DismountPlayerIfRiding(ConnectedPlayerState state)
    {
        if (state.RidingMinecartId == 0) return;
        var cart = FindCartById(state.RidingMinecartId);
        if (cart is not null)
        {
            cart.HasPlayerPassenger = false;
        }

        state.RidingMinecartId = 0;
        state.RiderForwardSteer = 0.0f;
    }

    private MinecartEntity? FindCartById(int id)
    {
        for (var i = 0; i < _minecarts.Minecarts.Count; i++)
        {
            if (_minecarts.Minecarts[i].Id == id) return _minecarts.Minecarts[i];
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
        _droppedItems.Items.Count > 0 ||
        _arrows.Arrows.Count > 0 ||
        _minecarts.Minecarts.Count > 0 ||
        _mobs.Mobs.Count > 0 ||
        _tntAndWorld.PrimedTnts.Count > 0 ||
        _tntAndWorld.FallingBlocks.Count > 0;

    private WorldEntitiesSnapshotPacket BuildWorldEntitiesSnapshot()
    {
        var drops = new (int, ushort, int, Vec3d, Vec3d)[_droppedItems.Items.Count];
        for (var i = 0; i < drops.Length; i++)
        {
            var d = _droppedItems.Items[i];
            drops[i] = (d.Id, d.Stack.ItemId, d.Stack.Count, d.Position, d.Velocity);
        }

        var arrows = new (int, Vec3d, float, float, float, byte)[_arrows.Arrows.Count];
        for (var i = 0; i < arrows.Length; i++)
        {
            var a = _arrows.Arrows[i];
            var flags = (byte)((a.IsStuckInBlock ? 1 : 0) | (a.IsCritical ? 2 : 0) | (!a.FiredByPlayer ? 4 : 0));
            arrows[i] = (a.Id, a.Position, a.YawDegrees, a.PitchDegrees, a.ShakeTimer, flags);
        }

        var carts = new (int, Vec3d, float, float, float, long)[_minecarts.Minecarts.Count];
        for (var i = 0; i < carts.Length; i++)
        {
            var c = _minecarts.Minecarts[i];
            var riderEntId = 0L;
            if (c.HasPlayerPassenger)
            {
                foreach (var (_, pl) in _players)
                {
                    if (pl.RidingMinecartId == c.Id)
                    {
                        riderEntId = pl.EntityId;
                        break;
                    }
                }
            }

            carts[i] = (c.Id, c.FeetPosition, c.YawDegrees, c.PitchDegrees, c.HurtWobbleTimer, riderEntId);
        }

        var mobs = new (int, byte, Vec3d, float, float, float, float, byte)[_mobs.Mobs.Count];
        for (var i = 0; i < mobs.Length; i++)
        {
            var m = _mobs.Mobs[i];
            var flags = (byte)((m.IsDead ? 1 : 0) | (m.IsOnFire ? 2 : 0) | (m.IsTargetingPlayer && m.Type == MobType.Skeleton ? 4 : 0) | (m.IsSheared ? 16 : 0));
            mobs[i] = (m.Id, (byte)m.Type, m.FeetPosition, m.YawDegrees, m.WalkPhase, m.HurtFlashTimer, Math.Clamp(m.CreeperFuseTimer / 1.5f, 0.0f, 1.0f), flags);
        }

        var tnts = new (int, Vec3d, bool)[_tntAndWorld.PrimedTnts.Count];
        for (var i = 0; i < tnts.Length; i++)
        {
            var t = _tntAndWorld.PrimedTnts[i];
            tnts[i] = (t.Id, t.FeetPosition, t.IsFlashingWhite);
        }

        var falling = new (int, ushort, int, double, int)[_tntAndWorld.FallingBlocks.Count];
        for (var i = 0; i < falling.Length; i++)
        {
            var fb = _tntAndWorld.FallingBlocks[i];
            falling[i] = (fb.Id, fb.State.Id, fb.BlockX, fb.CurrentY, fb.BlockZ);
        }

        return new WorldEntitiesSnapshotPacket(drops, arrows, carts, mobs, tnts, falling);
    }
}
