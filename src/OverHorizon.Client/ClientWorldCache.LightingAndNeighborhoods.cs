using OverHorizon.Core.Math;
using OverHorizon.Rendering;
using OverHorizon.Rendering.Meshing;
using OverHorizon.World.Lighting;
using OverHorizon.World.Storage;

namespace OverHorizon.Client;

public sealed partial class ClientWorldCache
{
    private bool TryBuildNeighborhood(ChunkPos pos, out ChunkNeighborhood nb)
    {
        if (!_publishedSnapshots.TryGetValue(pos, out var center))
        {
            nb = default;
            return false;
        }

        _ = _publishedSnapshots.TryGetValue(new ChunkPos(pos.X, pos.Z - 1), out var north);
        _ = _publishedSnapshots.TryGetValue(new ChunkPos(pos.X, pos.Z + 1), out var south);
        _ = _publishedSnapshots.TryGetValue(new ChunkPos(pos.X - 1, pos.Z), out var west);
        _ = _publishedSnapshots.TryGetValue(new ChunkPos(pos.X + 1, pos.Z), out var east);
        nb = new ChunkNeighborhood(center, north, south, west, east);
        return true;
    }

    private void RemeshColumnSectionRange(ChunkPos pos, int minSec, int maxSec, IRenderSubsystem renderer)
    {
        if (!TryBuildNeighborhood(pos, out var nb)) return;
        for (var s = minSec; s <= maxSec; s++)
        {
            var mesh = GreedySectionMesher.MeshSection(in nb, s, _blocks, _atlas);
            renderer.UploadOrReplaceSectionMesh(mesh);
        }
    }

    private void RelightMutableColumnAndCardinals(ChunkPos pos, ChunkColumn center, int minY, int maxY)
    {
        _ = _mutableColumns.TryGetValue(new ChunkPos(pos.X, pos.Z - 1), out var north);
        _ = _mutableColumns.TryGetValue(new ChunkPos(pos.X, pos.Z + 1), out var south);
        _ = _mutableColumns.TryGetValue(new ChunkPos(pos.X - 1, pos.Z), out var west);
        _ = _mutableColumns.TryGetValue(new ChunkPos(pos.X + 1, pos.Z), out var east);

        LightingEngine.RefreshChunkLightBand(center, minY, maxY, _blocks);
        _ = LightingEngine.PropagateCrossChunkLightBand(center, north, south, west, east, minY, maxY, _blocks);
        _publishedSnapshots[pos] = center.CreateSnapshot();

        RelightSingleNeighbor(north, minY, maxY);
        RelightSingleNeighbor(south, minY, maxY);
        RelightSingleNeighbor(west, minY, maxY);
        RelightSingleNeighbor(east, minY, maxY);
    }

    private void RelightSingleNeighbor(ChunkColumn? neighbor, int minY, int maxY)
    {
        if (neighbor is null) return;
        var np = neighbor.Position;
        _ = _mutableColumns.TryGetValue(new ChunkPos(np.X, np.Z - 1), out var nn);
        _ = _mutableColumns.TryGetValue(new ChunkPos(np.X, np.Z + 1), out var ns);
        _ = _mutableColumns.TryGetValue(new ChunkPos(np.X - 1, np.Z), out var nw);
        _ = _mutableColumns.TryGetValue(new ChunkPos(np.X + 1, np.Z), out var ne);

        LightingEngine.RefreshChunkLightBand(neighbor, minY, maxY, _blocks);
        _ = LightingEngine.PropagateCrossChunkLightBand(neighbor, nn, ns, nw, ne, minY, maxY, _blocks);
        _publishedSnapshots[np] = neighbor.CreateSnapshot();
    }

    private void MarkColumnAndCardinalsDirty(ChunkPos pos)
    {
        _ = _dirtyMeshColumns.Add(pos);
        MarkIfLoaded(new ChunkPos(pos.X, pos.Z - 1));
        MarkIfLoaded(new ChunkPos(pos.X, pos.Z + 1));
        MarkIfLoaded(new ChunkPos(pos.X - 1, pos.Z));
        MarkIfLoaded(new ChunkPos(pos.X + 1, pos.Z));
    }

    private void MarkIfLoaded(ChunkPos pos)
    {
        if (_publishedSnapshots.ContainsKey(pos))
        {
            _ = _dirtyMeshColumns.Add(pos);
        }
    }
}
