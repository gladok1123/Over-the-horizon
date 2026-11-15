using System.Buffers;
using System.IO.Compression;
using OverHorizon.Networking.Primitives;

namespace OverHorizon.Networking.Framing;

/// <summary>
/// Length-prefixed wire packet framer supporting both uncompressed and ZLib-compressed Java-style framing
/// over <see cref="ReadOnlySequence{T}"/> and <see cref="IBufferWriter{T}"/> with anti-DoS size guards.
/// </summary>
public static class PacketFramer
{
    /// <summary>Maximum outer wire frame body length (3-byte <c>VarInt</c>: 2 MiB - 1).</summary>
    public const int MaxWireFrameBytes = 2_097_151;

    /// <summary>Maximum decompressed packet body length (8 MiB, guards against ZLib zip-bombs).</summary>
    public const int MaxUncompressedPayloadBytes = 8_388_608;

    /// <summary>
    /// Encodes an uncompressed or ZLib-compressed packet body (<c>[PacketId: VarInt][Fields...]</c>) into <paramref name="output"/>.
    /// </summary>
    /// <param name="output">Target pipeline or buffer writer.</param>
    /// <param name="uncompressedPayload">Serialized <c>[PacketId][Payload]</c> bytes.</param>
    /// <param name="compressionThreshold">Threshold in bytes (<c>&lt; 0</c> = compression disabled).</param>
    public static void WriteFrame(
        IBufferWriter<byte> output,
        ReadOnlySpan<byte> uncompressedPayload,
        int compressionThreshold)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (uncompressedPayload.IsEmpty)
        {
            throw new ProtocolException("Cannot frame an empty packet payload.");
        }

        if (uncompressedPayload.Length > MaxUncompressedPayloadBytes)
        {
            throw new ProtocolException($"Outgoing packet size {uncompressedPayload.Length} exceeds limit {MaxUncompressedPayloadBytes}.");
        }

        if (compressionThreshold < 0)
        {
            if (uncompressedPayload.Length > MaxWireFrameBytes)
            {
                throw new ProtocolException($"Uncompressed packet size {uncompressedPayload.Length} exceeds wire frame limit.");
            }

            PacketBufferWriter.WriteVarInt(output, uncompressedPayload.Length);
            var dest = output.GetSpan(uncompressedPayload.Length);
            uncompressedPayload.CopyTo(dest);
            output.Advance(uncompressedPayload.Length);
            return;
        }

        if (uncompressedPayload.Length < compressionThreshold)
        {
            // Below threshold: DataLength = 0 (1 byte) followed by raw uncompressed payload.
            var frameBodyLength = 1 + uncompressedPayload.Length;
            PacketBufferWriter.WriteVarInt(output, frameBodyLength);
            PacketBufferWriter.WriteVarInt(output, 0);
            var dest = output.GetSpan(uncompressedPayload.Length);
            uncompressedPayload.CopyTo(dest);
            output.Advance(uncompressedPayload.Length);
            return;
        }

        // Compress with ZLibStream into a pooled memory buffer.
        using var compressedStream = new MemoryStream(Math.Max(64, uncompressedPayload.Length / 2));
        using (var zlib = new ZLibStream(compressedStream, CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(uncompressedPayload);
        }

        if (!compressedStream.TryGetBuffer(out var compressedSegment))
        {
            compressedSegment = new ArraySegment<byte>(compressedStream.ToArray());
        }

        var dataLenHeaderSize = VarIntCodec.GetVarIntLength(uncompressedPayload.Length);
        var totalFrameLen = dataLenHeaderSize + compressedSegment.Count;
        if (totalFrameLen > MaxWireFrameBytes)
        {
            throw new ProtocolException($"Compressed frame length {totalFrameLen} exceeds wire frame limit {MaxWireFrameBytes}.");
        }

        PacketBufferWriter.WriteVarInt(output, totalFrameLen);
        PacketBufferWriter.WriteVarInt(output, uncompressedPayload.Length);
        var targetSpan = output.GetSpan(compressedSegment.Count);
        compressedSegment.AsSpan().CopyTo(targetSpan);
        output.Advance(compressedSegment.Count);
    }

