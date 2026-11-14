using System.Buffers;
using OverHorizon.Core.Math;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Networking.Packets.Serverbound;
using OverHorizon.Networking.Primitives;

namespace OverHorizon.Networking.Codec;

/// <summary>
/// Binary serialization and validation for <c>v0.8.0</c> multiplayer equipment, interaction, entity snapshot, and grant/event packets.
/// </summary>
public sealed partial class JavaProtocolCodec
{
    private static void EncodePlayerEquipmentAndMode(IBufferWriter<byte> dst, PlayerEquipmentAndModePacket p)
    {
        PacketBufferWriter.WriteVarInt(dst, 0x05);
        PacketBufferWriter.WriteUInt8(dst, p.GameModeByte);
        PacketBufferWriter.WriteVarInt(dst, p.MainHandItemId);
        PacketBufferWriter.WriteVarInt(dst, p.OffhandItemId);
        PacketBufferWriter.WriteUInt8(dst, p.PackedArmorLower);
        PacketBufferWriter.WriteUInt8(dst, p.PackedArmorUpper);
        PacketBufferWriter.WriteUInt8(dst, p.ActionFlags);
        PacketBufferWriter.WriteUInt8(dst, p.BowChargeByte);
        PacketBufferWriter.WriteUInt8(dst, p.SwingCounter);
    }

    private static PlayerEquipmentAndModePacket DecodePlayerEquipmentAndMode(ref PacketSpanReader r)
    {
        var mode = r.ReadUInt8();
        var mainId = r.ReadVarInt();
        var offId = r.ReadVarInt();
        if ((uint)mainId > ushort.MaxValue || (uint)offId > ushort.MaxValue || mode > 2)
        {
            throw new ProtocolException("Invalid item ID or GameMode in PlayerEquipmentAndModePacket.");
        }

        return new PlayerEquipmentAndModePacket(
            mode,
            (ushort)mainId,
            (ushort)offId,
            r.ReadUInt8(),
            r.ReadUInt8(),
            r.ReadUInt8(),
            r.ReadUInt8(),
            r.ReadUInt8());
    }

    private static void EncodePlayerWorldInteraction(IBufferWriter<byte> dst, PlayerWorldInteractionPacket p)
    {
        PacketBufferWriter.WriteVarInt(dst, 0x06);
        PacketBufferWriter.WriteUInt8(dst, (byte)p.Kind);
        WriteVec3d(dst, p.Origin);
        WriteVec3d(dst, p.DirectionOrTarget);
        PacketBufferWriter.WriteVarInt(dst, p.AuxValue);
        PacketBufferWriter.WriteVarInt(dst, p.CountValue);
    }

    private static PlayerWorldInteractionPacket DecodePlayerWorldInteraction(ref PacketSpanReader r)
    {
        var kind = r.ReadUInt8();
        if (kind > (byte)PlayerWorldInteractionKind.AttackMobOrVehicle)
        {
            throw new ProtocolException($"Unknown PlayerWorldInteractionKind {kind}.");
        }

        var origin = ReadFiniteVec3d(ref r);
        var dir = ReadFiniteVec3d(ref r);
        return new PlayerWorldInteractionPacket((PlayerWorldInteractionKind)kind, origin, dir, r.ReadVarInt(), r.ReadVarInt());
    }

    private static void EncodeRemotePlayerEquipment(IBufferWriter<byte> dst, RemotePlayerEquipmentPacket p)
    {
        PacketBufferWriter.WriteVarInt(dst, 0x0C);
        PacketBufferWriter.WriteVarLong(dst, p.EntityId);
        PacketBufferWriter.WriteUInt8(dst, p.GameModeByte);
        PacketBufferWriter.WriteVarInt(dst, p.MainHandItemId);
        PacketBufferWriter.WriteVarInt(dst, p.OffhandItemId);
        PacketBufferWriter.WriteUInt8(dst, p.PackedArmorLower);
        PacketBufferWriter.WriteUInt8(dst, p.PackedArmorUpper);
        PacketBufferWriter.WriteUInt8(dst, p.ActionFlags);
        PacketBufferWriter.WriteUInt8(dst, p.BowChargeByte);
        PacketBufferWriter.WriteUInt8(dst, p.SwingCounter);
        PacketBufferWriter.WriteVarInt(dst, p.RidingMinecartId);
    }

