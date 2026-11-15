using System.Buffers;
using BenchmarkDotNet.Attributes;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Networking.Codec;
using OverHorizon.Networking.Framing;
using OverHorizon.Networking.Packets;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Networking.Packets.Serverbound;
using OverHorizon.World.Generation;
using OverHorizon.World.Serialization;
using OverHorizon.World.Storage;

namespace OverHorizon.Benchmarks;

[MemoryDiagnoser]
public class NetworkingBenchmarks
{
    private JavaProtocolCodec _codec = null!;
    private ArrayBufferWriter<byte> _payloadScratch = null!;
    private ArrayBufferWriter<byte> _wireScratch = null!;
    private ChunkDataPacket _sampleChunkPacket = null!;
    private byte[] _compressedChunkWireBytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _codec = new JavaProtocolCodec();
        _payloadScratch = new ArrayBufferWriter<byte>(65536);
        _wireScratch = new ArrayBufferWriter<byte>(32768);

        var registries = CoreRegistries.CreateDefault();
        var generator = new OverworldGenerator(0x5F3759DF42424242L, registries);
        var chunk = new ChunkColumn(new ChunkPos(0, 0));
        generator.GenerateChunk(chunk);

        var serializedChunk = ChunkNetworkSerializer.SerializeSnapshotToArray(chunk.CreateSnapshot());
        _sampleChunkPacket = new ChunkDataPacket(chunk.Position, serializedChunk);

        _payloadScratch.Clear();
        _codec.EncodeClientbound(ConnectionState.Play, _sampleChunkPacket, _payloadScratch);
        _wireScratch.Clear();
        PacketFramer.WriteFrame(_wireScratch, _payloadScratch.WrittenSpan, compressionThreshold: 256);
        _compressedChunkWireBytes = _wireScratch.WrittenSpan.ToArray();
    }

    [Benchmark]
    public int EncodeAndFrameMovePlayerPacket()
    {
        _payloadScratch.Clear();
        _wireScratch.Clear();

        _codec.EncodeServerbound(
            ConnectionState.Play,
            new MovePlayerPacket(new Vec3d(12.5, 68.0, -42.25), 90.0f, 10.0f, OnGround: true),
            _payloadScratch);
        PacketFramer.WriteFrame(_wireScratch, _payloadScratch.WrittenSpan, compressionThreshold: 256);
        return _wireScratch.WrittenCount;
    }

    [Benchmark]
    public int CompressAndFrameFullChunkColumn()
    {
        _payloadScratch.Clear();
        _wireScratch.Clear();

        _codec.EncodeClientbound(ConnectionState.Play, _sampleChunkPacket, _payloadScratch);
        PacketFramer.WriteFrame(_wireScratch, _payloadScratch.WrittenSpan, compressionThreshold: 256);
        return _wireScratch.WrittenCount;
    }

    [Benchmark]
    public int DecompressAndDecodeChunkColumn()
    {
        var seq = new ReadOnlySequence<byte>(_compressedChunkWireBytes);
        _ = PacketFramer.TryReadFrame(ref seq, compressionThreshold: 256, out var rented, out var len);
        try
        {
            var decoded = (ChunkDataPacket)_codec.DecodeClientbound(ConnectionState.Play, rented.AsSpan(0, len));
            return decoded.Payload.Length;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
