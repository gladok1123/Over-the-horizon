using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace OverHorizon.Core.Nbt;

/// <summary>
/// Bounds-checked big-endian binary reader for Named Binary Tag (NBT) payloads.
/// Resistant to malformed, truncated, or deeply nested fuzz inputs.
/// </summary>
public static class NbtReader
{
    /// <summary>Default maximum nesting depth of compound and list tags.</summary>
    public const int DefaultMaxDepth = 64;

    /// <summary>Maximum number of elements allowed in a single list or array tag.</summary>
    public const int MaxCollectionElements = 1_048_576;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Reads a named root <see cref="NbtCompoundTag"/> from <paramref name="data"/>, requiring all bytes to be consumed.
    /// </summary>
    /// <param name="data">Big-endian binary NBT span.</param>
    /// <param name="maxDepth">Maximum allowed nesting depth.</param>
    /// <returns>Tuple of root name and root <see cref="NbtCompoundTag"/>.</returns>
    public static (string Name, NbtCompoundTag Root) ReadRoot(ReadOnlySpan<byte> data, int maxDepth = DefaultMaxDepth)
    {
        if (maxDepth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "Max depth must be positive.");
        }

        var offset = 0;
        var rootType = ReadTagType(data, ref offset);
        if (rootType != NbtTagType.CompoundTag)
        {
            throw new NbtFormatException($"Expected root CompoundTag (10), but found {(byte)rootType}.");
        }

        var rootName = ReadStringPayload(data, ref offset);
        var root = ReadCompoundPayload(data, ref offset, depth: 0, maxDepth);

        if (offset != data.Length)
        {
            throw new NbtFormatException($"Trailing {data.Length - offset} unexpected byte(s) after root compound.");
        }

