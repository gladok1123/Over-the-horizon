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
/// Server-authoritative multiplayer world simulation (Mobs, Ballistic Arrows, 3D Dropped Items,
/// Rideable Minecarts, Redstone, Primed TNT, Falling Blocks, Fire, and Void damage).
/// </summary>
public sealed partial class ServerPlaySessionBridge
{
    /// <summary>Steps authoritative server world entities at 20 TPS and broadcasts deltas/snapshots.</summary>
    public void StepWorldEntitiesAndSync(double dtSeconds, long tickNumber, INetworkBroadcaster? broadcaster)
    {
        if (broadcaster is null || _players.IsEmpty)
        {
            return;
        }

        var blockChanges = new List<(BlockPos Position, ushort BlockStateId)>();
        var ignitedTnts = new List<BlockPos>();
        var redstoneSounds = new List<(BlockPos Pos, RedstoneSoundKind Sound)>();
        _redstone.Step(
            dtSeconds,
            _world.GetBlockState,
            (pos, st) =>
            {
                if (_world.SetBlockState(pos, st, tickNumber))
                {
                    blockChanges.Add((pos, st.Id));
                }
            },
            ignitedTnts,
            redstoneSounds);

        for (var i = 0; i < ignitedTnts.Count; i++)
        {
            var tp = ignitedTnts[i];
            if (_tntAndWorld.IgniteBlock(_world.GetBlockState, (p, s) => _world.SetBlockState(p, s, tickNumber), tp))
            {
                blockChanges.Add((tp, BlockState.Air.Id));
                broadcaster.BroadcastToPlaySessions(new InventoryGrantAndEventPacket(0, 0, 3, new Vec3d(tp.X + 0.5, tp.Y + 0.5, tp.Z + 0.5)));
            }
        }

        var explosions = new List<Vec3d>();
        var explodedBlocks = new List<(BlockPos Pos, BlockState State)>();
        var detachedBlocks = new List<(BlockPos Pos, BlockState State)>();
        _tntAndWorld.Step(
            dtSeconds,
            _world.GetBlockState,
            (pos, st) =>
            {
                if (_world.SetBlockState(pos, st, tickNumber))
                {
                    blockChanges.Add((pos, st.Id));
                    _redstone.NotifyBlockChanged(pos);
                    return true;
                }

                return false;
            },
            explosions,
            explodedBlocks,
            detachedBlocks);

        for (var i = 0; i < explodedBlocks.Count; i++)
        {
            var drop = BlockDropResolver.ResolveExplosionDrop(_registries, explodedBlocks[i].State);
            if (!drop.IsEmpty)
            {
                var p = explodedBlocks[i].Pos;
                _ = _droppedItems.SpawnDrop(drop, new Vec3d(p.X + 0.5, p.Y + 0.4, p.Z + 0.5));
            }
        }

        for (var i = 0; i < detachedBlocks.Count; i++)
        {
            var drop = BlockDropResolver.ResolveExplosionDrop(_registries, detachedBlocks[i].State);
            if (!drop.IsEmpty)
            {
                var p = detachedBlocks[i].Pos;
                _ = _droppedItems.SpawnDrop(drop, new Vec3d(p.X + 0.5, p.Y + 0.35, p.Z + 0.5));
            }
        }

        for (var i = 0; i < explosions.Count; i++)
        {
            broadcaster.BroadcastToPlaySessions(new InventoryGrantAndEventPacket(0, 0, 1, explosions[i]));
            ApplyExplosionDamageToPlayers(explosions[i], tickNumber, broadcaster);
        }

        StepMultiplayerMinecarts(dtSeconds, broadcaster);
        StepMultiplayerMobsAndArrows(dtSeconds, tickNumber, blockChanges, broadcaster);
        StepDroppedItemsAndVoidDamage(dtSeconds, tickNumber, broadcaster);

        if (blockChanges.Count > 0)
        {
            ServerWorldTimeAndBlockSync.BroadcastBlockChanges(blockChanges, broadcaster);
        }

        var hasAny = HasAnyWorldEntities();
        if (hasAny || _hadEntitiesLastTick)
        {
            _hadEntitiesLastTick = hasAny;
            broadcaster.BroadcastToPlaySessions(BuildWorldEntitiesSnapshot());
        }
    }

    private void HandleEquipmentAndMode(
        long sessionId,
        ConnectedPlayerState state,
        PlayerEquipmentAndModePacket equip,
        INetworkBroadcaster broadcaster)
    {
        state.GameMode = (GameMode)Math.Clamp((int)equip.GameModeByte, 0, 2);
        state.MainHandItemId = equip.MainHandItemId;
        state.OffhandItemId = equip.OffhandItemId;
        state.PackedArmorLower = equip.PackedArmorLower;
        state.PackedArmorUpper = equip.PackedArmorUpper;
        state.ActionFlags = equip.ActionFlags;
        state.BowChargeByte = equip.BowChargeByte;
        state.SwingCounter = equip.SwingCounter;
        if (state.GameMode == GameMode.Spectator)
        {
            DismountPlayerIfRiding(state);
        }

        BroadcastToPeers(sessionId, broadcaster, state.ToEquipmentPacket());
    }

