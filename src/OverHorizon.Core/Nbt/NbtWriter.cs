using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace OverHorizon.Core.Nbt;

/// <summary>
/// Streaming big-endian binary writer for Named Binary Tag (NBT) trees using <see cref="IBufferWriter{T}"/>.
/// </summary>
public static class NbtWriter
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Writes a named root <see cref="NbtCompoundTag"/> into <paramref name="writer"/>.
    /// </summary>
    /// <param name="writer">Target byte buffer writer.</param>
    /// <param name="rootName">Root tag name (often empty string).</param>
    /// <param name="root">Root compound tag.</param>
    public static void WriteRoot(IBufferWriter<byte> writer, string rootName, NbtCompoundTag root)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(rootName);
        ArgumentNullException.ThrowIfNull(root);

        WriteByte(writer, (byte)NbtTagType.CompoundTag);
        WriteStringPayload(writer, rootName);
        WriteCompoundPayload(writer, root, depth: 0);
    }

    /// <summary>
    /// Serializes a named root <see cref="NbtCompoundTag"/> into a newly allocated byte array.
    /// </summary>
    /// <param name="rootName">Root tag name.</param>
    /// <param name="root">Root compound tag.</param>
    /// <returns>Serialized big-endian NBT bytes.</returns>
    public static byte[] WriteToBytes(string rootName, NbtCompoundTag root)
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        WriteRoot(buffer, rootName, root);
        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteTagPayload(IBufferWriter<byte> writer, NbtTag tag, int depth)
    {
        if (depth > NbtReader.DefaultMaxDepth)
        {
            throw new NbtFormatException($"NBT nesting depth exceeded limit ({NbtReader.DefaultMaxDepth}).");
        }

        switch (tag)
        {
            case NbtByteTag b:
                WriteByte(writer, unchecked((byte)b.Value));
                break;
            case NbtShortTag s:
                WriteInt16(writer, s.Value);
                break;
            case NbtIntTag i:
                WriteInt32(writer, i.Value);
                break;
            case NbtLongTag l:
                WriteInt64(writer, l.Value);
                break;
            case NbtFloatTag f:
                WriteSingle(writer, f.Value);
                break;
            case NbtDoubleTag d:
                WriteDouble(writer, d.Value);
                break;
            case NbtByteArrayTag ba:
                WriteInt32(writer, ba.Values.Length);
                writer.Write(ba.Values.AsSpan());
                break;
            case NbtStringTag str:
                WriteStringPayload(writer, str.Value);
                break;
            case NbtListTag list:
                WriteByte(writer, (byte)(list.Items.IsEmpty ? NbtTagType.End : list.ElementType));
                WriteInt32(writer, list.Items.Length);
                foreach (var item in list.Items)
                {
                    WriteTagPayload(writer, item, depth + 1);
                }

                break;
            case NbtCompoundTag compound:
                WriteCompoundPayload(writer, compound, depth + 1);
                break;
            case NbtIntArrayTag ia:
                WriteInt32(writer, ia.Values.Length);
                foreach (var v in ia.Values)
                {
                    WriteInt32(writer, v);
                }

                break;
            case NbtLongArrayTag la:
                WriteInt32(writer, la.Values.Length);
                foreach (var v in la.Values)
                {
                    WriteInt64(writer, v);
                }

                break;
            default:
                throw new NbtFormatException($"Unsupported NBT tag type '{tag.TagType}'.");
        }
    }

    private static void WriteCompoundPayload(IBufferWriter<byte> writer, NbtCompoundTag compound, int depth)
    {
        foreach (var (name, child) in compound.Entries)
        {
            WriteByte(writer, (byte)child.TagType);
            WriteStringPayload(writer, name);
            WriteTagPayload(writer, child, depth);
        }

        WriteByte(writer, (byte)NbtTagType.End);
    }

    private static void WriteStringPayload(IBufferWriter<byte> writer, string value)
    {
        int byteCount;
        try
        {
            byteCount = StrictUtf8.GetByteCount(value);
        }
        catch (EncoderFallbackException ex)
        {
            throw new NbtFormatException("Invalid Unicode surrogate sequence in NBT string.", ex);
        }

        if (byteCount > ushort.MaxValue)
        {
            throw new NbtFormatException($"NBT string UTF-8 byte length {byteCount} exceeds 65,535.");
        }

        var span = writer.GetSpan(sizeof(ushort) + byteCount);
        BinaryPrimitives.WriteUInt16BigEndian(span, (ushort)byteCount);
        _ = StrictUtf8.GetBytes(value.AsSpan(), span[sizeof(ushort)..]);
        writer.Advance(sizeof(ushort) + byteCount);
    }

    private static void WriteByte(IBufferWriter<byte> writer, byte value)
    {
        var span = writer.GetSpan(1);
        span[0] = value;
        writer.Advance(1);
    }

    private static void WriteInt16(IBufferWriter<byte> writer, short value)
    {
        var span = writer.GetSpan(sizeof(short));
        BinaryPrimitives.WriteInt16BigEndian(span, value);
        writer.Advance(sizeof(short));
    }

    private static void WriteInt32(IBufferWriter<byte> writer, int value)
    {
        var span = writer.GetSpan(sizeof(int));
        BinaryPrimitives.WriteInt32BigEndian(span, value);
        writer.Advance(sizeof(int));
    }

    private static void WriteInt64(IBufferWriter<byte> writer, long value)
    {
        var span = writer.GetSpan(sizeof(long));
        BinaryPrimitives.WriteInt64BigEndian(span, value);
        writer.Advance(sizeof(long));
    }

    private static void WriteSingle(IBufferWriter<byte> writer, float value)
    {
        var span = writer.GetSpan(sizeof(float));
        BinaryPrimitives.WriteSingleBigEndian(span, value);
        writer.Advance(sizeof(float));
    }

    private static void WriteDouble(IBufferWriter<byte> writer, double value)
    {
        var span = writer.GetSpan(sizeof(double));
        BinaryPrimitives.WriteDoubleBigEndian(span, value);
        writer.Advance(sizeof(double));
    }
}