    private static RemotePlayerEquipmentPacket DecodeRemotePlayerEquipment(ref PacketSpanReader r) =>
        new(
            r.ReadVarLong(),
            r.ReadUInt8(),
            (ushort)Math.Clamp(r.ReadVarInt(), 0, ushort.MaxValue),
            (ushort)Math.Clamp(r.ReadVarInt(), 0, ushort.MaxValue),
            r.ReadUInt8(),
            r.ReadUInt8(),
            r.ReadUInt8(),
            r.ReadUInt8(),
            r.ReadUInt8(),
            r.ReadVarInt());

    private static void EncodeInventoryGrantAndEvent(IBufferWriter<byte> dst, InventoryGrantAndEventPacket p)
    {
        PacketBufferWriter.WriteVarInt(dst, 0x0E);
        PacketBufferWriter.WriteVarInt(dst, p.GrantedItemId);
        PacketBufferWriter.WriteVarInt(dst, p.GrantedCount);
        PacketBufferWriter.WriteUInt8(dst, p.EventKind);
        WriteVec3d(dst, p.EventPosition);
    }

    private static InventoryGrantAndEventPacket DecodeInventoryGrantAndEvent(ref PacketSpanReader r) =>
        new(
            (ushort)Math.Clamp(r.ReadVarInt(), 0, ushort.MaxValue),
            r.ReadVarInt(),
            r.ReadUInt8(),
            ReadFiniteVec3d(ref r));

    private static void EncodeWorldEntitiesSnapshot(IBufferWriter<byte> dst, WorldEntitiesSnapshotPacket p)
    {
        PacketBufferWriter.WriteVarInt(dst, 0x0D);

        PacketBufferWriter.WriteVarInt(dst, p.DroppedItems.Count);
        for (var i = 0; i < p.DroppedItems.Count; i++)
        {
            var d = p.DroppedItems[i];
            PacketBufferWriter.WriteVarInt(dst, d.Id);
            PacketBufferWriter.WriteVarInt(dst, d.ItemId);
            PacketBufferWriter.WriteVarInt(dst, d.Count);
            WriteVec3d(dst, d.Position);
            WriteVec3d(dst, d.Velocity);
        }

        PacketBufferWriter.WriteVarInt(dst, p.Arrows.Count);
        for (var i = 0; i < p.Arrows.Count; i++)
        {
            var a = p.Arrows[i];
            PacketBufferWriter.WriteVarInt(dst, a.Id);
            WriteVec3d(dst, a.Pos);
            PacketBufferWriter.WriteFloat32(dst, a.Yaw);
            PacketBufferWriter.WriteFloat32(dst, a.Pitch);
            PacketBufferWriter.WriteFloat32(dst, a.Shake);
            PacketBufferWriter.WriteUInt8(dst, a.Flags);
        }

        PacketBufferWriter.WriteVarInt(dst, p.Minecarts.Count);
        for (var i = 0; i < p.Minecarts.Count; i++)
        {
            var m = p.Minecarts[i];
            PacketBufferWriter.WriteVarInt(dst, m.Id);
            WriteVec3d(dst, m.Pos);
            PacketBufferWriter.WriteFloat32(dst, m.Yaw);
            PacketBufferWriter.WriteFloat32(dst, m.Pitch);
            PacketBufferWriter.WriteFloat32(dst, m.Wobble);
            PacketBufferWriter.WriteVarLong(dst, m.RiderEntityId);
        }

        PacketBufferWriter.WriteVarInt(dst, p.Mobs.Count);
        for (var i = 0; i < p.Mobs.Count; i++)
        {
            var mob = p.Mobs[i];
            PacketBufferWriter.WriteVarInt(dst, mob.Id);
            PacketBufferWriter.WriteUInt8(dst, mob.TypeByte);
            WriteVec3d(dst, mob.FeetPos);
            PacketBufferWriter.WriteFloat32(dst, mob.Yaw);
            PacketBufferWriter.WriteFloat32(dst, mob.WalkPhase);
            PacketBufferWriter.WriteFloat32(dst, mob.HurtFlash);
            PacketBufferWriter.WriteFloat32(dst, mob.CreeperFuse);
            PacketBufferWriter.WriteUInt8(dst, mob.Flags);
        }

        PacketBufferWriter.WriteVarInt(dst, p.PrimedTnts.Count);
        for (var i = 0; i < p.PrimedTnts.Count; i++)
        {
            var t = p.PrimedTnts[i];
            PacketBufferWriter.WriteVarInt(dst, t.Id);
            WriteVec3d(dst, t.FeetPos);
            PacketBufferWriter.WriteBool(dst, t.FlashWhite);
        }

        PacketBufferWriter.WriteVarInt(dst, p.FallingBlocks.Count);
        for (var i = 0; i < p.FallingBlocks.Count; i++)
        {
            var fb = p.FallingBlocks[i];
            PacketBufferWriter.WriteVarInt(dst, fb.Id);
            PacketBufferWriter.WriteVarInt(dst, fb.BlockStateId);
            PacketBufferWriter.WriteVarInt(dst, fb.BlockX);
            PacketBufferWriter.WriteFloat64(dst, fb.CurrentY);
            PacketBufferWriter.WriteVarInt(dst, fb.BlockZ);
        }
    }

