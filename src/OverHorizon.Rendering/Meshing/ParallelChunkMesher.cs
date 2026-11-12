using OverHorizon.Core.Blocks;
using OverHorizon.Rendering.Textures;
using OverHorizon.World.Storage;

namespace OverHorizon.Rendering.Meshing;

/// <summary>
/// Parallel worker coordinator generating greedy section meshes across chunk neighborhoods without blocking the render thread.
/// </summary>
public static class ParallelChunkMesher
{
    /// <summary>
    /// Meshes all non-empty vertical sections of <paramref name="neighborhood"/> synchronously.
    /// </summary>
    /// <param name="neighborhood">Target chunk and neighbor snapshots.</param>
    /// <param name="blocks">Frozen block registry.</param>
    /// <param name="atlas">Procedural block texture atlas.</param>
    /// <returns>Non-empty <see cref="SectionMeshData"/> entries ordered from bottom section to top section.</returns>
    public static IReadOnlyList<SectionMeshData> MeshColumn(
        in ChunkNeighborhood neighborhood,
        IBlockRegistry blocks,
        TextureAtlasData atlas)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(atlas);

        var results = new List<SectionMeshData>(8);
        var sections = neighborhood.Center.Sections;

        for (var s = 0; s < ChunkColumn.SectionCount; s++)
        {
            if (sections[s].IsOnlyAir)
            {
                continue;
            }

            var mesh = GreedySectionMesher.MeshSection(in neighborhood, s, blocks, atlas);
            if (!mesh.IsEmpty)
            {
                results.Add(mesh);
            }
        }

        return results;
    }

    /// <summary>
    /// Meshes a batch of <see cref="ChunkNeighborhood"/> columns concurrently across worker threads
    /// and returns all non-empty section meshes sorted deterministically by <c>SectionPosition</c>.
    /// </summary>
    /// <param name="neighborhoods">Batch of chunk neighborhoods to mesh.</param>
    /// <param name="blocks">Frozen block registry.</param>
    /// <param name="atlas">Procedural block texture atlas.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Deterministically sorted list of non-empty <see cref="SectionMeshData"/>.</returns>
    public static async ValueTask<IReadOnlyList<SectionMeshData>> MeshColumnsParallelAsync(
        IReadOnlyList<ChunkNeighborhood> neighborhoods,
        IBlockRegistry blocks,
        TextureAtlasData atlas,
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

        await Parallel.ForAsync(
            0,
            neighborhoods.Count,
            cancellationToken,
            (index, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                var nb = neighborhoods[index];
                perChunkResults[index] = MeshColumn(in nb, blocks, atlas);
                return ValueTask.CompletedTask;
            }).ConfigureAwait(false);

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
