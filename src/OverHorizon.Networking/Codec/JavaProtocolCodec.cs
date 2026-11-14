using System.Buffers;
using OverHorizon.Core.Math;
using OverHorizon.Networking.Framing;
using OverHorizon.Networking.Packets;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Networking.Packets.Serverbound;
using OverHorizon.Networking.Primitives;

namespace OverHorizon.Networking.Codec;

/// <summary>
/// Clean-room Java-style protocol codec implementing <c>Handshaking</c>, <c>Status</c>, <c>Login</c>, and <c>Play</c> states.
/// </summary>
public sealed partial class JavaProtocolCodec : IProtocolCodec
{
    /// <summary>Default MVP wire protocol version identifier.</summary>
    public const int CurrentProtocolVersion = 767;

    /// <inheritdoc />
    public ProtocolEdition Edition => ProtocolEdition.Java;

    /// <inheritdoc />
    public void EncodeServerbound(ConnectionState state, IServerboundPacket packet, IBufferWriter<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(destination);

        if (packet.ValidState != state)
        {
            throw new ProtocolException($"Cannot encode {packet.GetType().Name} while in state {state}.");
        }

        switch (packet)
        {
            case HandshakePacket hs:
                PacketBufferWriter.WriteVarInt(destination, 0x00);
                PacketBufferWriter.WriteVarInt(destination, hs.ProtocolVersion);
                PacketBufferWriter.WriteUtf8(destination, hs.ServerAddress, maxChars: 255);
                PacketBufferWriter.WriteInt16(destination, unchecked((short)hs.ServerPort));
                PacketBufferWriter.WriteVarInt(destination, (int)hs.Intent);
                break;

            case StatusRequestPacket:
                PacketBufferWriter.WriteVarInt(destination, 0x00);
                break;

            case PingRequestPacket ping:
                PacketBufferWriter.WriteVarInt(destination, 0x01);
                PacketBufferWriter.WriteInt64(destination, ping.ClientTimestamp);
                break;

            case LoginStartPacket login:
                PacketBufferWriter.WriteVarInt(destination, 0x00);
                PacketBufferWriter.WriteUtf8(destination, login.Username, maxChars: 16);
                PacketBufferWriter.WriteUuid(destination, login.ClientUuid);
                break;

            case KeepAliveResponsePacket ka:
                PacketBufferWriter.WriteVarInt(destination, 0x00);
                PacketBufferWriter.WriteInt64(destination, ka.KeepAliveId);
                break;

            case MovePlayerPacket move:
                PacketBufferWriter.WriteVarInt(destination, 0x01);
                PacketBufferWriter.WriteFloat64(destination, move.Position.X);
                PacketBufferWriter.WriteFloat64(destination, move.Position.Y);
                PacketBufferWriter.WriteFloat64(destination, move.Position.Z);
                PacketBufferWriter.WriteFloat32(destination, move.YawDegrees);
                PacketBufferWriter.WriteFloat32(destination, move.PitchDegrees);
                PacketBufferWriter.WriteBool(destination, move.OnGround);
                break;

            case PlayerBlockActionPacket blockAct:
                PacketBufferWriter.WriteVarInt(destination, 0x02);
                PacketBufferWriter.WriteVarInt(destination, (int)blockAct.Action);
                PacketBufferWriter.WriteBlockPos(destination, blockAct.TargetPos);
                PacketBufferWriter.WriteUInt8(destination, (byte)blockAct.Face);
                PacketBufferWriter.WriteVarInt(destination, blockAct.BlockStateId);
                break;

            case ChatMessagePacket chat:
                PacketBufferWriter.WriteVarInt(destination, 0x03);
                PacketBufferWriter.WriteUtf8(destination, chat.Message, maxChars: 256);
                break;

            case PlayerAttackEntityPacket attack:
                PacketBufferWriter.WriteVarInt(destination, 0x04);
                PacketBufferWriter.WriteVarLong(destination, attack.TargetEntityId);
                PacketBufferWriter.WriteVarInt(destination, attack.HeldItemId);
                PacketBufferWriter.WriteBool(destination, attack.IsCriticalHit);
                break;

            case PlayerEquipmentAndModePacket equip:
                EncodePlayerEquipmentAndMode(destination, equip);
                break;

            case PlayerWorldInteractionPacket interact:
                EncodePlayerWorldInteraction(destination, interact);
                break;

            default:
                throw new ProtocolException($"Unsupported serverbound packet type '{packet.GetType().Name}'.");
        }
    }

