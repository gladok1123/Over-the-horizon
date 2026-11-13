using System.Threading.Channels;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Rendering.Backends;
using OverHorizon.Rendering.Meshing;
using OverHorizon.Rendering.Textures;
using OverHorizon.World.Storage;

namespace OverHorizon.Client;

/// <summary>
/// Offloads multi-core parallel greedy + shaped chunk meshing onto background worker threads
/// and budgets incremental GPU section VAO/VBO uploads per render frame without whole-column wipes.
/// </summary>
internal sealed class ClientAsyncMeshQueue : IAsyncDisposable
{
    private const int MaxBatchDequeue = 20;
    private const int MaxGpuColumnUploadsPerFrame = 10;

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

    public void DrainPending()
    {
        while (_completed.Reader.TryRead(out _))
        {
            // Discard stale meshes from previous world session.
        }
    }

    public void DispatchAndUpload(ClientWorldCache worldCache, SilkOpenGlRenderSubsystem silkRenderer, ChunkPos? playerChunk = null)
    {
        if (Volatile.Read(ref _inFlightBatches) < 4)
        {
            var dirty = worldCache.DequeueDirtyNeighborhoods(MaxBatchDequeue, playerChunk);
            if (dirty.Count > 0 && _jobs.Writer.TryWrite(dirty))
            {
                _ = Interlocked.Increment(ref _inFlightBatches);
            }
        }

        var uploadedColumns = 0;
        var minSectionY = EngineConstants.WorldMinY >> 4;
        while (uploadedColumns < MaxGpuColumnUploadsPerFrame && _completed.Reader.TryRead(out var item))
        {
            if (!worldCache.IsColumnLoaded(new BlockPos(item.Position.X << 4, 0, item.Position.Z << 4)))
            {
                continue;
            }

            uint presentMask = 0;
            for (var i = 0; i < item.Sections.Count; i++)
            {
                var mesh = item.Sections[i];
                var sIdx = mesh.SectionPosition.Y - minSectionY;
                if ((uint)sIdx < 24U)
                {
                    presentMask |= 1U << sIdx;
                }

                silkRenderer.UploadOrReplaceSectionMesh(mesh);
            }

            for (var s = 0; s < ChunkColumn.SectionCount; s++)
            {
                if ((presentMask & (1U << s)) == 0)
                {
                    silkRenderer.RemoveSectionMesh(new SectionPos(item.Position.X, minSectionY + s, item.Position.Z));
                }
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
                    _ = Interlocked.Decrement(ref _inFlightBatches);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutdown requested.
        }
    }
}
