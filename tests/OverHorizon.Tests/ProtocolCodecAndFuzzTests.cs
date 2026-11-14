using System.Buffers;
using System.IO.Compression;
using FluentAssertions;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Networking.Codec;
using OverHorizon.Networking.Framing;
using OverHorizon.Networking.Packets;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Networking.Packets.Serverbound;
using OverHorizon.Networking.Primitives;
using OverHorizon.Shared.Random;
using OverHorizon.World.Generation;
using OverHorizon.World.Serialization;
using OverHorizon.World.Storage;
using Xunit;

namespace OverHorizon.Tests;

public sealed class ProtocolCodecAndFuzzTests
{
    [FsCheck.Xunit.Property]
    public void VarInt_And_VarLong_RoundTripAllSignedValues(int v32, long v64)
    {
        var writer = new ArrayBufferWriter<byte>(32);
        PacketBufferWriter.WriteVarInt(writer, v32);
        PacketBufferWriter.WriteVarLong(writer, v64);

        var reader = new PacketSpanReader(writer.WrittenSpan);
        reader.ReadVarInt().Should().Be(v32);
        reader.ReadVarLong().Should().Be(v64);
        reader.RemainingBytes.Should().Be(0);
    }

    [FsCheck.Xunit.Property]
    public void PackedBlockPos_RoundTripsAllSignedCoordinatesWithinBitWidths(int rawX, int rawY, int rawZ)
    {
        var x = Math.Clamp(rawX, -33_554_432, 33_554_431);
        var y = Math.Clamp(rawY, -2048, 2047);
        var z = Math.Clamp(rawZ, -33_554_432, 33_554_431);
        var expected = new BlockPos(x, y, z);

        var writer = new ArrayBufferWriter<byte>(16);
        PacketBufferWriter.WriteBlockPos(writer, expected);

        var reader = new PacketSpanReader(writer.WrittenSpan);
        reader.ReadBlockPos().Should().Be(expected);
    }