    /// <inheritdoc />
    public IServerboundPacket DecodeServerbound(ConnectionState state, ReadOnlySpan<byte> payload)
    {
        var reader = new PacketSpanReader(payload);
        var packetId = reader.ReadVarInt();

        IServerboundPacket decoded = (state, packetId) switch
        {
            (ConnectionState.Handshaking, 0x00) => DecodeHandshake(ref reader),
            (ConnectionState.Status, 0x00) => StatusRequestPacket.Instance,
            (ConnectionState.Status, 0x01) => new PingRequestPacket(reader.ReadInt64()),
            (ConnectionState.Login, 0x00) => DecodeLoginStart(ref reader),
            (ConnectionState.Play, 0x00) => new KeepAliveResponsePacket(reader.ReadInt64()),
            (ConnectionState.Play, 0x01) => DecodeMovePlayer(ref reader),
            (ConnectionState.Play, 0x02) => DecodeBlockAction(ref reader),
            (ConnectionState.Play, 0x03) => DecodeChat(ref reader),
            (ConnectionState.Play, 0x04) => DecodeAttackEntity(ref reader),
            (ConnectionState.Play, 0x05) => DecodePlayerEquipmentAndMode(ref reader),
            (ConnectionState.Play, 0x06) => DecodePlayerWorldInteraction(ref reader),
            _ => throw new ProtocolException($"Unknown serverbound packet ID 0x{packetId:X2} in state {state}."),
        };

        reader.EnsureEndOfPacket();
        return decoded;
    }

