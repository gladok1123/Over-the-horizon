using System.Threading.Channels;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Rendering.Backends;
using OverHorizon.Rendering.Meshing;
using OverHorizon.Rendering.Textures;
using OverHorizon.World.Storage;

namespace OverHorizon.Client;

/// <summary>
/// Offloads background greedy chunk meshing onto a dedicated worker task and uploads completed meshes on the render thread.
/// </summary>
internal sealed class ClientAsyncMeshQueue : IAsyncDisposable
{
    private readonly IBlockRegistry _blocks;
    private readonly TextureAtlasData _atlas;
    private readonly Channel<IReadOnlyList<ChunkNeighborhood>> _jobs = Channel.CreateUnbounded<IReadOnlyList<ChunkNeighborhood>>();
    private readonly Channel<(ChunkPos Position, IReadOnlyList<SectionMeshData> Sections)> _completed =
        Channel.CreateUnbounded<(ChunkPos, IReadOnlyList<SectionMeshData>)>();

    private Task? _workerTask;

    public ClientAsyncMeshQueue(IBlockRegistry blocks, TextureAtlasData atlas)
    {
        _blocks = blocks;
        _atlas = atlas;
    }

    public void Start(CancellationToken cancellationToken) =>
        _workerTask = PumpJobsAsync(cancellationToken);

    public void DispatchAndUpload(ClientWorldCache worldCache, SilkOpenGlRenderSubsystem silkRenderer)
    {
        var dirty = worldCache.DequeueDirtyNeighborhoods(maxColumns: 4);
        if (dirty.Count > 0)
        {
            _ = _jobs.Writer.TryWrite(dirty);
        }

        while (_completed.Reader.TryRead(out var item))
        {
            silkRenderer.RemoveChunkMeshes(item.Position);
            foreach (var mesh in item.Sections)
            {
                silkRenderer.UploadOrReplaceSectionMesh(mesh);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _jobs.Writer.TryComplete();
        if (_workerTask is null)
        {
            return;
        }

        try
        {
            await _workerTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown cancellation.
        }

        _workerTask = null;
    }

    private async Task PumpJobsAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var batch in _jobs.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                foreach (var nb in batch)
                {
                    ct.ThrowIfCancellationRequested();
                    var sections = ParallelChunkMesher.MeshColumn(in nb, _blocks, _atlas);
                    _ = _completed.Writer.TryWrite((nb.Center.Position, sections));
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutdown requested.
        }
    }
}
