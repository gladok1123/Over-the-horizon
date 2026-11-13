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

        var playerFeetList = new List<Vec3d>(_players.Count);
        Vec3d anchorPos = default;
        foreach (var (_, pl) in _players)
        {
            if (pl.GameMode != GameMode.Spectator)
            {
                playerFeetList.Add(pl.LastPosition);
                anchorPos = pl.LastPosition;
            }
        }

        var blockChanges = new List<(BlockPos Position, ushort BlockStateId)>();
        var ignitedTnts = new List<BlockPos>();
        var redstoneSounds = new List<(BlockPos Pos, RedstoneSoundKind Sound)>();
        void RecordServerBlockSet(BlockPos p, BlockState s)
        {
            if (_world.SetBlockState(p, s, tickNumber))
            {
                blockChanges.Add((p, s.Id));
            }
        }

        _redstone.Step(
            dtSeconds,
            playerFeetList.ToArray(),
            _world.GetBlockState,
            (pos, st) =>
            {
                if (_world.SetBlockState(pos, st, tickNumber))
                {
                    blockChanges.Add((pos, st.Id));
                    _tntAndWorld.NotifyWorldBlockChanged(pos, _world.GetBlockState, RecordServerBlockSet);
                }
            },
            ignitedTnts,
            redstoneSounds);

        for (var i = 0; i < ignitedTnts.Count; i++)
        {
            var tp = ignitedTnts[i];
            if (_world.SetBlockState(tp, BlockState.Air, tickNumber))
            {
                blockChanges.Add((tp, BlockState.Air.Id));
                _ = _tntAndWorld.SpawnPrimedTnt(tp);
                broadcaster.BroadcastToPlaySessions(new InventoryGrantAndEventPacket(0, 0, 3, new Vec3d(tp.X + 0.5, tp.Y + 0.5, tp.Z + 0.5)));
            }
        }

        var explosions = new List<Vec3d>();
        var fragileDrops = new List<(BlockPos Pos, BlockState BrokenState)>();
        _tntAndWorld.Step(
            dtSeconds,
            _collisionReader,
            anchorPos,
            _world.GetBlockState,
            _ => (15, 0),
            (pos, st) =>
            {
                if (_world.SetBlockState(pos, st, tickNumber))
                {
                    blockChanges.Add((pos, st.Id));
                    _redstone.NotifyBlockChanged(pos);
                }
            },
            explosions,
            setBlockBatch: null,
            brokenFragileDrops: fragileDrops);

        for (var i = 0; i < fragileDrops.Count; i++)
        {
            var bKey = _registries.Blocks.GetStateMetadata(fragileDrops[i].BrokenState).BlockKey;
            var drop = BlockDropResolver.ResolveDrop(bKey, heldTool: null, _registries.Items);
            if (!drop.IsEmpty)
            {
                _ = _droppedItems.SpawnBlockDrop(fragileDrops[i].Pos, drop);
            }
        }

        for (var i = 0; i < explosions.Count; i++)
        {
            broadcaster.BroadcastToPlaySessions(new InventoryGrantAndEventPacket(0, 0, 1, explosions[i]));
            _ = _mobs.ApplyExplosionBlast(explosions[i], radius: 8.0f, maxDamage: 26.0f);
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
        if (currentPos is null) return;
        var playerFeet = currentPos.Value;

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
                _ = _registries.Items.TryGetById(playerState.MainHandItemId, out var heldTool);
                var drop = BlockDropResolver.ResolveDrop(existingMeta.BlockKey, heldTool, _registries.Items);
                if (!drop.IsEmpty)
                {
                    _ = _droppedItems.SpawnBlockDrop(target, drop);
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

        if (_registries.Blocks.GetStateMetadata(placeEvent.PlacedState).BlockKey == BuiltInBlocks.Fire)
        {
            _tntAndWorld.TrackFireBlock(target);
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
                _ = _arrows.SpawnPlayerArrow(p.Origin, dir, charge, canBePickedUp: p.CountValue != 0);
                break;
            }

            case PlayerWorldInteractionKind.DropItem:
            {
                if (p.AuxValue > 0 && p.CountValue > 0 && _registries.Items.TryGetById((ushort)p.AuxValue, out _))
                {
                    var fwd = new Vector3((float)p.DirectionOrTarget.X, (float)p.DirectionOrTarget.Y, (float)p.DirectionOrTarget.Z);
                    _ = _droppedItems.SpawnPlayerThrow(p.Origin, fwd, new ItemStack((ushort)p.AuxValue, Math.Clamp(p.CountValue, 1, 64)));
                }

                break;
            }

            case PlayerWorldInteractionKind.PlaceMinecart:
            {
                var railPos = p.Origin.ToBlockPos();
                _ = _minecarts.TrySpawnOnRail(railPos, _world.GetBlockState);
                break;
            }

            case PlayerWorldInteractionKind.MountOrDismountMinecart:
                HandleMountOrDismountMinecart(session.SessionId, state, p, broadcaster);
                break;

            case PlayerWorldInteractionKind.SteerMinecart:
                state.RiderForwardSteer = Math.Clamp(p.CountValue, -1, 1);
                break;

            case PlayerWorldInteractionKind.SpawnMobEgg:
            {
                var mobByte = Math.Clamp(p.CountValue, 0, (int)MobType.Creeper);
                _ = _mobs.SpawnMob((MobType)mobByte, p.Origin);
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