    /// <inheritdoc />
    public void EncodeClientbound(ConnectionState state, IClientboundPacket packet, IBufferWriter<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(destination);

        if (packet.ValidState != state)
        {
            throw new ProtocolException($"Cannot encode {packet.GetType().Name} while in state {state}.");
        }

        switch (packet)
        {
            case StatusResponsePacket status:
                PacketBufferWriter.WriteVarInt(destination, 0x00);
                PacketBufferWriter.WriteUtf8(destination, status.JsonPayload, maxChars: 32767);
                break;

            case PongResponsePacket pong:
                PacketBufferWriter.WriteVarInt(destination, 0x01);
                PacketBufferWriter.WriteInt64(destination, pong.ClientTimestamp);
                break;

            case DisconnectPacket disconnect when state is ConnectionState.Login or ConnectionState.Play:
                PacketBufferWriter.WriteVarInt(destination, 0x00);
                PacketBufferWriter.WriteUtf8(destination, disconnect.Reason, maxChars: 2048);
                break;

            case SetCompressionPacket compression:
                PacketBufferWriter.WriteVarInt(destination, 0x01);
                PacketBufferWriter.WriteVarInt(destination, compression.ThresholdBytes);
                break;

            case LoginSuccessPacket success:
                PacketBufferWriter.WriteVarInt(destination, 0x02);
                PacketBufferWriter.WriteUuid(destination, success.PlayerUuid);
                PacketBufferWriter.WriteUtf8(destination, success.Username, maxChars: 16);
                PacketBufferWriter.WriteVarLong(destination, success.AssignedEntityId);
                break;

            case KeepAliveRequestPacket keepAlive:
                PacketBufferWriter.WriteVarInt(destination, 0x01);
                PacketBufferWriter.WriteInt64(destination, keepAlive.KeepAliveId);
                break;

            case SynchronizePlayerPositionPacket syncPos:
                PacketBufferWriter.WriteVarInt(destination, 0x02);
                PacketBufferWriter.WriteFloat64(destination, syncPos.Position.X);
                PacketBufferWriter.WriteFloat64(destination, syncPos.Position.Y);
                PacketBufferWriter.WriteFloat64(destination, syncPos.Position.Z);
                PacketBufferWriter.WriteFloat32(destination, syncPos.YawDegrees);
                PacketBufferWriter.WriteFloat32(destination, syncPos.PitchDegrees);
                PacketBufferWriter.WriteVarInt(destination, syncPos.TeleportId);
                break;

            case BlockUpdatePacket blockUpdate:
                PacketBufferWriter.WriteVarInt(destination, 0x03);
                PacketBufferWriter.WriteBlockPos(destination, blockUpdate.Position);
                PacketBufferWriter.WriteVarInt(destination, blockUpdate.BlockStateId);
                break;

            case ChunkDataPacket chunkData:
                PacketBufferWriter.WriteVarInt(destination, 0x04);
                PacketBufferWriter.WriteInt32(destination, chunkData.Position.X);
                PacketBufferWriter.WriteInt32(destination, chunkData.Position.Z);
                PacketBufferWriter.WriteByteSpan(destination, chunkData.Payload.Span);
                break;

            case SystemChatPacket sysChat:
                PacketBufferWriter.WriteVarInt(destination, 0x05);
                PacketBufferWriter.WriteUtf8(destination, sysChat.Message, maxChars: 4096);
                PacketBufferWriter.WriteBool(destination, sysChat.IsOverlay);
                break;

            case RemotePlayerSpawnPacket remoteSpawn:
                PacketBufferWriter.WriteVarInt(destination, 0x06);
                PacketBufferWriter.WriteVarLong(destination, remoteSpawn.EntityId);
                PacketBufferWriter.WriteUtf8(destination, remoteSpawn.Username, maxChars: 16);
                PacketBufferWriter.WriteFloat64(destination, remoteSpawn.Position.X);
                PacketBufferWriter.WriteFloat64(destination, remoteSpawn.Position.Y);
                PacketBufferWriter.WriteFloat64(destination, remoteSpawn.Position.Z);
                PacketBufferWriter.WriteFloat32(destination, remoteSpawn.YawDegrees);
                PacketBufferWriter.WriteFloat32(destination, remoteSpawn.PitchDegrees);
                break;

            case RemotePlayerMovePacket remoteMove:
                PacketBufferWriter.WriteVarInt(destination, 0x07);
                PacketBufferWriter.WriteVarLong(destination, remoteMove.EntityId);
                PacketBufferWriter.WriteFloat64(destination, remoteMove.Position.X);
                PacketBufferWriter.WriteFloat64(destination, remoteMove.Position.Y);
                PacketBufferWriter.WriteFloat64(destination, remoteMove.Position.Z);
                PacketBufferWriter.WriteFloat32(destination, remoteMove.YawDegrees);
                PacketBufferWriter.WriteFloat32(destination, remoteMove.PitchDegrees);
                break;

            case RemotePlayerDespawnPacket remoteDespawn:
                PacketBufferWriter.WriteVarInt(destination, 0x08);
                PacketBufferWriter.WriteVarLong(destination, remoteDespawn.EntityId);
                break;

            case EntityCombatEventPacket combat:
                PacketBufferWriter.WriteVarInt(destination, 0x09);
                PacketBufferWriter.WriteVarLong(destination, combat.TargetEntityId);
                PacketBufferWriter.WriteVarLong(destination, combat.AttackerEntityId);
                PacketBufferWriter.WriteFloat32(destination, combat.NewHealth);
                PacketBufferWriter.WriteFloat64(destination, combat.KnockbackVelocity.X);
                PacketBufferWriter.WriteFloat64(destination, combat.KnockbackVelocity.Y);
                PacketBufferWriter.WriteFloat64(destination, combat.KnockbackVelocity.Z);
                PacketBufferWriter.WriteBool(destination, combat.TargetDied);
                break;

            case MultiBlockUpdatePacket multiBlock:
                PacketBufferWriter.WriteVarInt(destination, 0x0A);
                PacketBufferWriter.WriteVarInt(destination, multiBlock.Updates.Count);
                for (var i = 0; i < multiBlock.Updates.Count; i++)
                {
                    var (pos, stateId) = multiBlock.Updates[i];
                    PacketBufferWriter.WriteBlockPos(destination, pos);
                    PacketBufferWriter.WriteVarInt(destination, stateId);
                }

                break;

            case WorldTimeUpdatePacket worldTime:
                PacketBufferWriter.WriteVarInt(destination, 0x0B);
                PacketBufferWriter.WriteInt64(destination, worldTime.WorldTimeTicks);
                break;

            case RemotePlayerEquipmentPacket remoteEquip:
                EncodeRemotePlayerEquipment(destination, remoteEquip);
                break;

            case WorldEntitiesSnapshotPacket entitiesSnap:
                EncodeWorldEntitiesSnapshot(destination, entitiesSnap);
                break;

            case InventoryGrantAndEventPacket grantEv:
                EncodeInventoryGrantAndEvent(destination, grantEv);
                break;

            case PlayerDimensionPacket playerDimension:
                PacketBufferWriter.WriteVarInt(destination, 0x0F);
                PacketBufferWriter.WriteVarInt(destination, (int)playerDimension.Dimension);
                PacketBufferWriter.WriteFloat64(destination, playerDimension.Position.X);
                PacketBufferWriter.WriteFloat64(destination, playerDimension.Position.Y);
                PacketBufferWriter.WriteFloat64(destination, playerDimension.Position.Z);
                PacketBufferWriter.WriteFloat32(destination, playerDimension.YawDegrees);
                PacketBufferWriter.WriteFloat32(destination, playerDimension.PitchDegrees);
                break;

            default:
                throw new ProtocolException($"Unsupported clientbound packet type '{packet.GetType().Name}'.");
        }
    }

