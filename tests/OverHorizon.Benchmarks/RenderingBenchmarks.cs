using BenchmarkDotNet.Attributes;
using OverHorizon.Audio;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Rendering.Backends;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Meshing;
using OverHorizon.Rendering.Textures;
using OverHorizon.World.Generation;

namespace OverHorizon.Benchmarks;

/// <summary>
/// Microbenchmarks measuring greedy chunk meshing, parallel 3x3 chunk meshing, procedural atlas generation,
/// frustum culling across resident sections, and procedural PCM SFX synthesis.
/// </summary>
[MemoryDiagnoser]
public class RenderingBenchmarks
{
    private CoreRegistries _registries = null!;
    private TextureAtlasData _atlas = null!;
    private ChunkNeighborhood _centerNeighborhood;
    private IReadOnlyList<ChunkNeighborhood> _nineChunkBatch = null!;
    private HeadlessRenderSubsystem _headlessRenderer = null!;
    private FirstPersonCamera _camera = null!;

    [GlobalSetup]
    public void Setup()
    {
        _registries = CoreRegistries.CreateDefault();
        _atlas = ProceduralAtlasGenerator.CreateDefault(_registries.Blocks);
        var generator = new OverworldGenerator(seed: 20261112L, _registries);

        var snapshots = new Dictionary<ChunkPos, OverHorizon.World.Storage.ChunkSnapshot>(9);
        for (var z = -1; z <= 1; z++)
        {
            for (var x = -1; x <= 1; x++)
            {
                var pos = new ChunkPos(x, z);
                snapshots[pos] = generator.GenerateChunk(pos).CreateSnapshot();
            }
        }

        _centerNeighborhood = new ChunkNeighborhood(
            snapshots[new ChunkPos(0, 0)],
            North: snapshots[new ChunkPos(0, -1)],
            South: snapshots[new ChunkPos(0, 1)],
            West: snapshots[new ChunkPos(-1, 0)],
            East: snapshots[new ChunkPos(1, 0)]);

        var batch = new List<ChunkNeighborhood>(9);
        foreach (var (pos, center) in snapshots)
        {
            _ = snapshots.TryGetValue(new ChunkPos(pos.X, pos.Z - 1), out var n);
            _ = snapshots.TryGetValue(new ChunkPos(pos.X, pos.Z + 1), out var s);
            _ = snapshots.TryGetValue(new ChunkPos(pos.X - 1, pos.Z), out var w);
            _ = snapshots.TryGetValue(new ChunkPos(pos.X + 1, pos.Z), out var e);
            batch.Add(new ChunkNeighborhood(center, n, s, w, e));
        }

        _nineChunkBatch = batch;
        _headlessRenderer = new HeadlessRenderSubsystem();
        _headlessRenderer.UploadAtlas(_atlas);

        foreach (var nb in _nineChunkBatch)
        {
            foreach (var mesh in ParallelChunkMesher.MeshColumn(in nb, _registries.Blocks, _atlas))
            {
                _headlessRenderer.UploadOrReplaceSectionMesh(mesh);
            }
        }

        _camera = new FirstPersonCamera { Position = new Vec3d(8.0, 90.0, 24.0) };
        _camera.SetRotation(0.0f, -25.0f);
    }

    [GlobalCleanup]
    public void Cleanup() => _headlessRenderer.Dispose();

    [Benchmark]
    public int GreedyMeshSingleOverworldColumn() =>
        ParallelChunkMesher.MeshColumn(in _centerNeighborhood, _registries.Blocks, _atlas).Count;

    [Benchmark]
    public Task<int> ParallelMeshNineChunkColumnsAsync() =>
        MeshBatchCoreAsync();

    [Benchmark]
    public int FrustumCullAndSimulateFrame() =>
        _headlessRenderer.RenderFrame(_camera).TrianglesSubmitted;

    [Benchmark]
    public TextureAtlasData GenerateProceduralAtlas() =>
        ProceduralAtlasGenerator.CreateDefault(_registries.Blocks);

    [Benchmark]
    public int SynthesizeProceduralSoundBank() =>
        ProceduralSoundBank.CreateDefaultBank().Count;

    private async Task<int> MeshBatchCoreAsync()
    {
        var res = await ParallelChunkMesher.MeshColumnsParallelAsync(_nineChunkBatch, _registries.Blocks, _atlas)
            .ConfigureAwait(false);
        return res.Count;
    }
}
