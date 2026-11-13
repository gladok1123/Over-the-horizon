using System.Collections.Concurrent;
using System.Globalization;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay.Survival;
using OverHorizon.Physics;
using OverHorizon.Rendering;
using OverHorizon.Rendering.Meshing;
using OverHorizon.Rendering.Textures;
using OverHorizon.World.Lighting;
using OverHorizon.World.Storage;

namespace OverHorizon.Client;

/// <summary>
/// Lock-free client-side chunk snapshot cache, <see cref="IVoxelCollisionReader"/>, <see cref="IVoxelSelectionReader"/>,
/// Copy-on-Write section-diff tracker, and instant single-section block-edit remesher.
/// </summary>
public sealed class ClientWorldCache : IVoxelCollisionReader, IVoxelSelectionReader
{
    private readonly IBlockRegistry _blocks;
    private readonly TextureAtlasData _atlas;
    private readonly Dictionary<ChunkPos, ChunkColumn> _mutableColumns = [];
    private readonly ConcurrentDictionary<ChunkPos, ChunkSnapshot> _publishedSnapshots = [];
    private readonly HashSet<ChunkPos> _dirtyMeshColumns = [];
    private readonly bool[] _isSolidByStateId;
    private readonly sbyte[] _fluidLevelByStateId;
    private readonly bool[] _hasSelectionByStateId;
    private readonly BoundingBox[] _localSelectionBoxByStateId;

    /// <summary>Initializes a new instance of the <see cref="ClientWorldCache"/> class.</summary>
    public ClientWorldCache(IBlockRegistry blocks, TextureAtlasData atlas)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(atlas);

        _blocks = blocks;
        _atlas = atlas;

        var stateCount = blocks.StateCount;
        _isSolidByStateId = new bool[stateCount];
        _fluidLevelByStateId = new sbyte[stateCount];
        _hasSelectionByStateId = new bool[stateCount];
        _localSelectionBoxByStateId = new BoundingBox[stateCount];