    /// <inheritdoc />
    public IClientboundPacket DecodeClientbound(ConnectionState state, ReadOnlySpan<byte> payload)
    {
        var reader = new PacketSpanReader(payload);
        var packetId = reader.ReadVarInt();

        IClientboundPacket decoded = (state, packetId) switch
        {
            (ConnectionState.Status, 0x00) => new StatusResponsePacket(reader.ReadUtf8(maxChars: 32767)),
            (ConnectionState.Status, 0x01) => new PongResponsePacket(reader.ReadInt64()),
            (ConnectionState.Login, 0x00) => new DisconnectPacket(ConnectionState.Login, reader.ReadUtf8(maxChars: 2048)),
            (ConnectionState.Login, 0x01) => new SetCompressionPacket(reader.ReadVarInt()),
            (ConnectionState.Login, 0x02) => new LoginSuccessPacket(
                reader.ReadUuid(),
                reader.ReadUtf8(maxChars: 16),
                reader.ReadVarLong()),
            (ConnectionState.Play, 0x00) => new DisconnectPacket(ConnectionState.Play, reader.ReadUtf8(maxChars: 2048)),
            (ConnectionState.Play, 0x01) => new KeepAliveRequestPacket(reader.ReadInt64()),
            (ConnectionState.Play, 0x02) => new SynchronizePlayerPositionPacket(
                new Vec3d(reader.ReadFloat64(), reader.ReadFloat64(), reader.ReadFloat64()),
                reader.ReadFloat32(),
                reader.ReadFloat32(),
                reader.ReadVarInt()),
            (ConnectionState.Play, 0x03) => DecodeBlockUpdate(ref reader),
            (ConnectionState.Play, 0x04) => new ChunkDataPacket(
                new ChunkPos(reader.ReadInt32(), reader.ReadInt32()),
                reader.ReadByteArray(maxBytes: PacketFramer.MaxUncompressedPayloadBytes)),
            (ConnectionState.Play, 0x05) => new SystemChatPacket(
                reader.ReadUtf8(maxChars: 4096),
                reader.ReadBool()),
            (ConnectionState.Play, 0x06) => new RemotePlayerSpawnPacket(
                reader.ReadVarLong(),
                reader.ReadUtf8(maxChars: 16),
                new Vec3d(reader.ReadFloat64(), reader.ReadFloat64(), reader.ReadFloat64()),
                reader.ReadFloat32(),
                reader.ReadFloat32()),
            (ConnectionState.Play, 0x07) => new RemotePlayerMovePacket(
                reader.ReadVarLong(),
                new Vec3d(reader.ReadFloat64(), reader.ReadFloat64(), reader.ReadFloat64()),
                reader.ReadFloat32(),
                reader.ReadFloat32()),
            (ConnectionState.Play, 0x08) => new RemotePlayerDespawnPacket(reader.ReadVarLong()),
            (ConnectionState.Play, 0x09) => DecodeCombatEvent(ref reader),
            (ConnectionState.Play, 0x0A) => DecodeMultiBlockUpdate(ref reader),
            (ConnectionState.Play, 0x0B) => new WorldTimeUpdatePacket(reader.ReadInt64()),
            (ConnectionState.Play, 0x0C) => DecodeRemotePlayerEquipment(ref reader),
            (ConnectionState.Play, 0x0D) => DecodeWorldEntitiesSnapshot(ref reader),
            (ConnectionState.Play, 0x0E) => DecodeInventoryGrantAndEvent(ref reader),
            (ConnectionState.Play, 0x0F) => new PlayerDimensionPacket(
                (OverHorizon.Core.DimensionId)reader.ReadVarInt(),
                new Vec3d(reader.ReadFloat64(), reader.ReadFloat64(), reader.ReadFloat64()),
                reader.ReadFloat32(),
                reader.ReadFloat32()),
            _ => throw new ProtocolException($"Unknown clientbound packet ID 0x{packetId:X2} in state {state}."),
        };

        reader.EnsureEndOfPacket();
        return decoded;
    }

