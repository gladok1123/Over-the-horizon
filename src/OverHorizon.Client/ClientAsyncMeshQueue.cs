using System.Threading.Channels;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Rendering.Backends;
using OverHorizon.Rendering.Meshing;
using OverHorizon.Rendering.Textures;
using OverHorizon.World.Storage;

namespace OverHorizon.Client;

/// <summary>
/// Offloads multi-core parallel greedy + shaped chunk meshing onto background worker threads
/// and budgets GPU VAO/VBO uploads per render frame so even 64-chunk view distances stay stall-free.
/// </summary>
internal sealed class ClientAsyncMeshQueue : IAsyncDisposable
{
    private const int MaxBatchDequeue = 24;
    private const int MaxGpuColumnUploadsPerFrame = 14;

    private readonly IBlockRegistry _blocks;
    private readonly TextureAtlasData _atlas;
    private readonly Channel<IReadOnlyList<ChunkNeighborhood>> _jobs = Channel.CreateBounded<IReadOnlyList<ChunkNeighborhood>>(
        new BoundedChannelOptions(16)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

    private readonly Channel<(ChunkPos Position, IReadOnlyList<SectionMeshData> Sections)> _completed =
        Channel.CreateUnbounded<(ChunkPos, IReadOnlyList<SectionMeshData>)>();

    private Task? _workerTask;
    private int _inFlightBatches;

    public ClientAsyncMeshQueue(IBlockRegistry blocks, TextureAtlasData atlas)
    {
        _blocks = blocks;
        _atlas = atlas;
    }

    public void Start(CancellationToken cancellationToken) =>
        _workerTask = PumpJobsAsync(cancellationToken);

    public void DispatchAndUpload(ClientWorldCache worldCache, SilkOpenGlRenderSubsystem silkRenderer, ChunkPos? playerChunk = null)
    {
        if (Volatile.Read(ref _inFlightBatches) < 4)
        {
            var dirty = worldCache.DequeueDirtyNeighborhoods(MaxBatchDequeue, playerChunk);
            if (dirty.Count > 0 && _jobs.Writer.TryWrite(dirty))
            {
                Interlocked.Increment(ref _inFlightBatches);
            }
        }

        var uploadedColumns = 0;
        while (uploadedColumns < MaxGpuColumnUploadsPerFrame && _completed.Reader.TryRead(out var item))
        {
            silkRenderer.RemoveChunkMeshes(item.Position);
            foreach (var mesh in item.Sections)
            {
                silkRenderer.UploadOrReplaceSectionMesh(mesh);
            }

            uploadedColumns++;
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
                try
                {
                    Parallel.For(0, batch.Count, new ParallelOptions { CancellationToken = ct }, i =>
                    {
                        var nb = batch[i];
                        var sections = ParallelChunkMesher.MeshColumn(in nb, _blocks, _atlas);
                        _ = _completed.Writer.TryWrite((nb.Center.Position, sections));
                    });
                }
                finally
                {
                    Interlocked.Decrement(ref _inFlightBatches);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutdown requested.
        }
    }
}