    private static WorldEntitiesSnapshotPacket DecodeWorldEntitiesSnapshot(ref PacketSpanReader r)
    {
        var dropCount = ValidateCount(r.ReadVarInt(), 256);
        var drops = new (int, ushort, int, Vec3d, Vec3d)[dropCount];
        for (var i = 0; i < dropCount; i++)
        {
            drops[i] = (r.ReadVarInt(), (ushort)Math.Clamp(r.ReadVarInt(), 0, ushort.MaxValue), r.ReadVarInt(), ReadFiniteVec3d(ref r), ReadFiniteVec3d(ref r));
        }

        var arrowCount = ValidateCount(r.ReadVarInt(), 128);
        var arrows = new (int, Vec3d, float, float, float, byte)[arrowCount];
        for (var i = 0; i < arrowCount; i++)
        {
            arrows[i] = (r.ReadVarInt(), ReadFiniteVec3d(ref r), r.ReadFloat32(), r.ReadFloat32(), r.ReadFloat32(), r.ReadUInt8());
        }

        var cartCount = ValidateCount(r.ReadVarInt(), 64);
        var carts = new (int, Vec3d, float, float, float, long)[cartCount];
        for (var i = 0; i < cartCount; i++)
        {
            carts[i] = (r.ReadVarInt(), ReadFiniteVec3d(ref r), r.ReadFloat32(), r.ReadFloat32(), r.ReadFloat32(), r.ReadVarLong());
        }

        var mobCount = ValidateCount(r.ReadVarInt(), 128);
        var mobs = new (int, byte, Vec3d, float, float, float, float, byte)[mobCount];
        for (var i = 0; i < mobCount; i++)
        {
            mobs[i] = (r.ReadVarInt(), r.ReadUInt8(), ReadFiniteVec3d(ref r), r.ReadFloat32(), r.ReadFloat32(), r.ReadFloat32(), r.ReadFloat32(), r.ReadUInt8());
        }

        var tntCount = ValidateCount(r.ReadVarInt(), 64);
        var tnts = new (int, Vec3d, bool)[tntCount];
        for (var i = 0; i < tntCount; i++)
        {
            tnts[i] = (r.ReadVarInt(), ReadFiniteVec3d(ref r), r.ReadBool());
        }

        var fbCount = ValidateCount(r.ReadVarInt(), 128);
        var falling = new (int, ushort, int, double, int)[fbCount];
        for (var i = 0; i < fbCount; i++)
        {
            falling[i] = (r.ReadVarInt(), (ushort)Math.Clamp(r.ReadVarInt(), 0, ushort.MaxValue), r.ReadVarInt(), r.ReadFloat64(), r.ReadVarInt());
        }

        return new WorldEntitiesSnapshotPacket(drops, arrows, carts, mobs, tnts, falling);
    }

    private static int ValidateCount(int count, int max) =>
        (uint)count <= (uint)max ? count : throw new ProtocolException($"Entity list count {count} exceeds maximum {max}.");

    private static void WriteVec3d(IBufferWriter<byte> dst, Vec3d v)
    {
        PacketBufferWriter.WriteFloat64(dst, v.X);
        PacketBufferWriter.WriteFloat64(dst, v.Y);
        PacketBufferWriter.WriteFloat64(dst, v.Z);
    }

    private static Vec3d ReadFiniteVec3d(ref PacketSpanReader r)
    {
        var x = r.ReadFloat64();
        var y = r.ReadFloat64();
        var z = r.ReadFloat64();
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z))
        {
            throw new ProtocolException("Non-finite Vec3d in packet.");
        }

        return new Vec3d(x, y, z);
    }
}