    [Fact]
    public void PacketFramer_StreamsFragmentedMultiSegmentBuffersWithZLibCompression()
    {
        const int threshold = 128;
        var codec = new JavaProtocolCodec();

        // 1. Small packet (< threshold -> uncompressed inside compressed frame format).
        var smallScratch = new ArrayBufferWriter<byte>(64);
        codec.EncodeServerbound(
            ConnectionState.Play,
            new MovePlayerPacket(new Vec3d(12.5, 65.0, -8.25), 90.0f, -15.0f, OnGround: true),
            smallScratch);

        // 2. Large repetitive chunk packet (>= threshold -> ZLib compressed).
        var largeChunkBytes = new byte[4096];
        Array.Fill(largeChunkBytes, (byte)0x42);
        var largeScratch = new ArrayBufferWriter<byte>(4200);
        codec.EncodeClientbound(
            ConnectionState.Play,
            new ChunkDataPacket(new ChunkPos(-3, 7), largeChunkBytes),
            largeScratch);

        var wire = new ArrayBufferWriter<byte>(2048);
        PacketFramer.WriteFrame(wire, smallScratch.WrittenSpan, threshold);
        PacketFramer.WriteFrame(wire, largeScratch.WrittenSpan, threshold);

        // Split wire bytes into tiny 7-byte linked segments to exercise fragmented ReadOnlySequence<byte> framing.
        var sequence = CreateFragmentedSequence(wire.WrittenSpan, chunkSize: 7);

        PacketFramer.TryReadFrame(ref sequence, threshold, out var p1, out var len1).Should().BeTrue();
        try
        {
            var move = codec.DecodeServerbound(ConnectionState.Play, p1.AsSpan(0, len1));
            move.Should().Be(new MovePlayerPacket(new Vec3d(12.5, 65.0, -8.25), 90.0f, -15.0f, OnGround: true));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(p1);
        }

        PacketFramer.TryReadFrame(ref sequence, threshold, out var p2, out var len2).Should().BeTrue();
        try
        {
            var chunk = codec.DecodeClientbound(ConnectionState.Play, p2.AsSpan(0, len2)).Should().BeOfType<ChunkDataPacket>().Subject;
            chunk.Position.Should().Be(new ChunkPos(-3, 7));
            chunk.Payload.ToArray().Should().Equal(largeChunkBytes);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(p2);
        }

        sequence.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void JavaProtocolCodec_RoundTripsAllServerboundAndClientboundPacketTypes()
    {
        var codec = new JavaProtocolCodec();
        var buf = new ArrayBufferWriter<byte>(256);
        var sampleUuid = Guid.Parse("12345678-9abc-def0-1234-56789abcdef0");

        IServerboundPacket[] serverbound =
        [
            new HandshakePacket(JavaProtocolCodec.CurrentProtocolVersion, "play.example.org", 25565, HandshakeIntent.Login),
            StatusRequestPacket.Instance,
            new PingRequestPacket(9876543210L),
            new LoginStartPacket("Builder42", sampleUuid),
            new KeepAliveResponsePacket(555L),
            new MovePlayerPacket(new Vec3d(-10.5, 72.0, 33.25), 180.0f, 12.5f, OnGround: false),
            new PlayerBlockActionPacket(PlayerBlockActionKind.PlaceBlock, new BlockPos(-5, 64, 12), Direction.Up, 3),
            new ChatMessagePacket("Привет из теста!"),
        ];

        foreach (var pkt in serverbound)
        {
            buf.Clear();
            codec.EncodeServerbound(pkt.ValidState, pkt, buf);
            codec.DecodeServerbound(pkt.ValidState, buf.WrittenSpan).Should().Be(pkt);
        }

        IClientboundPacket[] clientbound =
        [
            new StatusResponsePacket("{\"ok\":true}"),
            new PongResponsePacket(9876543210L),
            new DisconnectPacket(ConnectionState.Login, "Maintenance"),
            new SetCompressionPacket(256),
            new LoginSuccessPacket(sampleUuid, "Builder42", 77L),
            new DisconnectPacket(ConnectionState.Play, "Kicked"),
            new KeepAliveRequestPacket(555L),
            new SynchronizePlayerPositionPacket(new Vec3d(0.5, 68.0, 0.5), 45.0f, 0.0f, 1),
            new BlockUpdatePacket(new BlockPos(1, -60, -2), 9),
            new SystemChatPacket("System broadcast", IsOverlay: true),
        ];

        foreach (var pkt in clientbound)
        {
            buf.Clear();
            codec.EncodeClientbound(pkt.ValidState, pkt, buf);
            codec.DecodeClientbound(pkt.ValidState, buf.WrittenSpan).Should().Be(pkt);
        }
    }

    [Fact]
    public void MalformedZipBombAndRandomFuzzInputs_OnlyThrowProtocolException()
    {
        // 1. Oversized 6-byte VarInt must throw ProtocolException.
        var sixByteVarInt = new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x01 };
        Action readBadVarInt = () => _ = VarIntCodec.ReadVarInt(sixByteVarInt);
        readBadVarInt.Should().Throw<ProtocolException>();

        // 2. Zip-bomb check: compressed stream expands beyond declared DataLength.
        using var sneakyStream = new MemoryStream();
        using (var zlib = new ZLibStream(sneakyStream, CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(new byte[1024]);
        }

        var compressedBytes = sneakyStream.ToArray();
        var bombFrame = new ArrayBufferWriter<byte>(compressedBytes.Length + 16);
        var fakeDataLen = 256; // Claims 256 bytes, actually expands to 1024!
        var bodyLen = VarIntCodec.GetVarIntLength(fakeDataLen) + compressedBytes.Length;
        PacketBufferWriter.WriteVarInt(bombFrame, bodyLen);
        PacketBufferWriter.WriteVarInt(bombFrame, fakeDataLen);
        var dest = bombFrame.GetSpan(compressedBytes.Length);
        compressedBytes.CopyTo(dest);
        bombFrame.Advance(compressedBytes.Length);

        Action decodeBomb = () =>
        {
            var seq = new ReadOnlySequence<byte>(bombFrame.WrittenMemory);
            _ = PacketFramer.TryReadFrame(ref seq, compressionThreshold: 128, out var r, out _);
            ArrayPool<byte>.Shared.Return(r);
        };
        decodeBomb.Should().Throw<ProtocolException>();

        // 3. Deterministic fuzzing across 500 pseudo-random byte buffers and all protocol states.
        var codec = new JavaProtocolCodec();
        var rng = new Xoroshiro128PlusPlus(0xF022_2026_CAFE_BEEFUL);
        ConnectionState[] states =
        [
            ConnectionState.Handshaking,
            ConnectionState.Status,
            ConnectionState.Login,
            ConnectionState.Play,
        ];

        var survivedCount = 0;
        for (var i = 0; i < 500; i++)
        {
            var len = rng.NextInt32(96);
            var fuzz = new byte[len];
            for (var b = 0; b < len; b++)
            {
                fuzz[b] = (byte)rng.NextInt32(256);
            }

            var state = states[i & 3];
            var threshold = (i & 1) == 0 ? -1 : 64;

            try
            {
                var seq = new ReadOnlySequence<byte>(fuzz);
                if (PacketFramer.TryReadFrame(ref seq, threshold, out var rented, out var pLen))
                {
                    try
                    {
                        _ = codec.DecodeServerbound(state, rented.AsSpan(0, pLen));
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(rented);
                    }
                }

                survivedCount++;
            }
            catch (ProtocolException)
            {
                survivedCount++;
            }
        }

        survivedCount.Should().Be(500);
    }

    [Fact]
    public void ChunkNetworkSerializer_RoundTripsOverworldChunkBitForBit()
    {
        var registries = CoreRegistries.CreateDefault();
        var generator = new OverworldGenerator(worldSeed: 0x1234_5678_9ABC_DEF0L, registries);
        var original = new ChunkColumn(new ChunkPos(2, -3));
        generator.GenerateChunk(original);

        var snapshot = original.CreateSnapshot();
        var payload = ChunkNetworkSerializer.SerializeSnapshotToArray(snapshot);
        var hydrated = ChunkNetworkSerializer.DeserializeColumn(original.Position, payload, registries.Blocks);

        hydrated.ComputeDeterministicHash().Should().Be(original.ComputeDeterministicHash());
    }

    private static ReadOnlySequence<byte> CreateFragmentedSequence(ReadOnlySpan<byte> data, int chunkSize)
    {
        if (data.IsEmpty)
        {
            return ReadOnlySequence<byte>.Empty;
        }

        TestSegment? first = null;
        TestSegment? current = null;
        var offset = 0;

        while (offset < data.Length)
        {
            var take = Math.Min(chunkSize, data.Length - offset);
            var copy = data.Slice(offset, take).ToArray();
            var next = new TestSegment(copy, current);
            first ??= next;
            current = next;
            offset += take;
        }

        return new ReadOnlySequence<byte>(first!, 0, current!, current!.Memory.Length);
    }

    private sealed class TestSegment : ReadOnlySequenceSegment<byte>
    {
        public TestSegment(byte[] data, TestSegment? previous)
        {
            Memory = data;
            if (previous is not null)
            {
                RunningIndex = previous.RunningIndex + previous.Memory.Length;
                previous.Next = this;
            }
        }
    }
}
