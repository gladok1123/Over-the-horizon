using System.Collections.Concurrent;
using System.Globalization;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Physics;
using OverHorizon.Rendering;
using OverHorizon.Rendering.Meshing;
using OverHorizon.Rendering.Textures;
using OverHorizon.World.Storage;

namespace OverHorizon.Client;

/// <summary>
/// Lock-free client-side chunk snapshot cache, <see cref="IVoxelCollisionReader"/>, and parallel dirty-section mesh uploader.
/// </summary>
public sealed class ClientWorldCache : IVoxelCollisionReader
{
    private readonly IBlockRegistry _blocks;
    private readonly TextureAtlasData _atlas;
    private readonly Dictionary<ChunkPos, ChunkColumn> _mutableColumns = [];
    private readonly ConcurrentDictionary<ChunkPos, ChunkSnapshot> _publishedSnapshots = [];
    private readonly HashSet<ChunkPos> _dirtyMeshColumns = [];
    private readonly bool[] _isSolidByStateId;
    private readonly sbyte[] _fluidLevelByStateId;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientWorldCache"/> class.
    /// </summary>
    /// <param name="blocks">Frozen block registry.</param>
    /// <param name="atlas">Active procedural block texture atlas.</param>
    public ClientWorldCache(IBlockRegistry blocks, TextureAtlasData atlas)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(atlas);

        _blocks = blocks;
        _atlas = atlas;

        var stateCount = blocks.StateCount;
        _isSolidByStateId = new bool[stateCount];
        _fluidLevelByStateId = new sbyte[stateCount];