        return (rootName, root);
    }

    /// <summary>
    /// Attempts to read a named root <see cref="NbtCompoundTag"/> without throwing on malformed input.
    /// </summary>
    /// <param name="data">Source binary span.</param>
    /// <param name="rootName">Parsed root name on success.</param>
    /// <param name="root">Parsed root compound on success.</param>
    /// <param name="maxDepth">Maximum allowed nesting depth.</param>
    /// <returns><see langword="true"/> if valid NBT; otherwise <see langword="false"/>.</returns>
    public static bool TryReadRoot(
        ReadOnlySpan<byte> data,
        out string rootName,
        [MaybeNullWhen(false)] out NbtCompoundTag root,
        int maxDepth = DefaultMaxDepth)
    {
        try
        {
            (rootName, root) = ReadRoot(data, maxDepth);
            return true;
        }
        catch (NbtFormatException)
        {
            rootName = string.Empty;
            root = null;
            return false;
        }
    }

    private static NbtTag ReadTagPayload(ReadOnlySpan<byte> data, ref int offset, NbtTagType tagType, int depth, int maxDepth)
    {
        if (depth > maxDepth)
        {
            throw new NbtFormatException($"NBT nesting depth exceeded limit ({maxDepth}).");
        }

        return tagType switch
        {
            NbtTagType.ByteTag => new NbtByteTag(unchecked((sbyte)ReadByte(data, ref offset))),
            NbtTagType.ShortTag => new NbtShortTag(ReadInt16(data, ref offset)),
            NbtTagType.IntTag => new NbtIntTag(ReadInt32(data, ref offset)),
            NbtTagType.LongTag => new NbtLongTag(ReadInt64(data, ref offset)),
            NbtTagType.FloatTag => new NbtFloatTag(ReadSingle(data, ref offset)),
            NbtTagType.DoubleTag => new NbtDoubleTag(ReadDouble(data, ref offset)),
            NbtTagType.ByteArrayTag => ReadByteArrayPayload(data, ref offset),
            NbtTagType.StringTag => new NbtStringTag(ReadStringPayload(data, ref offset)),
            NbtTagType.ListTag => ReadListPayload(data, ref offset, depth + 1, maxDepth),
            NbtTagType.CompoundTag => ReadCompoundPayload(data, ref offset, depth + 1, maxDepth),
            NbtTagType.IntArrayTag => ReadIntArrayPayload(data, ref offset),
            NbtTagType.LongArrayTag => ReadLongArrayPayload(data, ref offset),
            _ => throw new NbtFormatException($"Invalid or unsupported NBT tag type {(byte)tagType}."),
        };
    }

    private static NbtCompoundTag ReadCompoundPayload(ReadOnlySpan<byte> data, ref int offset, int depth, int maxDepth)
    {
        if (depth > maxDepth)
        {
            throw new NbtFormatException($"NBT nesting depth exceeded limit ({maxDepth}).");
        }

        var compound = new NbtCompoundTag();
        while (true)
        {
            var childType = ReadTagType(data, ref offset);
            if (childType == NbtTagType.End)
            {
                break;
            }

            var name = ReadStringPayload(data, ref offset);
            var payload = ReadTagPayload(data, ref offset, childType, depth, maxDepth);
            _ = compound.Set(name, payload);
        }

        return compound;
    }

    private static NbtListTag ReadListPayload(ReadOnlySpan<byte> data, ref int offset, int depth, int maxDepth)
    {
        var elementType = ReadTagType(data, ref offset);
        var count = ReadInt32(data, ref offset);

        if (count < 0 || count > MaxCollectionElements)
        {
            throw new NbtFormatException($"Invalid NBT list length {count}.");
        }

        if (count == 0)
        {
            return new NbtListTag(elementType);
        }

        if (elementType == NbtTagType.End)
        {
            throw new NbtFormatException("Non-empty NBT list cannot declare End (0) element type.");
        }

        if (data.Length - offset < count)
        {
            throw new NbtFormatException($"Truncated NBT list: declared {count} items, only {data.Length - offset} byte(s) remain.");
        }

        var items = new NbtTag[count];
        for (var i = 0; i < count; i++)
        {
            items[i] = ReadTagPayload(data, ref offset, elementType, depth, maxDepth);
        }

        return new NbtListTag(elementType, items);
    }

    private static NbtByteArrayTag ReadByteArrayPayload(ReadOnlySpan<byte> data, ref int offset)
    {
        var length = ReadInt32(data, ref offset);
        if (length < 0 || length > MaxCollectionElements || data.Length - offset < length)
        {
            throw new NbtFormatException($"Invalid or truncated NbtByteArray length {length}.");
        }

        var slice = data.Slice(offset, length);
        offset += length;
        return new NbtByteArrayTag(slice);
    }

    private static NbtIntArrayTag ReadIntArrayPayload(ReadOnlySpan<byte> data, ref int offset)
    {
        var length = ReadInt32(data, ref offset);
        var byteLength = (long)length * sizeof(int);
        if (length < 0 || length > MaxCollectionElements || data.Length - offset < byteLength)
        {
            throw new NbtFormatException($"Invalid or truncated NbtIntArray length {length}.");
        }

        var builder = ImmutableArray.CreateBuilder<int>(length);
        for (var i = 0; i < length; i++)
        {
            builder.Add(ReadInt32(data, ref offset));
        }

        return new NbtIntArrayTag(builder.MoveToImmutable());
    }

    private static NbtLongArrayTag ReadLongArrayPayload(ReadOnlySpan<byte> data, ref int offset)
    {
        var length = ReadInt32(data, ref offset);
        var byteLength = (long)length * sizeof(long);
        if (length < 0 || length > MaxCollectionElements || data.Length - offset < byteLength)
        {
            throw new NbtFormatException($"Invalid or truncated NbtLongArray length {length}.");
        }

        var builder = ImmutableArray.CreateBuilder<long>(length);
        for (var i = 0; i < length; i++)
        {
            builder.Add(ReadInt64(data, ref offset));
        }

        return new NbtLongArrayTag(builder.MoveToImmutable());
    }

    private static string ReadStringPayload(ReadOnlySpan<byte> data, ref int offset)
    {
        EnsureAvailable(data, offset, sizeof(ushort));
        var byteLen = BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);
        offset += sizeof(ushort);

        EnsureAvailable(data, offset, byteLen);
        var utf8Slice = data.Slice(offset, byteLen);
        offset += byteLen;

        try
        {
            return StrictUtf8.GetString(utf8Slice);
        }
        catch (DecoderFallbackException ex)
        {
            throw new NbtFormatException("Malformed UTF-8 byte sequence in NBT string.", ex);
        }
    }

    private static NbtTagType ReadTagType(ReadOnlySpan<byte> data, ref int offset)
    {
        var raw = ReadByte(data, ref offset);
        if (raw > (byte)NbtTagType.LongArrayTag)
        {
            throw new NbtFormatException($"Unknown NBT tag type discriminator {raw}.");
        }

        return (NbtTagType)raw;
    }

    private static byte ReadByte(ReadOnlySpan<byte> data, ref int offset)
    {
        EnsureAvailable(data, offset, 1);
        return data[offset++];
    }

    private static short ReadInt16(ReadOnlySpan<byte> data, ref int offset)
    {
        EnsureAvailable(data, offset, sizeof(short));
        var val = BinaryPrimitives.ReadInt16BigEndian(data[offset..]);
        offset += sizeof(short);
        return val;
    }

    private static int ReadInt32(ReadOnlySpan<byte> data, ref int offset)
    {
        EnsureAvailable(data, offset, sizeof(int));
        var val = BinaryPrimitives.ReadInt32BigEndian(data[offset..]);
        offset += sizeof(int);
        return val;
    }

    private static long ReadInt64(ReadOnlySpan<byte> data, ref int offset)
    {
        EnsureAvailable(data, offset, sizeof(long));
        var val = BinaryPrimitives.ReadInt64BigEndian(data[offset..]);
        offset += sizeof(long);
        return val;
    }

    private static float ReadSingle(ReadOnlySpan<byte> data, ref int offset)
    {
        EnsureAvailable(data, offset, sizeof(float));
        var val = BinaryPrimitives.ReadSingleBigEndian(data[offset..]);
        offset += sizeof(float);
        return val;
    }

    private static double ReadDouble(ReadOnlySpan<byte> data, ref int offset)
    {
        EnsureAvailable(data, offset, sizeof(double));
        var val = BinaryPrimitives.ReadDoubleBigEndian(data[offset..]);
        offset += sizeof(double);
        return val;
    }

    private static void EnsureAvailable(ReadOnlySpan<byte> data, int offset, int required)
    {
        if (data.Length - offset < required)
        {
            throw new NbtFormatException(
                $"Unexpected end of NBT stream at byte {offset} (needed {required} byte(s), {data.Length - offset} available).");
        }
    }
}
