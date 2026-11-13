using Microsoft.Extensions.Logging;
using OverHorizon.Audio;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay.Inventory;
using OverHorizon.Networking.Packets;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Rendering;
using OverHorizon.World.Serialization;

namespace OverHorizon.Client;

/// <summary>
/// Applies inbound authoritative server packets (including <see cref="RemotePlayerEquipmentPacket"/>,
/// <see cref="WorldEntitiesSnapshotPacket"/>, and <see cref="InventoryGrantAndEventPacket"/>)
/// to the client world cache, player pose, unified inventory, 3D particles/audio, and chat log.
/// </summary>
internal static class ClientInboundPacketApplier
{
    public static void Apply(
        IClientboundPacket packet,
        long localEntityId,
        ClientRemotePlayerTracker remotePlayers,
        ClientPlayerController player,
        ClientWorldCache worldCache,
        ClientWorldSyncAndMobBridge worldBridge,
        IBlockRegistry blocks,
        IRegistry<ItemDefinition> items,
        IAudioSubsystem audio,
        IRenderSubsystem renderer,
        ClientChatHistory chatHistory,
        ILogger logger,
        int activeViewDistance,
        ref bool receivedInitialSpawnSync,
        ref Vec3d lastSentFeetPosition)
    {
        if (remotePlayers.TryApply(packet))
        {
            return;
        }

        switch (packet)
        {
            case SynchronizePlayerPositionPacket syncPos:
                if (!receivedInitialSpawnSync || (syncPos.Position - player.FeetPosition).LengthSquared > 256.0)
                {
                    player.ApplyAuthoritativePose(syncPos.Position, syncPos.YawDegrees, syncPos.PitchDegrees);
                    receivedInitialSpawnSync = true;
                }
                else
                {
                    player.ApplyAuthoritativePositionOnly(syncPos.Position);
                }

                lastSentFeetPosition = syncPos.Position;
                break;

            case EntityCombatEventPacket combatPkt:
                if (combatPkt.TargetEntityId == localEntityId)
                {
                    player.ApplyAuthoritativeCombatHit(combatPkt.NewHealth, combatPkt.KnockbackVelocity, combatPkt.TargetDied);
                    audio.PlayPositional(SoundCue.PlayerHurt, player.Camera.Position, gain: 0.85f * player.MasterVolume);
                }
                else if (remotePlayers.TryApplyCombatFeedback(combatPkt.TargetEntityId, combatPkt.NewHealth, combatPkt.TargetDied, out var peerPos))
                {
                    worldBridge.Particles.EmitCombatHit(peerPos, isCritical: false);
                    audio.PlayPositional(SoundCue.PlayerHit, peerPos + new Vec3d(0.0, 1.0, 0.0), gain: 0.85f * player.MasterVolume);
                }

                break;

            case WorldEntitiesSnapshotPacket entPkt:
                worldBridge.ApplyMultiplayerEntitiesSnapshot(entPkt, localEntityId, player);
                break;

            case InventoryGrantAndEventPacket evtPkt:
                ApplyInventoryOrWorldEvent(evtPkt, player, worldBridge, items, audio);
                break;

            case ChunkDataPacket chunkPkt:
                var col = ChunkNetworkSerializer.DeserializeColumn(chunkPkt.Position, chunkPkt.Payload.Span, blocks);
                worldCache.UpsertColumn(col);
                _ = worldCache.EvictOutOfRangeChunks(player.FeetPosition.ToBlockPos().ToChunkPos(), activeViewDistance + 4, renderer);
                break;

            case BlockUpdatePacket blockPkt:
                var prevState = worldCache.GetBlockState(blockPkt.Position);
                var newState = new BlockState(blockPkt.BlockStateId);
                if (prevState != newState && worldCache.ApplyBlockUpdate(blockPkt.Position, newState))
                {
                    worldCache.RemeshAroundBlockImmediate(blockPkt.Position, renderer);
                    var soundPos = new Vec3d(blockPkt.Position.X + 0.5, blockPkt.Position.Y + 0.5, blockPkt.Position.Z + 0.5);
                    if (newState.IsAir)
                    {
                        var brokenKey = blocks.GetStateMetadata(prevState).BlockKey;
                        worldBridge.EmitBlockBreakParticles(blockPkt.Position, brokenKey);
                        var breakCue = brokenKey == BuiltInBlocks.Fire ? SoundCue.Fizz : BlockSoundProfile.ResolveBreakCue(brokenKey);
                        audio.PlayPositional(breakCue, soundPos, gain: 0.72f * player.MasterVolume, pitch: BlockSoundProfile.ResolveBreakPitch(brokenKey));
                    }
                    else
                    {
                        var placedKey = blocks.GetStateMetadata(newState).BlockKey;
                        audio.PlayPositional(BlockSoundProfile.ResolvePlaceCue(placedKey), soundPos, gain: 0.72f * player.MasterVolume, pitch: BlockSoundProfile.ResolvePlacePitch(placedKey));
                    }
                }

                break;

            case MultiBlockUpdatePacket multiPkt:
                for (var i = 0; i < multiPkt.Updates.Count; i++)
                {
                    var (pos, stateId) = multiPkt.Updates[i];
                    var st = new BlockState(stateId);
                    if (worldCache.GetBlockState(pos) != st && worldCache.ApplyBlockUpdate(pos, st) && multiPkt.Updates.Count <= 24)
                    {
                        worldCache.RemeshAroundBlockImmediate(pos, renderer);
                    }
                }

                break;

            case WorldTimeUpdatePacket timePkt:
                worldBridge.TimeCycle.SetTime(timePkt.WorldTimeTicks);
                break;

            case SystemChatPacket chatPkt:
                ClientLog.SystemChatReceived(logger, chatPkt.Message);
                chatHistory.AddMessage(chatPkt.Message);
                break;
        }
    }

