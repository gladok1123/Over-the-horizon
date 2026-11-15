using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;

namespace OverHorizon.Networking.Primitives;

/// <summary>
/// Allocation-light big-endian wire primitive writer over any <see cref="IBufferWriter{T}"/>.
/// </summary>
public static class PacketBufferWriter
{
    /// <summary>Default maximum character count allowed in a protocol UTF-8 string.</summary>
    public const int DefaultMaxStringChars = 32767;

    /// <summary>Writes a single unsigned 8-bit integer.</summary>
    /// <param name="writer">Target buffer writer.</param>
    /// <param name="octet">Byte value.</param>
    public static void WriteUInt8(IBufferWriter<byte> writer, byte octet)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var span = writer.GetSpan(1);
        span[0] = octet;
        writer.Advance(1);
    }

    /// <summary>Writes a protocol boolean (<c>0x01</c> or <c>0x00</c>).</summary>
    /// <param name="writer">Target buffer writer.</param>
    /// <param name="flag">Boolean value.</param>
    public static void WriteBool(IBufferWriter<byte> writer, bool flag) =>
        WriteUInt8(writer, flag ? (byte)1 : (byte)0);

    /// <summary>Writes a signed 16-bit big-endian integer.</summary>
    /// <param name="writer">Target buffer writer.</param>
    /// <param name="number">16-bit integer.</param>
    public static void WriteInt16(IBufferWriter<byte> writer, short number)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var span = writer.GetSpan(2);
        BinaryPrimitives.WriteInt16BigEndian(span, number);
        writer.Advance(2);
    }

    /// <summary>Writes a signed 32-bit big-endian integer.</summary>
    /// <param name="writer">Target buffer writer.</param>
    /// <param name="number">32-bit integer.</param>
    public static void WriteInt32(IBufferWriter<byte> writer, int number)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var span = writer.GetSpan(4);
        BinaryPrimitives.WriteInt32BigEndian(span, number);
        writer.Advance(4);
    }

    /// <summary>Writes a signed 64-bit big-endian integer.</summary>
    /// <param name="writer">Target buffer writer.</param>
    /// <param name="number">64-bit integer.</param>
    public static void WriteInt64(IBufferWriter<byte> writer, long number)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var span = writer.GetSpan(8);
        BinaryPrimitives.WriteInt64BigEndian(span, number);
        writer.Advance(8);
    }

    /// <summary>Writes an IEEE-754 32-bit big-endian float.</summary>
    /// <param name="writer">Target buffer writer.</param>
    /// <param name="number">32-bit float.</param>
    public static void WriteFloat32(IBufferWriter<byte> writer, float number)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var span = writer.GetSpan(4);
        BinaryPrimitives.WriteSingleBigEndian(span, number);
        writer.Advance(4);
    }

    /// <summary>Writes an IEEE-754 64-bit big-endian double.</summary>
    /// <param name="writer">Target buffer writer.</param>
    /// <param name="number">64-bit double.</param>
    public static void WriteFloat64(IBufferWriter<byte> writer, double number)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var span = writer.GetSpan(8);
        BinaryPrimitives.WriteDoubleBigEndian(span, number);
        writer.Advance(8);
    }

    /// <summary>Writes a 32-bit <c>VarInt</c>.</summary>
    /// <param name="writer">Target buffer writer.</param>
    /// <param name="number">32-bit integer.</param>
    public static void WriteVarInt(IBufferWriter<byte> writer, int number)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var span = writer.GetSpan(VarIntCodec.MaxVarIntBytes);
        var written = VarIntCodec.WriteVarInt(span, number);
        writer.Advance(written);
    }

    /// <summary>Writes a 64-bit <c>VarLong</c>.</summary>
    /// <param name="writer">Target buffer writer.</param>
    /// <param name="number">64-bit integer.</param>
    public static void WriteVarLong(IBufferWriter<byte> writer, long number)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var span = writer.GetSpan(VarIntCodec.MaxVarLongBytes);
        var written = VarIntCodec.WriteVarLong(span, number);
        writer.Advance(written);
    }

    /// <summary>Writes a 128-bit UUID in big-endian network byte order.</summary>
    /// <param name="writer">Target buffer writer.</param>
    /// <param name="id">UUID value.</param>
    public static void WriteUuid(IBufferWriter<byte> writer, Guid id)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var span = writer.GetSpan(16);
        _ = id.TryWriteBytes(span, bigEndian: true, out _);
        writer.Advance(16);
    }

    /// <summary>
    /// Writes a <c>VarInt</c>-prefixed UTF-8 string, enforcing <paramref name="maxChars"/>.
    /// </summary>
    /// <param name="writer">Target buffer writer.</param>
    /// <param name="text">UTF-16 character span.</param>
    /// <param name="maxChars">Maximum permitted UTF-16 character length.</param>
    public static void WriteUtf8(
        IBufferWriter<byte> writer,
        ReadOnlySpan<char> text,
        int maxChars = DefaultMaxStringChars)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (text.Length > maxChars)
        {
            throw new ProtocolException($"UTF-8 string length {text.Length} exceeds limit {maxChars}.");
        }

        var byteCount = Encoding.UTF8.GetByteCount(text);
        WriteVarInt(writer, byteCount);
        if (byteCount == 0)
        {
            return;
        }

        var span = writer.GetSpan(byteCount);
        var actual = Encoding.UTF8.GetBytes(text, span);
        writer.Advance(actual);
    }

    /// <summary>Writes a <c>VarInt</c>-prefixed byte span.</summary>
    /// <param name="writer">Target buffer writer.</param>
    /// <param name="payload">Raw byte span.</param>
    public static void WriteByteSpan(IBufferWriter<byte> writer, ReadOnlySpan<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(writer);
        WriteVarInt(writer, payload.Length);
        if (payload.IsEmpty)
        {
            return;
        }

        var span = writer.GetSpan(payload.Length);
        payload.CopyTo(span);
        writer.Advance(payload.Length);
    }

    /// <summary>
    /// Writes a <see cref="BlockPos"/> packed into a 64-bit big-endian integer (26-bit X, 26-bit Z, 12-bit Y).
    /// </summary>
    /// <param name="writer">Target buffer writer.</param>
    /// <param name="pos">Block coordinate.</param>
    public static void WriteBlockPos(IBufferWriter<byte> writer, BlockPos pos)
    {
        var packed =
            (((ulong)(long)pos.X & 0x3FFFFFFUL) << 38) |
            (((ulong)(long)pos.Z & 0x3FFFFFFUL) << 12) |
            ((ulong)(long)pos.Y & 0xFFFUL);
        WriteInt64(writer, unchecked((long)packed));
    }

    /// <summary>Writes a <see cref="ResourceKey"/> as <c>namespace:path</c>.</summary>
    /// <param name="writer">Target buffer writer.</param>
    /// <param name="key">Resource key.</param>
    public static void WriteResourceKey(IBufferWriter<byte> writer, ResourceKey key) =>
        WriteUtf8(writer, key.ToString(), maxChars: 512);
}