        for (ushort id = 0; id < stateCount; id++)
        {
            var meta = blocks.GetStateMetadata(new BlockState(id));
            _isSolidByStateId[id] = meta.IsSolid;
            _fluidLevelByStateId[id] = meta.IsFluid
                ? ParseFluidLevel(meta)
                : (sbyte)-1;
        }
    }

    /// <summary>Gets the number of chunk columns currently cached on the client.</summary>
    public int LoadedChunkCount => _publishedSnapshots.Count;

    /// <summary>Gets the number of chunk columns queued for remeshing.</summary>
    public int PendingRemeshCount => _dirtyMeshColumns.Count;

    /// <summary>
    /// Installs or replaces a full <see cref="ChunkColumn"/> received from multiplayer or singleplayer streaming
    /// and schedules the column plus its 4 cardinal neighbors for parallel meshing.
    /// </summary>
    /// <param name="column">Hydrated chunk column.</param>
    public void UpsertColumn(ChunkColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);

        var pos = column.Position;
        _mutableColumns[pos] = column;
        _publishedSnapshots[pos] = column.CreateSnapshot();
        MarkColumnAndCardinalsDirty(pos);
    }

    /// <summary>
    /// Installs an already-immutable <see cref="ChunkSnapshot"/> from local singleplayer world generation.
    /// </summary>
    /// <param name="snapshot">Published chunk snapshot.</param>
    public void UpsertSnapshot(ChunkSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var pos = snapshot.Position;
        if (_publishedSnapshots.TryGetValue(pos, out var existing) && ReferenceEquals(existing, snapshot))
        {
            return;
        }

        _publishedSnapshots[pos] = snapshot;
        MarkColumnAndCardinalsDirty(pos);
    }

    /// <summary>
    /// Applies an authoritative single-block update to a multiplayer column if cached, refreshing its snapshot and dirty mesh flags.
    /// </summary>
    /// <param name="worldPos">Changed world block coordinate.</param>
    /// <param name="newState">Updated block state.</param>
    /// <returns><see langword="true"/> if the containing chunk was cached and updated.</returns>
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

        _ = column.SetBlockState(worldPos.SectionLocalX, worldPos.Y, worldPos.SectionLocalZ, newState, _blocks);
        _publishedSnapshots[chunkPos] = column.CreateSnapshot();

        _dirtyMeshColumns.Add(chunkPos);
        if (worldPos.SectionLocalX == 0)
        {
            MarkIfLoaded(new ChunkPos(chunkPos.X - 1, chunkPos.Z));
        }
        else if (worldPos.SectionLocalX == 15)
        {
            MarkIfLoaded(new ChunkPos(chunkPos.X + 1, chunkPos.Z));
        }

        if (worldPos.SectionLocalZ == 0)
        {
            MarkIfLoaded(new ChunkPos(chunkPos.X, chunkPos.Z - 1));
        }
        else if (worldPos.SectionLocalZ == 15)
        {
            MarkIfLoaded(new ChunkPos(chunkPos.X, chunkPos.Z + 1));
        }

        return true;
    }

    /// <summary>
    /// Reads the current <see cref="BlockState"/> at <paramref name="worldPos"/>, or <see cref="BlockState.Air"/> if unloaded.
    /// </summary>
    /// <param name="worldPos">World block position.</param>
    /// <returns>Block state at <paramref name="worldPos"/>.</returns>
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

    /// <summary>
    /// Reads sky and block light nibbles (<c>0..15</c>) at <paramref name="worldPos"/>.
    /// </summary>
    /// <param name="worldPos">World block position.</param>
    /// <returns>Tuple of <c>(SkyLight, BlockLight)</c>.</returns>
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

    /// <summary>
    /// Dequeues up to <paramref name="maxColumns"/> dirty <see cref="ChunkNeighborhood"/> snapshots for background meshing.
    /// </summary>
    /// <param name="maxColumns">Maximum dirty columns to pop.</param>
    /// <returns>List of dequeued chunk neighborhoods.</returns>
    public IReadOnlyList<ChunkNeighborhood> DequeueDirtyNeighborhoods(int maxColumns = 16)
    {
        if (_dirtyMeshColumns.Count == 0 || maxColumns <= 0)
        {
            return [];
        }

        var batchPositions = new List<ChunkPos>(Math.Min(maxColumns, _dirtyMeshColumns.Count));
        foreach (var pos in _dirtyMeshColumns)
        {
            batchPositions.Add(pos);
            if (batchPositions.Count >= maxColumns)
            {
                break;
            }
        }

        var neighborhoods = new List<ChunkNeighborhood>(batchPositions.Count);
        foreach (var pos in batchPositions)
        {
            _dirtyMeshColumns.Remove(pos);
            if (!_publishedSnapshots.TryGetValue(pos, out var center))
            {
                continue;
            }

            _ = _publishedSnapshots.TryGetValue(new ChunkPos(pos.X, pos.Z - 1), out var north);
            _ = _publishedSnapshots.TryGetValue(new ChunkPos(pos.X, pos.Z + 1), out var south);
            _ = _publishedSnapshots.TryGetValue(new ChunkPos(pos.X - 1, pos.Z), out var west);
            _ = _publishedSnapshots.TryGetValue(new ChunkPos(pos.X + 1, pos.Z), out var east);

            neighborhoods.Add(new ChunkNeighborhood(center, north, south, west, east));
        }

        return neighborhoods;
    }

    /// <summary>
    /// Meshes up to <paramref name="maxColumns"/> dirty chunk columns in parallel across worker threads
    /// and uploads all resulting section meshes into <paramref name="renderer"/>.
    /// </summary>
    /// <param name="renderer">Target render subsystem.</param>
    /// <param name="maxColumns">Maximum dirty columns to remesh in this batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of chunk columns meshed.</returns>
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

        foreach (var nb in neighborhoods)
        {
            renderer.RemoveChunkMeshes(nb.Center.Position);
        }

        var meshes = await ParallelChunkMesher.MeshColumnsParallelAsync(
            neighborhoods,
            _blocks,
            _atlas,
            cancellationToken).ConfigureAwait(false);

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
        if (pos.Y < EngineConstants.WorldMinY || pos.Y >= EngineConstants.WorldMaxY)
        {
            return false;
        }

        if (!_publishedSnapshots.TryGetValue(pos.ToChunkPos(), out var snap))
        {
            return false;
        }

        var state = snap.GetBlockState(pos.SectionLocalX, pos.Y, pos.SectionLocalZ);
        return (uint)state.Id < (uint)_isSolidByStateId.Length && _isSolidByStateId[state.Id];
    }

    /// <inheritdoc />
    public int GetFluidLevelOrNegative(BlockPos pos)
    {
        if (pos.Y < EngineConstants.WorldMinY || pos.Y >= EngineConstants.WorldMaxY)
        {
            return -1;
        }

        if (!_publishedSnapshots.TryGetValue(pos.ToChunkPos(), out var snap))
        {
            return -1;
        }

        var state = snap.GetBlockState(pos.SectionLocalX, pos.Y, pos.SectionLocalZ);
        return (uint)state.Id < (uint)_fluidLevelByStateId.Length ? _fluidLevelByStateId[state.Id] : -1;
    }

    private static sbyte ParseFluidLevel(BlockStateMetadata meta) =>
        meta.Properties.TryGetValue("level", out var lvlStr) &&
        sbyte.TryParse(lvlStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lvl)
            ? lvl
            : (sbyte)0;

    private void MarkColumnAndCardinalsDirty(ChunkPos pos)
    {
        _dirtyMeshColumns.Add(pos);
        MarkIfLoaded(new ChunkPos(pos.X, pos.Z - 1));
        MarkIfLoaded(new ChunkPos(pos.X, pos.Z + 1));
        MarkIfLoaded(new ChunkPos(pos.X - 1, pos.Z));
        MarkIfLoaded(new ChunkPos(pos.X + 1, pos.Z));
    }

    private void MarkIfLoaded(ChunkPos pos)
    {
        if (_publishedSnapshots.ContainsKey(pos))
        {
            _dirtyMeshColumns.Add(pos);
        }
    }
}
