using OverHorizon.Core.Blocks;
using OverHorizon.Rendering.Textures;
using OverHorizon.Rendering.Threading;
using OverHorizon.World.Storage;

namespace OverHorizon.Rendering.Meshing;

/// <summary>
/// Parallel worker coordinator generating greedy section meshes across chunk neighborhoods without blocking the render thread.
/// </summary>
public static class ParallelChunkMesher
{
    /// <summary>Minimum number of non-air sections a worker must own before one column is meshed in parallel.</summary>
    private const int MinSectionsPerWorker = 2;

    /// <summary>Minimum number of columns a worker must own before a batch is meshed in parallel.</summary>
    private const int MinColumnsPerWorker = 1;

    /// <summary>
    /// Meshes all non-empty vertical sections of <paramref name="neighborhood"/>, spreading the sections across
    /// the worker pool when the column is tall enough to pay for the hand-off.
    /// </summary>
    /// <param name="neighborhood">Target chunk and neighbor snapshots.</param>
    /// <param name="blocks">Frozen block registry.</param>
    /// <param name="atlas">Procedural block texture atlas.</param>
    /// <param name="maxThreads">
    /// Worker cap for the per-section fan-out (<c>0</c> selects <see cref="RenderParallelism.AutoWorkerCount"/>,
    /// <c>1</c> keeps the column on the calling thread, which is what callers that already parallelise over
    /// columns pass so the two levels never oversubscribe the pool).
    /// </param>
    /// <returns>Non-empty <see cref="SectionMeshData"/> entries ordered from bottom section to top section.</returns>
    public static IReadOnlyList<SectionMeshData> MeshColumn(
        in ChunkNeighborhood neighborhood,
        IBlockRegistry blocks,
        TextureAtlasData atlas,
        int maxThreads = 0)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(atlas);

        var sectionIndices = new int[ChunkColumn.SectionCount];
        var pendingCount = 0;
        var sections = neighborhood.Center.Sections;

        for (var s = 0; s < ChunkColumn.SectionCount; s++)
        {
            if (!sections[s].IsOnlyAir)
            {
                sectionIndices[pendingCount++] = s;
            }
        }

        if (pendingCount == 0)
        {
            return [];
        }

        // An `in` parameter cannot be captured by a lambda, so the neighbourhood is copied into a local first.
        var captured = neighborhood;
        var meshes = new SectionMeshData?[pendingCount];
        var workers = RenderParallelism.ResolveWorkerCount(pendingCount, MinSectionsPerWorker, maxThreads);

        if (workers <= 1)
        {
            for (var i = 0; i < pendingCount; i++)
            {
                meshes[i] = MeshSection(ref captured, sectionIndices[i], blocks, atlas);
            }
        }
        else
        {
            Parallel.For(
                0,
                pendingCount,
                new ParallelOptions { MaxDegreeOfParallelism = workers },
                i => meshes[i] = MeshSection(ref captured, sectionIndices[i], blocks, atlas));
        }

        var results = new List<SectionMeshData>(pendingCount);
        for (var i = 0; i < pendingCount; i++)
        {
            var mesh = meshes[i];
            if (mesh is not null && !mesh.IsEmpty)
            {
                results.Add(mesh);
            }
        }

        return results;
    }

    private static SectionMeshData MeshSection(
        ref ChunkNeighborhood neighborhood,
        int sectionIndex,
        IBlockRegistry blocks,
        TextureAtlasData atlas) =>
        GreedySectionMesher.MeshSection(in neighborhood, sectionIndex, blocks, atlas);

    /// <summary>
    /// Meshes a batch of <see cref="ChunkNeighborhood"/> columns concurrently across worker threads
    /// and returns all non-empty section meshes sorted deterministically by <c>SectionPosition</c>.
    /// </summary>
    /// <param name="neighborhoods">Batch of chunk neighborhoods to mesh.</param>
    /// <param name="blocks">Frozen block registry.</param>
    /// <param name="atlas">Procedural block texture atlas.</param>
    /// <param name="maxThreads">
    /// Worker cap for the per-column fan-out (<c>0</c> selects <see cref="RenderParallelism.AutoWorkerCount"/>).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Deterministically sorted list of non-empty <see cref="SectionMeshData"/>.</returns>
    public static async ValueTask<IReadOnlyList<SectionMeshData>> MeshColumnsParallelAsync(
        IReadOnlyList<ChunkNeighborhood> neighborhoods,
        IBlockRegistry blocks,
        TextureAtlasData atlas,
        int maxThreads = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(neighborhoods);
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(atlas);

        if (neighborhoods.Count == 0)
        {
            return [];
        }

        var perChunkResults = new IReadOnlyList<SectionMeshData>[neighborhoods.Count];
        var workers = RenderParallelism.ResolveWorkerCount(neighborhoods.Count, MinColumnsPerWorker, maxThreads);

        // Two levels of fan-out would oversubscribe the pool, so a column that is meshed as part of a
        // parallel batch keeps its own sections on one thread, and a lone column spreads them out instead.
        var sectionThreads = workers > 1 ? 1 : maxThreads;

        if (workers <= 1)
        {
            for (var i = 0; i < neighborhoods.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var nb = neighborhoods[i];
                perChunkResults[i] = MeshColumn(in nb, blocks, atlas, sectionThreads);
            }
        }
        else
        {
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = workers,
                CancellationToken = cancellationToken,
            };

            await Parallel.ForAsync(
                0,
                neighborhoods.Count,
                options,
                (index, _) =>
                {
                    var nb = neighborhoods[index];
                    perChunkResults[index] = MeshColumn(in nb, blocks, atlas, sectionThreads);
                    return ValueTask.CompletedTask;
                }).ConfigureAwait(false);
        }

        var totalCount = 0;
        for (var i = 0; i < perChunkResults.Length; i++)
        {
            totalCount += perChunkResults[i].Count;
        }

        var merged = new List<SectionMeshData>(totalCount);
        for (var i = 0; i < perChunkResults.Length; i++)
        {
            merged.AddRange(perChunkResults[i]);
        }

        merged.Sort(static (a, b) => CompareSectionPos(a.SectionPosition, b.SectionPosition));
        return merged;
    }

    private static int CompareSectionPos(OverHorizon.Core.Math.SectionPos a, OverHorizon.Core.Math.SectionPos b)
    {
        var cx = a.X.CompareTo(b.X);
        if (cx != 0)
        {
            return cx;
        }

        var cz = a.Z.CompareTo(b.Z);
        return cz != 0 ? cz : a.Y.CompareTo(b.Y);
    }
}
