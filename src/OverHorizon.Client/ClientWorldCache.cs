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
public sealed partial class ClientWorldCache : IVoxelSelectionReader
{
    private readonly IBlockRegistry _blocks;
    private readonly TextureAtlasData _atlas;
    private readonly Dictionary<ChunkPos, ChunkColumn> _mutableColumns = [];
    private readonly ConcurrentDictionary<ChunkPos, ChunkSnapshot> _publishedSnapshots = [];
    private readonly ConcurrentDictionary<ChunkPos, uint> _columnGenerations = [];
    private readonly HashSet<ChunkPos> _dirtyMeshColumns = [];
    private readonly bool[] _isSolidByStateId;
    private readonly sbyte[] _fluidLevelByStateId;
    private readonly bool[] _hasSelectionByStateId;
    private readonly BoundingBox[] _localSelectionBoxByStateId;
    private readonly BlockCollisionShapeTable _collisionShapes;

    /// <summary>Initializes a new instance of the <see cref="ClientWorldCache"/> class.</summary>
    public ClientWorldCache(IBlockRegistry blocks, TextureAtlasData atlas)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(atlas);

        _blocks = blocks;
        _atlas = atlas;
        _collisionShapes = new BlockCollisionShapeTable(blocks);

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
        _columnGenerations.Clear();
        _dirtyMeshColumns.Clear();
    }

    /// <summary>Marks all currently loaded chunk columns dirty (e.g. when toggling X-Ray mode).</summary>
    public void MarkAllLoadedChunksDirty()
    {
        foreach (var pos in _publishedSnapshots.Keys)
        {
            BumpGeneration(pos);
            _ = _dirtyMeshColumns.Add(pos);
        }
    }

    /// <summary>Returns the current monotonic mesh generation for <paramref name="pos"/>.</summary>
    public uint GetColumnGeneration(ChunkPos pos) =>
        _columnGenerations.TryGetValue(pos, out var gen) ? gen : 0U;

    private void BumpGeneration(ChunkPos pos) =>
        _ = _columnGenerations.AddOrUpdate(pos, 1U, static (_, prev) => unchecked(prev + 1U));

    /// <summary>Installs or replaces a full <see cref="ChunkColumn"/> received from multiplayer streaming.</summary>
    public void UpsertColumn(ChunkColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);

        var pos = column.Position;
        _mutableColumns[pos] = column;
        BumpGeneration(pos);
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
            BumpGeneration(pos);
            if (queueFullColumnRemesh)
            {
                _ = _dirtyMeshColumns.Add(pos);
            }

            return;
        }

        _publishedSnapshots[pos] = snapshot;
        BumpGeneration(pos);
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
        BumpGeneration(chunkPos);
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

        BumpGeneration(centerChunk);
        RemeshColumnSectionRange(centerChunk, minSec, maxSec, renderer);
        _ = _dirtyMeshColumns.Remove(centerChunk);

        var lx = editedPos.SectionLocalX;
        var lz = editedPos.SectionLocalZ;
        if (lx <= 1)
        {
            var wPos = new ChunkPos(centerChunk.X - 1, centerChunk.Z);
            BumpGeneration(wPos);
            RemeshColumnSectionRange(wPos, minSec, maxSec, renderer);
        }

        if (lx >= 14)
        {
            var ePos = new ChunkPos(centerChunk.X + 1, centerChunk.Z);
            BumpGeneration(ePos);
            RemeshColumnSectionRange(ePos, minSec, maxSec, renderer);
        }

        if (lz <= 1)
        {
            var nPos = new ChunkPos(centerChunk.X, centerChunk.Z - 1);
            BumpGeneration(nPos);
            RemeshColumnSectionRange(nPos, minSec, maxSec, renderer);
        }

        if (lz >= 14)
        {
            var sPos = new ChunkPos(centerChunk.X, centerChunk.Z + 1);
            BumpGeneration(sPos);
            RemeshColumnSectionRange(sPos, minSec, maxSec, renderer);
        }
    }

    /// <summary>
    /// Deduplicates and remeshes all <c>(ChunkPos, SectionIndex)</c> cells affected by a multi-block edit batch
    /// (such as breaking/placing a 2-block Door, Bed, or Redstone cascade) so each section is meshed at most once.
    /// </summary>
    public void RemeshEditedBlocksBatchImmediate(ReadOnlySpan<BlockPos> editedPositions, IRenderSubsystem renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        if (editedPositions.Length == 0)
        {
            return;
        }

        if (editedPositions.Length == 1)
        {
            RemeshAroundBlockImmediate(editedPositions[0], renderer);
            return;
        }

        var sectionRanges = new Dictionary<ChunkPos, (int MinSec, int MaxSec)>(4);
        void IncludeRange(ChunkPos cp, int s0, int s1)
        {
            if (sectionRanges.TryGetValue(cp, out var cur))
            {
                sectionRanges[cp] = (Math.Min(cur.MinSec, s0), Math.Max(cur.MaxSec, s1));
            }
            else
            {
                sectionRanges[cp] = (s0, s1);
            }
        }

        for (var i = 0; i < editedPositions.Length; i++)
        {
            var editedPos = editedPositions[i];
            if (editedPos.Y < EngineConstants.WorldMinY || editedPos.Y >= EngineConstants.WorldMaxY) continue;

            var centerChunk = editedPos.ToChunkPos();
            var secIdx = (editedPos.Y - EngineConstants.WorldMinY) >> 4;
            var localY = (editedPos.Y - EngineConstants.WorldMinY) & 15;
            var minSec = localY == 0 ? Math.Max(0, secIdx - 1) : secIdx;
            var maxSec = localY == 15 ? Math.Min(ChunkColumn.SectionCount - 1, secIdx + 1) : secIdx;

            IncludeRange(centerChunk, minSec, maxSec);
            _ = _dirtyMeshColumns.Remove(centerChunk);

            var lx = editedPos.SectionLocalX;
            var lz = editedPos.SectionLocalZ;
            if (lx <= 1) IncludeRange(new ChunkPos(centerChunk.X - 1, centerChunk.Z), minSec, maxSec);
            if (lx >= 14) IncludeRange(new ChunkPos(centerChunk.X + 1, centerChunk.Z), minSec, maxSec);
            if (lz <= 1) IncludeRange(new ChunkPos(centerChunk.X, centerChunk.Z - 1), minSec, maxSec);
            if (lz >= 14) IncludeRange(new ChunkPos(centerChunk.X, centerChunk.Z + 1), minSec, maxSec);
        }

        foreach (var (cp, (minSec, maxSec)) in sectionRanges)
        {
            BumpGeneration(cp);
            RemeshColumnSectionRange(cp, minSec, maxSec, renderer);
        }
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
    public bool TryGetCollisionBox(BlockPos pos, out BoundingBox worldBox)
    {
        worldBox = default;
        if (pos.Y < EngineConstants.WorldMinY || pos.Y >= EngineConstants.WorldMaxY) return false;
        if (!_publishedSnapshots.TryGetValue(pos.ToChunkPos(), out var snap)) return false;
        var state = snap.GetBlockState(pos.SectionLocalX, pos.Y, pos.SectionLocalZ);
        return _collisionShapes.TryGetWorldBox(state, pos, out worldBox);
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
        worldBox = _localSelectionBoxByStateId[state.Id].Move(new Vec3d(pos.X, pos.Y, pos.Z));
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
}