    private static HandshakePacket DecodeHandshake(ref PacketSpanReader reader)
    {
        var protocolVersion = reader.ReadVarInt();
        var address = reader.ReadUtf8(maxChars: 255);
        var port = unchecked((ushort)reader.ReadInt16());
        var intentRaw = reader.ReadVarInt();
        var intent = intentRaw switch
        {
            1 => HandshakeIntent.Status,
            2 => HandshakeIntent.Login,
            _ => throw new ProtocolException($"Invalid handshake intent {intentRaw}."),
        };

        return new HandshakePacket(protocolVersion, address, port, intent);
    }

    private static LoginStartPacket DecodeLoginStart(ref PacketSpanReader reader)
    {
        var username = reader.ReadUtf8(maxChars: 16);
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ProtocolException("Username cannot be empty or whitespace.");
        }

        var uuid = reader.ReadUuid();
        return new LoginStartPacket(username, uuid);
    }

    private static MovePlayerPacket DecodeMovePlayer(ref PacketSpanReader reader)
    {
        var x = reader.ReadFloat64();
        var y = reader.ReadFloat64();
        var z = reader.ReadFloat64();
        var yaw = reader.ReadFloat32();
        var pitch = reader.ReadFloat32();
        var onGround = reader.ReadBool();

        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z) ||
            !float.IsFinite(yaw) || !float.IsFinite(pitch))
        {
            throw new ProtocolException("Non-finite floating-point coordinate or rotation in MovePlayerPacket.");
        }

        return new MovePlayerPacket(new Vec3d(x, y, z), yaw, pitch, onGround);
    }

    private static PlayerBlockActionPacket DecodeBlockAction(ref PacketSpanReader reader)
    {
        var actionRaw = reader.ReadVarInt();
        if (actionRaw is < 0 or > 1)
        {
            throw new ProtocolException($"Invalid PlayerBlockActionKind {actionRaw}.");
        }

        var pos = reader.ReadBlockPos();
        var faceRaw = reader.ReadUInt8();
        if (faceRaw > (byte)Direction.East)
        {
            throw new ProtocolException($"Invalid block face {faceRaw}.");
        }

        var stateId = reader.ReadVarInt();
        if ((uint)stateId > ushort.MaxValue)
        {
            throw new ProtocolException($"Invalid BlockStateId {stateId}.");
        }

        return new PlayerBlockActionPacket(
            (PlayerBlockActionKind)actionRaw,
            pos,
            (Direction)faceRaw,
            (ushort)stateId);
    }

    private static ChatMessagePacket DecodeChat(ref PacketSpanReader reader)
    {
        var msg = reader.ReadUtf8(maxChars: 256);
        if (string.IsNullOrWhiteSpace(msg))
        {
            throw new ProtocolException("Chat message cannot be empty.");
        }

        return new ChatMessagePacket(msg);
    }

    private static BlockUpdatePacket DecodeBlockUpdate(ref PacketSpanReader reader)
    {
        var pos = reader.ReadBlockPos();
        var stateId = reader.ReadVarInt();
        if ((uint)stateId > ushort.MaxValue)
        {
            throw new ProtocolException($"Invalid BlockStateId {stateId} in BlockUpdatePacket.");
        }

        return new BlockUpdatePacket(pos, (ushort)stateId);
    }

    private static PlayerAttackEntityPacket DecodeAttackEntity(ref PacketSpanReader reader)
    {
        var targetId = reader.ReadVarLong();
        var itemId = reader.ReadVarInt();
        if ((uint)itemId > ushort.MaxValue)
        {
            throw new ProtocolException($"Invalid HeldItemId {itemId} in PlayerAttackEntityPacket.");
        }

        var crit = reader.ReadBool();
        return new PlayerAttackEntityPacket(targetId, (ushort)itemId, crit);
    }

    private static EntityCombatEventPacket DecodeCombatEvent(ref PacketSpanReader reader)
    {
        var targetId = reader.ReadVarLong();
        var attackerId = reader.ReadVarLong();
        var health = reader.ReadFloat32();
        var kx = reader.ReadFloat64();
        var ky = reader.ReadFloat64();
        var kz = reader.ReadFloat64();
        var died = reader.ReadBool();
        if (!float.IsFinite(health) || !double.IsFinite(kx) || !double.IsFinite(ky) || !double.IsFinite(kz))
        {
            throw new ProtocolException("Non-finite float in EntityCombatEventPacket.");
        }

        return new EntityCombatEventPacket(targetId, attackerId, health, new Vec3d(kx, ky, kz), died);
    }

    private static MultiBlockUpdatePacket DecodeMultiBlockUpdate(ref PacketSpanReader reader)
    {
        var count = reader.ReadVarInt();
        if ((uint)count > 8192U)
        {
            throw new ProtocolException($"MultiBlockUpdatePacket count {count} exceeds maximum.");
        }

        var list = new (BlockPos Position, ushort BlockStateId)[count];
        for (var i = 0; i < count; i++)
        {
            var pos = reader.ReadBlockPos();
            var stateId = reader.ReadVarInt();
            if ((uint)stateId > ushort.MaxValue)
            {
                throw new ProtocolException($"Invalid BlockStateId {stateId} in MultiBlockUpdatePacket.");
            }

            list[i] = (pos, (ushort)stateId);
        }

        return new MultiBlockUpdatePacket(list);
    }
}