    private void HandleBlockAction(
        PlayerSessionInfo session,
        ConnectedPlayerState playerState,
        PlayerBlockActionPacket action,
        long tickNumber,
        INetworkBroadcaster broadcaster)
    {
        if (playerState.GameMode == GameMode.Spectator) return;

        var currentPos = _gameplay.TryGetEntityPosition(playerState.EntityId);
        if (currentPos is not { } playerFeet) return;

        var target = action.TargetPos;
        if (target.Y < EngineConstants.WorldMinY || target.Y >= EngineConstants.WorldMaxY) return;

        var eyePos = new Vec3d(playerFeet.X, playerFeet.Y + PlayerEyeHeight, playerFeet.Z);
        var blockCenter = new Vec3d(target.X + 0.5, target.Y + 0.5, target.Z + 0.5);
        if ((blockCenter - eyePos).LengthSquared > MaxBlockReachSquared) return;

        var existingState = _world.GetBlockState(target);

        if (action.Action == PlayerBlockActionKind.BreakBlock)
        {
            var existingMeta = _registries.Blocks.GetStateMetadata(existingState);
            if (existingMeta.BlockKey == BuiltInBlocks.Bedrock && playerState.GameMode != GameMode.Creative)
            {
                _ = broadcaster.SendToSession(session.SessionId, new BlockUpdatePacket(target, existingState.Id));
                return;
            }

            var breakEvent = new OverHorizon.Modding.Events.BlockBreakEvent(session.SessionId, session.Username, target, existingState);
            _eventBus.Publish(ref breakEvent);
            if (breakEvent.IsCancelled)
            {
                _ = broadcaster.SendToSession(session.SessionId, new BlockUpdatePacket(target, existingState.Id));
                return;
            }

            if (playerState.GameMode == GameMode.Survival && existingMeta.BlockKey != BuiltInBlocks.Fire)
            {
                _registries.Items.TryGetById(playerState.MainHandItemId, out var heldTool);
                var drop = BlockDropResolver.ResolveDrop(_registries, existingState, heldTool);
                if (!drop.IsEmpty)
                {
                    _ = _droppedItems.SpawnDrop(drop, new Vec3d(target.X + 0.5, target.Y + 0.35, target.Z + 0.5), pickupDelay: 0.28f);
                }
            }

            ServerWorldTimeAndBlockSync.ApplyMutationAndBroadcastCascade(
                _world, _registries, _redstone, target, BlockState.Air, tickNumber, broadcaster, _droppedItems, _tntAndWorld);
            return;
        }

        if (action.BlockStateId == 0 || action.BlockStateId >= _registries.Blocks.StateCount) return;

        var candidateState = new BlockState(action.BlockStateId);
        if (_registries.Blocks.GetStateMetadata(candidateState).IsSolid)
        {
            var playerBox = BoundingBox.FromEntityFeet(playerFeet, PlayerWidth, PlayerHeight).Inflate(-1e-4, -1e-4, -1e-4);
            if (playerBox.Intersects(BoundingBox.FromBlock(target))) return;
        }

        var placeEvent = new OverHorizon.Modding.Events.BlockPlaceEvent(session.SessionId, session.Username, target, candidateState);
        _eventBus.Publish(ref placeEvent);
        if (placeEvent.IsCancelled)
        {
            _ = broadcaster.SendToSession(session.SessionId, new BlockUpdatePacket(target, existingState.Id));
            return;
        }

        ServerWorldTimeAndBlockSync.ApplyMutationAndBroadcastCascade(
            _world, _registries, _redstone, target, placeEvent.PlacedState, tickNumber, broadcaster, _droppedItems, _tntAndWorld);
    }

    private void HandleWorldInteraction(
        PlayerSessionInfo session,
        ConnectedPlayerState state,
        PlayerWorldInteractionPacket p,
        long tickNumber,
        INetworkBroadcaster broadcaster)
    {
        if (state.GameMode == GameMode.Spectator) return;

        switch (p.Kind)
        {
            case PlayerWorldInteractionKind.ShootArrow:
            {
                var dir = new Vector3((float)p.DirectionOrTarget.X, (float)p.DirectionOrTarget.Y, (float)p.DirectionOrTarget.Z);
                var charge = Math.Clamp(p.AuxValue / 100.0f, 0.15f, 1.0f);
                _ = _arrows.ShootArrow(p.Origin, dir, charge, fromSkeleton: false, ownerEntityId: state.EntityId);
                break;
            }

            case PlayerWorldInteractionKind.DropItem:
            {
                if (p.AuxValue > 0 && p.CountValue > 0 && _registries.Items.TryGetById((ushort)p.AuxValue, out _))
                {
                    var fwd = new Vector3((float)p.DirectionOrTarget.X, (float)p.DirectionOrTarget.Y, (float)p.DirectionOrTarget.Z);
                    _ = _droppedItems.SpawnPlayerThrow(new ItemStack((ushort)p.AuxValue, Math.Clamp(p.CountValue, 1, 64)), p.Origin, fwd);
                }

                break;
            }

            case PlayerWorldInteractionKind.PlaceMinecart:
            {
                var railPos = p.DirectionOrTarget.ToBlockPos();
                _ = _minecarts.TryPlaceMinecart(_world.GetBlockState, railPos);
                break;
            }

            case PlayerWorldInteractionKind.MountOrDismountMinecart:
                HandleMountOrDismountMinecart(session.SessionId, state, p, broadcaster);
                break;

            case PlayerWorldInteractionKind.SteerMinecart:
                state.RiderForwardSteer = Math.Clamp(p.AuxValue / 1000.0f, -1.0f, 1.0f);
                break;

            case PlayerWorldInteractionKind.SpawnMobEgg:
            {
                if (p.AuxValue >= 0 && p.AuxValue <= (int)MobType.Creeper)
                {
                    _ = _mobs.SpawnMob((MobType)p.AuxValue, p.DirectionOrTarget);
                }

                break;
            }

            case PlayerWorldInteractionKind.InteractWorldComponent:
                HandleComponentInteraction(session.SessionId, state, p, tickNumber, broadcaster);
                break;

            case PlayerWorldInteractionKind.AttackMobOrVehicle:
                HandleAttackMobOrVehicle(session.SessionId, state, p, broadcaster);
                break;
        }
    }
}
