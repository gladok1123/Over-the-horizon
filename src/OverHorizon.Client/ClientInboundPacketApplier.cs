using Microsoft.Extensions.Logging;
using OverHorizon.Audio;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Networking.Packets;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Rendering;
using OverHorizon.World.Serialization;

namespace OverHorizon.Client;

/// <summary>
/// Applies inbound authoritative server packets to the client world cache, player pose, PvP combat state, remote avatars, and chat log.
/// </summary>
internal static class ClientInboundPacketApplier
{
    public static void Apply(
        IClientboundPacket packet,
        long localEntityId,
        ClientRemotePlayerTracker remotePlayers,
        ClientPlayerController player,
        ClientWorldCache worldCache,
        IBlockRegistry blocks,
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
                    audio.PlayPositional(SoundCue.PlayerHit, peerPos + new Vec3d(0.0, 1.0, 0.0), gain: 0.85f * player.MasterVolume);
                }

                break;

            case ChunkDataPacket chunkPkt:
                var col = ChunkNetworkSerializer.DeserializeColumn(chunkPkt.Position, chunkPkt.Payload.Span, blocks);
                worldCache.UpsertColumn(col);
                _ = worldCache.EvictOutOfRangeChunks(player.FeetPosition.ToBlockPos().ToChunkPos(), activeViewDistance + 4, renderer);
                break;

            case BlockUpdatePacket blockPkt:
                var newState = new BlockState(blockPkt.BlockStateId);
                if (worldCache.GetBlockState(blockPkt.Position) != newState &&
                    worldCache.ApplyBlockUpdate(blockPkt.Position, newState))
                {
                    var soundPos = new Vec3d(blockPkt.Position.X + 0.5, blockPkt.Position.Y + 0.5, blockPkt.Position.Z + 0.5);
                    audio.PlayPositional(newState.IsAir ? SoundCue.BlockBreak : SoundCue.BlockPlace, soundPos, gain: 0.7f * player.MasterVolume);
                }

                break;

            case SystemChatPacket chatPkt:
                ClientLog.SystemChatReceived(logger, chatPkt.Message);
                chatHistory.AddMessage(chatPkt.Message);
                break;
        }
    }
}