        for (ushort id = 0; id < stateCount; id++)
        {
            var state = new BlockState(id);
            var meta = blocks.GetStateMetadata(state);
            _isSolidByStateId[id] = meta.IsSolid;
            _fluidLevelByStateId[id] = meta.IsFluid ? ParseFluidLevel(meta) : (sbyte)-1;

            if (BlockAttachmentAndShapeRules.TryGetLocalSelectionBox(blocks, state, out var localBox))
            {
                _hasSelectionByStateId[id] = true;
                _localSelectionBoxByStateId[id] = localBox;
            }
        }
    }

    /// <summary>Gets the number of chunk columns currently cached on the client.</summary>
    public int LoadedChunkCount => _publishedSnapshots.Count;

    /// <summary>Gets the number of chunk columns queued for remeshing.</summary>
    public int PendingRemeshCount => _dirtyMeshColumns.Count;

    /// <summary>Clears all cached client chunks and GPU meshes when switching worlds or servers.</summary>
    public void ClearAll(IRenderSubsystem renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        foreach (var pos in _publishedSnapshots.Keys.ToArray())
        {
            renderer.RemoveChunkMeshes(pos);
        }

        _publishedSnapshots.Clear();
        _mutableColumns.Clear();
        _dirtyMeshColumns.Clear();
    }

    /// <summary>Installs or replaces a full <see cref="ChunkColumn"/> received from multiplayer streaming.</summary>
    public void UpsertColumn(ChunkColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);

        var pos = column.Position;
        _mutableColumns[pos] = column;
        RelightMutableColumnAndCardinals(pos, column, EngineConstants.WorldMinY, EngineConstants.WorldMaxY - 1);
        MarkColumnAndCardinalsDirty(pos);
    }

    /// <summary>
    /// Installs an immutable <see cref="ChunkSnapshot"/> from local singleplayer streaming.
    /// Uses Copy-on-Write section reference equality to skip columns whose sections did not change.
    /// </summary>
    public void UpsertSnapshot(ChunkSnapshot snapshot, bool queueFullColumnRemesh = true)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var pos = snapshot.Position;
        if (_publishedSnapshots.TryGetValue(pos, out var existing))
        {
            if (ReferenceEquals(existing, snapshot) || HaveIdenticalSections(existing, snapshot))
            {
                return;
            }

            _publishedSnapshots[pos] = snapshot;
            if (queueFullColumnRemesh)
            {
                _ = _dirtyMeshColumns.Add(pos);
            }

            return;
        }

        _publishedSnapshots[pos] = snapshot;
        MarkColumnAndCardinalsDirty(pos);
    }

    /// <summary>
    /// Applies an authoritative single-block update to a multiplayer column if cached, using bounded Y-band relighting.
    /// </summary>
    public bool ApplyBlockUpdate(BlockPos worldPos, BlockState newState)
    {
        if (worldPos.Y < EngineConstants.WorldMinY || worldPos.Y >= EngineConstants.WorldMaxY)
        {
            return false;
        }

        var chunkPos = worldPos.ToChunkPos();
        if (!_mutableColumns.TryGetValue(chunkPos, out var column))
        {
            return false;
        }

        var lx = worldPos.SectionLocalX;
        var lz = worldPos.SectionLocalZ;
        var oldStopY = column.GetHeight(HeightmapType.LightBlocking, lx, lz);
        _ = column.SetBlockState(lx, worldPos.Y, lz, newState, _blocks);
        var newStopY = column.GetHeight(HeightmapType.LightBlocking, lx, lz);
        var minY = Math.Max(EngineConstants.WorldMinY, Math.Min(worldPos.Y, Math.Min(oldStopY, newStopY)) - 16);
        var maxY = Math.Min(EngineConstants.WorldMaxY - 1, Math.Max(worldPos.Y, Math.Max(oldStopY, newStopY)) + 16);

        RelightMutableColumnAndCardinals(chunkPos, column, minY, maxY);
        _ = _dirtyMeshColumns.Add(chunkPos);
        if (lx == 0) MarkIfLoaded(new ChunkPos(chunkPos.X - 1, chunkPos.Z));
        if (lx == 15) MarkIfLoaded(new ChunkPos(chunkPos.X + 1, chunkPos.Z));
        if (lz == 0) MarkIfLoaded(new ChunkPos(chunkPos.X, chunkPos.Z - 1));
        if (lz == 15) MarkIfLoaded(new ChunkPos(chunkPos.X, chunkPos.Z + 1));
        return true;
    }

    /// <summary>
    /// Immediately remeshes and uploads ONLY the <c>16x16x16</c> section(s) affected by a block edit at <paramref name="editedPos"/>,
    /// delivering zero-latency, zero-stutter block breaking and placement without touching unrelated sections.
    /// </summary>
    public void RemeshAroundBlockImmediate(BlockPos editedPos, IRenderSubsystem renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        if (editedPos.Y < EngineConstants.WorldMinY || editedPos.Y >= EngineConstants.WorldMaxY)
        {
            return;
        }

        var centerChunk = editedPos.ToChunkPos();
        var secIdx = (editedPos.Y - EngineConstants.WorldMinY) >> 4;
        var localY = (editedPos.Y - EngineConstants.WorldMinY) & 15;
        var minSec = localY == 0 ? Math.Max(0, secIdx - 1) : secIdx;
        var maxSec = localY == 15 ? Math.Min(ChunkColumn.SectionCount - 1, secIdx + 1) : secIdx;

        RemeshColumnSectionRange(centerChunk, minSec, maxSec, renderer);
        _ = _dirtyMeshColumns.Remove(centerChunk);

        var lx = editedPos.SectionLocalX;
        var lz = editedPos.SectionLocalZ;
        if (lx <= 1) RemeshColumnSectionRange(new ChunkPos(centerChunk.X - 1, centerChunk.Z), minSec, maxSec, renderer);
        if (lx >= 14) RemeshColumnSectionRange(new ChunkPos(centerChunk.X + 1, centerChunk.Z), minSec, maxSec, renderer);
        if (lz <= 1) RemeshColumnSectionRange(new ChunkPos(centerChunk.X, centerChunk.Z - 1), minSec, maxSec, renderer);
        if (lz >= 14) RemeshColumnSectionRange(new ChunkPos(centerChunk.X, centerChunk.Z + 1), minSec, maxSec, renderer);
    }

    /// <summary>Evicts cached chunk columns outside Chebyshev distance <paramref name="keepRadius"/>.</summary>
    public int EvictOutOfRangeChunks(ChunkPos center, int keepRadius, IRenderSubsystem renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        var evicted = 0;
        foreach (var pos in _publishedSnapshots.Keys.Where(pos => center.ChessboardDistance(pos) > keepRadius))
        {
            if (_publishedSnapshots.TryRemove(pos, out _))
            {
                _ = _mutableColumns.Remove(pos);
                _ = _dirtyMeshColumns.Remove(pos);
                renderer.RemoveChunkMeshes(pos);
                evicted++;
            }
        }

        return evicted;
    }

    /// <summary>Reads the current <see cref="BlockState"/> at <paramref name="worldPos"/>, or <see cref="BlockState.Air"/> if unloaded.</summary>
    public BlockState GetBlockState(BlockPos worldPos)
    {
        if (worldPos.Y < EngineConstants.WorldMinY || worldPos.Y >= EngineConstants.WorldMaxY)
        {
            return BlockState.Air;
        }

        return _publishedSnapshots.TryGetValue(worldPos.ToChunkPos(), out var snap)
            ? snap.GetBlockState(worldPos.SectionLocalX, worldPos.Y, worldPos.SectionLocalZ)
            : BlockState.Air;
    }

    /// <summary>Reads sky and block light nibbles (<c>0..15</c>) at <paramref name="worldPos"/>.</summary>
    public (byte SkyLight, byte BlockLight) GetLight(BlockPos worldPos)
    {
        if (worldPos.Y < EngineConstants.WorldMinY || worldPos.Y >= EngineConstants.WorldMaxY)
        {
            return (15, 0);
        }

        return _publishedSnapshots.TryGetValue(worldPos.ToChunkPos(), out var snap)
            ? (snap.GetSkyLight(worldPos.SectionLocalX, worldPos.Y, worldPos.SectionLocalZ),
               snap.GetBlockLight(worldPos.SectionLocalX, worldPos.Y, worldPos.SectionLocalZ))
            : ((byte)15, (byte)0);
    }

    /// <summary>Dequeues up to <paramref name="maxColumns"/> dirty <see cref="ChunkNeighborhood"/> snapshots for background meshing.</summary>
    public IReadOnlyList<ChunkNeighborhood> DequeueDirtyNeighborhoods(int maxColumns = 16, ChunkPos? priorityCenter = null)
    {
        if (_dirtyMeshColumns.Count == 0 || maxColumns <= 0)
        {
            return [];
        }

        List<ChunkPos> batchPositions;
        if (priorityCenter is { } centerChunk && _dirtyMeshColumns.Count > maxColumns)
        {
            batchPositions = _dirtyMeshColumns
                .OrderBy(pos => centerChunk.ChessboardDistance(pos))
                .Take(maxColumns)
                .ToList();
        }
        else
        {
            batchPositions = new List<ChunkPos>(Math.Min(maxColumns, _dirtyMeshColumns.Count));
            foreach (var pos in _dirtyMeshColumns)
            {
                batchPositions.Add(pos);
                if (batchPositions.Count >= maxColumns) break;
            }
        }

        var neighborhoods = new List<ChunkNeighborhood>(batchPositions.Count);
        foreach (var pos in batchPositions)
        {
            _ = _dirtyMeshColumns.Remove(pos);
            if (TryBuildNeighborhood(pos, out var nb))
            {
                neighborhoods.Add(nb);
            }
        }

        return neighborhoods;
    }

    /// <summary>Meshes up to <paramref name="maxColumns"/> dirty chunk columns in parallel across worker threads.</summary>
    public async ValueTask<int> RebuildDirtyMeshesAsync(
        IRenderSubsystem renderer,
        int maxColumns = 16,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        var neighborhoods = DequeueDirtyNeighborhoods(maxColumns);
        if (neighborhoods.Count == 0)
        {
            return 0;
        }

        var meshes = await ParallelChunkMesher.MeshColumnsParallelAsync(
            neighborhoods,
            _blocks,
            _atlas,
            cancellationToken).ConfigureAwait(false);

        foreach (var nb in neighborhoods)
        {
            renderer.RemoveChunkMeshes(nb.Center.Position);
        }

        foreach (var mesh in meshes)
        {
            renderer.UploadOrReplaceSectionMesh(mesh);
        }

        return neighborhoods.Count;
    }

    /// <inheritdoc />
    public bool IsColumnLoaded(BlockPos pos) =>
        _publishedSnapshots.ContainsKey(pos.ToChunkPos());

    /// <inheritdoc />
    public bool IsSolidBlock(BlockPos pos)
    {
        if (pos.Y < EngineConstants.WorldMinY || pos.Y >= EngineConstants.WorldMaxY) return false;
        if (!_publishedSnapshots.TryGetValue(pos.ToChunkPos(), out var snap)) return false;
        var state = snap.GetBlockState(pos.SectionLocalX, pos.Y, pos.SectionLocalZ);
        return (uint)state.Id < (uint)_isSolidByStateId.Length && _isSolidByStateId[state.Id];
    }

    /// <inheritdoc />
    public int GetFluidLevelOrNegative(BlockPos pos)
    {
        if (pos.Y < EngineConstants.WorldMinY || pos.Y >= EngineConstants.WorldMaxY) return -1;
        if (!_publishedSnapshots.TryGetValue(pos.ToChunkPos(), out var snap)) return -1;
        var state = snap.GetBlockState(pos.SectionLocalX, pos.Y, pos.SectionLocalZ);
        return (uint)state.Id < (uint)_fluidLevelByStateId.Length ? _fluidLevelByStateId[state.Id] : -1;
    }

    /// <inheritdoc />
    public bool TryGetSelectionBox(BlockPos pos, out BoundingBox worldBox)
    {
        worldBox = default;
        if (pos.Y < EngineConstants.WorldMinY || pos.Y >= EngineConstants.WorldMaxY) return false;
        if (!_publishedSnapshots.TryGetValue(pos.ToChunkPos(), out var snap)) return false;
        var state = snap.GetBlockState(pos.SectionLocalX, pos.Y, pos.SectionLocalZ);
        if ((uint)state.Id >= (uint)_hasSelectionByStateId.Length || !_hasSelectionByStateId[state.Id]) return false;
        worldBox = _localSelectionBoxByStateId[state.Id].Translate(new Vec3d(pos.X, pos.Y, pos.Z));
        return true;
    }

    private static bool HaveIdenticalSections(ChunkSnapshot a, ChunkSnapshot b)
    {
        for (var i = 0; i < ChunkColumn.SectionCount; i++)
        {
            if (!ReferenceEquals(a.Sections[i], b.Sections[i])) return false;
        }

        return true;
    }

    private static sbyte ParseFluidLevel(BlockStateMetadata meta) =>
        meta.Properties.TryGetValue("level", out var lvlStr) &&
        sbyte.TryParse(lvlStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lvl)
            ? lvl
            : (sbyte)0;

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
