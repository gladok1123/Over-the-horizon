using System.Buffers;

namespace OverHorizon.Networking.Primitives;

/// <summary>
/// Allocation-free variable-length integer (<c>VarInt</c> and <c>VarLong</c>) wire codec.
/// </summary>
public static class VarIntCodec
{
    /// <summary>Maximum encoded byte count for a 32-bit <c>VarInt</c>.</summary>
    public const int MaxVarIntBytes = 5;

    /// <summary>Maximum encoded byte count for a 64-bit <c>VarLong</c>.</summary>
    public const int MaxVarLongBytes = 10;

    /// <summary>
    /// Computes the exact number of bytes required to encode <paramref name="number"/> as a <c>VarInt</c>.
    /// </summary>
    /// <param name="number">32-bit signed integer.</param>
    /// <returns>Byte length in <c>[1, 5]</c>.</returns>
    public static int GetVarIntLength(int number)
    {
        var unsigned = (uint)number;
        var count = 1;
        while ((unsigned >>= 7) != 0U)
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// Writes a 32-bit <c>VarInt</c> into <paramref name="destination"/> and returns the number of bytes written.
    /// </summary>
    /// <param name="destination">Destination span (at least 5 bytes recommended).</param>
    /// <param name="number">Value to encode.</param>
    /// <returns>Bytes written (<c>1..5</c>).</returns>
    public static int WriteVarInt(Span<byte> destination, int number)
    {
        var unsigned = (uint)number;
        var written = 0;

        while ((unsigned & ~0x7FU) != 0U)
        {
            destination[written++] = (byte)((unsigned & 0x7FU) | 0x80U);
            unsigned >>= 7;
        }

        destination[written++] = (byte)unsigned;
        return written;
    }

    /// <summary>
    /// Writes a 64-bit <c>VarLong</c> into <paramref name="destination"/> and returns the number of bytes written.
    /// </summary>
    /// <param name="destination">Destination span (at least 10 bytes recommended).</param>
    /// <param name="number">Value to encode.</param>
    /// <returns>Bytes written (<c>1..10</c>).</returns>
    public static int WriteVarLong(Span<byte> destination, long number)
    {
        var unsigned = (ulong)number;
        var written = 0;

        while ((unsigned & ~0x7FUL) != 0UL)
        {
            destination[written++] = (byte)((unsigned & 0x7FUL) | 0x80UL);
            unsigned >>= 7;
        }

        destination[written++] = (byte)unsigned;
        return written;
    }

    /// <summary>
    /// Reads a 32-bit <c>VarInt</c> from <paramref name="source"/>, throwing <see cref="ProtocolException"/> if truncated or oversized.
    /// </summary>
    /// <param name="source">Source byte span.</param>
    /// <returns>Tuple of decoded 32-bit integer and number of bytes consumed.</returns>
    public static (int Value, int BytesConsumed) ReadVarInt(ReadOnlySpan<byte> source)
    {
        uint result = 0;
        var shift = 0;
        var index = 0;

        while (true)
        {
            if (index >= source.Length)
            {
                throw new ProtocolException("Unexpected end of buffer while decoding VarInt.");
            }

            var current = source[index++];
            if (shift == 28 && (current & 0xF0) != 0)
            {
                throw new ProtocolException("VarInt exceeds 32-bit range (malformed 5th byte).");
            }

            result |= (uint)(current & 0x7F) << shift;
            if ((current & 0x80) == 0)
            {
                return ((int)result, index);
            }

            shift += 7;
            if (shift >= 35)
            {
                throw new ProtocolException("VarInt exceeds maximum 5-byte length.");
            }
        }
    }

    /// <summary>
    /// Reads a 64-bit <c>VarLong</c> from <paramref name="source"/>, throwing <see cref="ProtocolException"/> if truncated or oversized.
    /// </summary>
    /// <param name="source">Source byte span.</param>
    /// <returns>Tuple of decoded 64-bit integer and number of bytes consumed.</returns>
    public static (long Value, int BytesConsumed) ReadVarLong(ReadOnlySpan<byte> source)
    {
        ulong result = 0;
        var shift = 0;
        var index = 0;

        while (true)
        {
            if (index >= source.Length)
            {
                throw new ProtocolException("Unexpected end of buffer while decoding VarLong.");
            }

            var current = source[index++];
            if (shift == 63 && current > 1)
            {
                throw new ProtocolException("VarLong exceeds 64-bit range (malformed 10th byte).");
            }

            result |= (ulong)(current & 0x7F) << shift;
            if ((current & 0x80) == 0)
            {
                return ((long)result, index);
            }

            shift += 7;
            if (shift >= 70)
            {
                throw new ProtocolException("VarLong exceeds maximum 10-byte length.");
            }
        }
    }

    /// <summary>
    /// Attempts to decode a frame-header <c>VarInt</c> from a fragmented <see cref="ReadOnlySequence{T}"/>
    /// without throwing when more network bytes are still in flight.
    /// </summary>
    /// <param name="sequence">Incoming pipeline sequence.</param>
    /// <param name="decoded">Decoded 32-bit integer on success.</param>
    /// <param name="bytesConsumed">Bytes consumed on success.</param>
    /// <returns><see langword="true"/> if a complete <c>VarInt</c> was available; <see langword="false"/> if incomplete.</returns>
    public static bool TryReadVarIntHeader(
        in ReadOnlySequence<byte> sequence,
        out int decoded,
        out int bytesConsumed)
    {
        var reader = new SequenceReader<byte>(sequence);
        uint result = 0;
        var shift = 0;
        var count = 0;

        while (reader.TryRead(out var current))
        {
            count++;
            if (shift == 28 && (current & 0xF0) != 0)
            {
                throw new ProtocolException("Frame header VarInt exceeds 32-bit range.");
            }

            result |= (uint)(current & 0x7F) << shift;
            if ((current & 0x80) == 0)
            {
                decoded = (int)result;
                bytesConsumed = count;
                return true;
            }

            shift += 7;
            if (shift >= 35)
            {
                throw new ProtocolException("Frame header VarInt exceeds maximum 5-byte length.");
            }
        }

        if (count >= MaxVarIntBytes)
        {
            throw new ProtocolException("Frame header VarInt exceeds maximum 5-byte length.");
        }

        decoded = 0;
        bytesConsumed = 0;
        return false;
    }
}