    /// <summary>
    /// Attempts to decode one complete packet payload (<c>[PacketId: VarInt][Fields...]</c>) from <paramref name="buffer"/>,
    /// advancing <paramref name="buffer"/> past the consumed frame on success.
    /// Caller MUST return <paramref name="rentedPayload"/> to <see cref="ArrayPool{T}.Shared"/> when returns <see langword="true"/>.
    /// </summary>
    /// <param name="buffer">Unread pipeline byte sequence.</param>
    /// <param name="compressionThreshold">Active compression threshold (<c>&lt; 0</c> = uncompressed).</param>
    /// <param name="rentedPayload">Pooled buffer holding the decoded uncompressed payload on success.</param>
    /// <param name="payloadLength">Valid byte count in <paramref name="rentedPayload"/>.</param>
    /// <returns><see langword="true"/> if a full frame was decoded; <see langword="false"/> if awaiting more bytes.</returns>
    public static bool TryReadFrame(
        ref ReadOnlySequence<byte> buffer,
        int compressionThreshold,
        out byte[] rentedPayload,
        out int payloadLength)
    {
        rentedPayload = [];
        payloadLength = 0;

        if (!VarIntCodec.TryReadVarIntHeader(in buffer, out var frameLength, out var headerBytes))
        {
            return false;
        }

        if (frameLength <= 0 || frameLength > MaxWireFrameBytes)
        {
            throw new ProtocolException($"Invalid wire frame length {frameLength} (must be in [1, {MaxWireFrameBytes}]).");
        }

        var totalNeeded = (long)headerBytes + frameLength;
        if (buffer.Length < totalNeeded)
        {
            return false;
        }

        var frameSlice = buffer.Slice(headerBytes, frameLength);
        buffer = buffer.Slice(totalNeeded);

        if (compressionThreshold < 0)
        {
            var rented = ArrayPool<byte>.Shared.Rent(frameLength);
            frameSlice.CopyTo(rented);
            rentedPayload = rented;
            payloadLength = frameLength;
            return true;
        }

        var tempFrame = ArrayPool<byte>.Shared.Rent(frameLength);
        try
        {
            frameSlice.CopyTo(tempFrame);
            var frameSpan = tempFrame.AsSpan(0, frameLength);
            var (uncompressedLength, dataHeaderBytes) = VarIntCodec.ReadVarInt(frameSpan);

            if (uncompressedLength < 0 || uncompressedLength > MaxUncompressedPayloadBytes)
            {
                throw new ProtocolException($"Invalid DataLength {uncompressedLength} in compressed frame.");
            }

            var innerSpan = frameSpan[dataHeaderBytes..];
            if (uncompressedLength == 0)
            {
                if (innerSpan.IsEmpty)
                {
                    throw new ProtocolException("Empty uncompressed body inside compressed frame.");
                }

                var rentedRaw = ArrayPool<byte>.Shared.Rent(innerSpan.Length);
                innerSpan.CopyTo(rentedRaw);
                rentedPayload = rentedRaw;
                payloadLength = innerSpan.Length;
                return true;
            }

            if (uncompressedLength < compressionThreshold)
            {
                throw new ProtocolException(
                    $"Compressed packet DataLength {uncompressedLength} is below active threshold {compressionThreshold}.");
            }

            var decompressed = ArrayPool<byte>.Shared.Rent(uncompressedLength);
            try
            {
                using var inputStream = new MemoryStream(tempFrame, dataHeaderBytes, innerSpan.Length, writable: false);
                using var zlib = new ZLibStream(inputStream, CompressionMode.Decompress, leaveOpen: true);

                var totalRead = 0;
                while (totalRead < uncompressedLength)
                {
                    var n = zlib.Read(decompressed, totalRead, uncompressedLength - totalRead);
                    if (n == 0)
                    {
                        throw new ProtocolException(
                            $"ZLib stream ended prematurely ({totalRead}/{uncompressedLength} bytes).");
                    }

                    totalRead += n;
                }

                // Ensure no extra trailing decompressed bytes (zip-bomb / mismatch check).
                if (zlib.ReadByte() != -1)
                {
                    throw new ProtocolException("ZLib stream produced more bytes than declared DataLength.");
                }

                rentedPayload = decompressed;
                payloadLength = uncompressedLength;
                return true;
            }
            catch (InvalidDataException ex)
            {
                ArrayPool<byte>.Shared.Return(decompressed);
                throw new ProtocolException("Corrupted ZLib payload in compressed frame.", ex);
            }
            catch
            {
                ArrayPool<byte>.Shared.Return(decompressed);
                throw;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(tempFrame);
        }
    }
}
