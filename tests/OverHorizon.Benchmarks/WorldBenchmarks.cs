using System.Buffers;
using BenchmarkDotNet.Attributes;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.World.Generation;
using OverHorizon.World.Lighting;
using OverHorizon.World.Serialization;
using OverHorizon.World.Storage;

namespace OverHorizon.Benchmarks;

[MemoryDiagnoser]
public class WorldBenchmarks
{
    private CoreRegistries _registries = null!;
    private OverworldGenerator _generator = null!;
    private ChunkColumn _lightingChunk = null!;
    private ArrayBufferWriter<byte> _nbtBuffer = null!;
    private int _chunkCursor;

    [GlobalSetup]
    public void Setup()
    {
        _registries = CoreRegistries.CreateDefault();
        _generator = new OverworldGenerator(worldSeed: 20261112L, _registries);
        _lightingChunk = new ChunkColumn(new ChunkPos(0, 0));
        _generator.GenerateChunk(_lightingChunk);
        _nbtBuffer = new ArrayBufferWriter<byte>(32_768);
    }

    [Benchmark]
    public BlockState PalettedContainerWriteAndRead4096()
    {
        var container = new PalettedContainer<BlockState>(
            capacity: 4096,
            initialValue: BlockState.Air,
            minIndirectBits: 4,
            maxIndirectBits: 8,
            directBits: 16,
            toGlobalId: static s => s.Id,
            fromGlobalId: static id => new BlockState((ushort)id));

        for (var i = 0; i < 4096; i++)
        {
            container.Set(i, new BlockState((ushort)(i & 15)));
        }

        return container.Get(2048);
    }

    [Benchmark]
    public int GenerateAndLightChunk()
    {
        var x = _chunkCursor++;
        var chunk = new ChunkColumn(new ChunkPos(x, -x));
        _generator.GenerateChunk(chunk);
        return chunk.GetHeight(HeightmapType.WorldSurface, 8, 8);
    }

    [Benchmark]
    public byte RefreshChunkLighting()
    {
        LightingEngine.RefreshChunkLight(_lightingChunk, _registries.Blocks);
        return _lightingChunk.GetSkyLight(8, 80, 8);
    }

    [Benchmark]
    public int ChunkNbtRoundTrip()
    {
        _nbtBuffer.Clear();
        ChunkNbtSerializer.Serialize(_nbtBuffer, _lightingChunk, _registries);
        var restored = ChunkNbtSerializer.Deserialize(_nbtBuffer.WrittenSpan, _registries);
        return restored.GetHeight(HeightmapType.WorldSurface, 8, 8);
    }
}
