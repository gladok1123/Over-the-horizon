using System.Globalization;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay.Survival;
using OverHorizon.Physics;
using OverHorizon.World.ChunkManagement;

namespace OverHorizon.Gameplay;

/// <summary>
/// Lock-free <see cref="IVoxelCollisionReader"/> and <see cref="IVoxelSelectionReader"/> adapter backed by
/// <see cref="IChunkManager"/> snapshots and L1-cache-resident precomputed solidity, fluid, and sub-voxel selection tables.
/// </summary>
public sealed class WorldCollisionReader : IVoxelSelectionReader
{
    private readonly IChunkManager _chunks;
    private readonly bool[] _isSolidByStateId;
    private readonly sbyte[] _fluidLevelByStateId;
    private readonly bool[] _hasSelectionByStateId;
    private readonly BoundingBox[] _localSelectionBoxByStateId;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldCollisionReader"/> class.
    /// </summary>
    public WorldCollisionReader(IChunkManager chunks, IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        ArgumentNullException.ThrowIfNull(blocks);

        _chunks = chunks;
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

    /// <inheritdoc />
    public bool IsColumnLoaded(BlockPos pos) =>
        _chunks.TryGetSnapshot(pos.ToChunkPos(), out _);

    /// <inheritdoc />
    public bool IsSolidBlock(BlockPos pos)
    {
        if (pos.Y < EngineConstants.WorldMinY || pos.Y >= EngineConstants.WorldMaxY)
        {
            return false;
        }

        if (!_chunks.TryGetSnapshot(pos.ToChunkPos(), out var snapshot))
        {
            return false;
        }

        var state = snapshot.GetBlockState(pos.SectionLocalX, pos.Y, pos.SectionLocalZ);
        return (uint)state.Id < (uint)_isSolidByStateId.Length && _isSolidByStateId[state.Id];
    }

    /// <inheritdoc />
    public int GetFluidLevelOrNegative(BlockPos pos)
    {
        if (pos.Y < EngineConstants.WorldMinY || pos.Y >= EngineConstants.WorldMaxY)
        {
            return -1;
        }

        if (!_chunks.TryGetSnapshot(pos.ToChunkPos(), out var snapshot))
        {
            return -1;
        }

        var state = snapshot.GetBlockState(pos.SectionLocalX, pos.Y, pos.SectionLocalZ);
        return (uint)state.Id < (uint)_fluidLevelByStateId.Length ? _fluidLevelByStateId[state.Id] : -1;
    }

    /// <inheritdoc />
    public bool TryGetSelectionBox(BlockPos pos, out BoundingBox worldBox)
    {
        worldBox = default;
        if (pos.Y < EngineConstants.WorldMinY || pos.Y >= EngineConstants.WorldMaxY)
        {
            return false;
        }

        if (!_chunks.TryGetSnapshot(pos.ToChunkPos(), out var snapshot))
        {
            return false;
        }

        var state = snapshot.GetBlockState(pos.SectionLocalX, pos.Y, pos.SectionLocalZ);
        if ((uint)state.Id >= (uint)_hasSelectionByStateId.Length || !_hasSelectionByStateId[state.Id])
        {
            return false;
        }

        var local = _localSelectionBoxByStateId[state.Id];
        worldBox = local.Move(new Vec3d(pos.X, pos.Y, pos.Z));
        return true;
    }

    private static sbyte ParseFluidLevel(BlockStateMetadata meta) =>
        meta.Properties.TryGetValue("level", out var lvlStr) &&
        sbyte.TryParse(lvlStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lvl)
            ? lvl
            : (sbyte)0;
}