    private static void ApplyInventoryOrWorldEvent(
        InventoryGrantAndEventPacket pkt,
        ClientPlayerController player,
        ClientWorldSyncAndMobBridge worldBridge,
        IRegistry<ItemDefinition> items,
        IAudioSubsystem audio)
    {
        switch (pkt.EventKind)
        {
            case 0: // ItemGrant (picked up 3D DroppedItem on server!)
                if (pkt.GrantedItemId > 0 && pkt.GrantedCount > 0)
                {
                    _ = player.Hotbar.SurvivalInventory.AddItem(new ItemStack(pkt.GrantedItemId, pkt.GrantedCount), items);
                    player.Hotbar.RefreshSlotVisuals(player.Mode);
                    audio.PlayPositional(SoundCue.ItemPop, player.Camera.Position, gain: 0.58f * player.MasterVolume, pitch: 1.06f);
                }

                break;

            case 1: // Explosion
                worldBridge.Particles.EmitExplosion(pkt.EventPosition);
                audio.PlayPositional(SoundCue.Explosion, pkt.EventPosition, gain: 1.0f * player.MasterVolume);
                break;

            case 2: // ShieldBlock
                audio.PlayPositional(SoundCue.ShieldBlock, pkt.EventPosition, gain: 0.90f * player.MasterVolume);
                break;

            case 3: // FuseHiss (Creeper or TNT)
                var fuseCue = pkt.GrantedCount == 1 ? SoundCue.MobCreeperHiss : SoundCue.TntFuse;
                audio.PlayPositional(fuseCue, pkt.EventPosition, gain: 0.92f * player.MasterVolume);
                worldBridge.Particles.EmitFlameAndSmoke(pkt.EventPosition + new Vec3d(0.0, 1.1, 0.0));
                break;

            case 4: // ArrowHit
                audio.PlayPositional(SoundCue.ArrowHit, pkt.EventPosition, gain: 0.80f * player.MasterVolume);
                break;

            case 5: // RidingCartSync
                if (pkt.GrantedCount <= 0)
                {
                    player.DismountMinecart();
                }
                else
                {
                    player.MountMinecart(pkt.GrantedCount);
                }

                break;
        }
    }
}
