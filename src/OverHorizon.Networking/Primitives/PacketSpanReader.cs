using System.Buffers.Binary;
using System.Text;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;

namespace OverHorizon.Networking.Primitives;

/// <summary>
/// Zero-allocation forward-only wire primitive reader over a contiguous <see cref="ReadOnlySpan{T}"/>.
/// </summary>
public ref struct PacketSpanReader
{
    private readonly ReadOnlySpan<byte> _buffer;
    private int _offset;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketSpanReader"/> struct.
    /// </summary>
    /// <param name="buffer">Packet payload bytes.</param>
    public PacketSpanReader(ReadOnlySpan<byte> buffer)
    {
        _buffer = buffer;
        _offset = 0;
    }

    /// <summary>Gets the number of unread bytes remaining in the packet.</summary>
    public readonly int RemainingBytes => _buffer.Length - _offset;

    /// <summary>Reads an unsigned 8-bit integer.</summary>
    /// <returns>Decoded byte.</returns>
    public byte ReadUInt8()
    {
        EnsureAvailable(1);
        return _buffer[_offset++];
    }

    /// <summary>Reads a protocol boolean (<c>0x00</c> = false, <c>0x01</c> = true).</summary>
    /// <returns>Decoded boolean.</returns>
    public bool ReadBool()
    {
        var raw = ReadUInt8();
        return raw switch
        {
            0 => false,
            1 => true,
            _ => throw new ProtocolException($"Invalid boolean byte 0x{raw:X2}."),
        };
    }

    /// <summary>Reads a signed 16-bit big-endian integer.</summary>
    /// <returns>Decoded 16-bit integer.</returns>
    public short ReadInt16()
    {
        EnsureAvailable(2);
        var val = BinaryPrimitives.ReadInt16BigEndian(_buffer.Slice(_offset, 2));
        _offset += 2;
        return val;
    }

    /// <summary>Reads a signed 32-bit big-endian integer.</summary>
    /// <returns>Decoded 32-bit integer.</returns>
    public int ReadInt32()
    {
        EnsureAvailable(4);
        var val = BinaryPrimitives.ReadInt32BigEndian(_buffer.Slice(_offset, 4));
        _offset += 4;
        return val;
    }

    /// <summary>Reads a signed 64-bit big-endian integer.</summary>
    /// <returns>Decoded 64-bit integer.</returns>
    public long ReadInt64()
    {
        EnsureAvailable(8);
        var val = BinaryPrimitives.ReadInt64BigEndian(_buffer.Slice(_offset, 8));
        _offset += 8;
        return val;
    }

    /// <summary>Reads an IEEE-754 32-bit big-endian float.</summary>
    /// <returns>Decoded float.</returns>
    public float ReadFloat32()
    {
        EnsureAvailable(4);
        var val = BinaryPrimitives.ReadSingleBigEndian(_buffer.Slice(_offset, 4));
        _offset += 4;
        return val;
    }

    /// <summary>Reads an IEEE-754 64-bit big-endian double.</summary>
    /// <returns>Decoded double.</returns>
    public double ReadFloat64()
    {
        EnsureAvailable(8);
        var val = BinaryPrimitives.ReadDoubleBigEndian(_buffer.Slice(_offset, 8));
        _offset += 8;
        return val;
    }

    /// <summary>Reads a 32-bit <c>VarInt</c>.</summary>
    /// <returns>Decoded 32-bit integer.</returns>
    public int ReadVarInt()
    {
        var (val, consumed) = VarIntCodec.ReadVarInt(_buffer[_offset..]);
        _offset += consumed;
        return val;
    }

    /// <summary>Reads a 64-bit <c>VarLong</c>.</summary>
    /// <returns>Decoded 64-bit integer.</returns>
    public long ReadVarLong()
    {
        var (val, consumed) = VarIntCodec.ReadVarLong(_buffer[_offset..]);
        _offset += consumed;
        return val;
    }

    /// <summary>Reads a 128-bit big-endian UUID.</summary>
    /// <returns>Decoded <see cref="Guid"/>.</returns>
    public Guid ReadUuid()
    {
        EnsureAvailable(16);
        var id = new Guid(_buffer.Slice(_offset, 16), bigEndian: true);
        _offset += 16;
        return id;
    }

    /// <summary>
    /// Reads a <c>VarInt</c>-prefixed UTF-8 string, enforcing <paramref name="maxChars"/>.
    /// </summary>
    /// <param name="maxChars">Maximum allowed character count.</param>
    /// <returns>Decoded string.</returns>
    public string ReadUtf8(int maxChars = PacketBufferWriter.DefaultMaxStringChars)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxChars);

        var byteLength = ReadVarInt();
        if (byteLength < 0 || byteLength > maxChars * 3)
        {
            throw new ProtocolException($"Invalid UTF-8 byte length {byteLength} (maxChars={maxChars}).");
        }

        EnsureAvailable(byteLength);
        var slice = _buffer.Slice(_offset, byteLength);
        _offset += byteLength;

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(slice);
        }
        catch (DecoderFallbackException ex)
        {
            throw new ProtocolException("Invalid UTF-8 byte sequence in packet string.", ex);
        }

        if (decoded.Length > maxChars)
        {
            throw new ProtocolException($"Decoded string length {decoded.Length} exceeds limit {maxChars}.");
        }

        return decoded;
    }

    /// <summary>Reads a <c>VarInt</c>-prefixed byte array capped at <paramref name="maxBytes"/>.</summary>
    /// <param name="maxBytes">Maximum permitted byte length.</param>
    /// <returns>Allocated copy of payload bytes.</returns>
    public byte[] ReadByteArray(int maxBytes)
    {
        var length = ReadVarInt();
        if (length < 0 || length > maxBytes)
        {
            throw new ProtocolException($"Byte array length {length} is outside [0, {maxBytes}].");
        }

        EnsureAvailable(length);
        var result = _buffer.Slice(_offset, length).ToArray();
        _offset += length;
        return result;
    }

    /// <summary>Reads a packed 64-bit big-endian <see cref="BlockPos"/>.</summary>
    /// <returns>Decoded <see cref="BlockPos"/>.</returns>
    public BlockPos ReadBlockPos()
    {
        var packed = ReadInt64();
        var x = (int)(packed >> 38);
        var z = (int)((packed << 26) >> 38);
        var y = (int)((packed << 52) >> 52);
        return new BlockPos(x, y, z);
    }

    /// <summary>Reads a validated <see cref="ResourceKey"/>.</summary>
    /// <returns>Decoded <see cref="ResourceKey"/>.</returns>
    public ResourceKey ReadResourceKey()
    {
        var raw = ReadUtf8(maxChars: 512);
        try
        {
            return ResourceKey.Parse(raw);
        }
        catch (FormatException ex)
        {
            throw new ProtocolException($"Invalid ResourceKey '{raw}' in packet.", ex);
        }
    }

    /// <summary>Throws <see cref="ProtocolException"/> if trailing unread bytes remain.</summary>
    public readonly void EnsureEndOfPacket()
    {
        if (_offset != _buffer.Length)
        {
            throw new ProtocolException($"Packet has {_buffer.Length - _offset} unexpected trailing bytes.");
        }
    }

    private readonly void EnsureAvailable(int requiredBytes)
    {
        if (requiredBytes < 0 || _offset > _buffer.Length - requiredBytes)
        {
            throw new ProtocolException(
                $"Packet truncated: needed {requiredBytes} bytes, only {_buffer.Length - _offset} remaining.");
        }
    }
}
