using System.Globalization;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Physics;
using OverHorizon.World.ChunkManagement;

namespace OverHorizon.Gameplay;

/// <summary>
/// Lock-free <see cref="IVoxelCollisionReader"/> adapter backed by <see cref="IChunkManager"/> snapshots
/// and L1-cache-resident precomputed <see cref="BlockState"/> solidity/fluid tables.
/// </summary>
public sealed class WorldCollisionReader : IVoxelCollisionReader
{
    private readonly IChunkManager _chunks;
    private readonly bool[] _isSolidByStateId;
    private readonly sbyte[] _fluidLevelByStateId;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldCollisionReader"/> class.
    /// </summary>
    /// <param name="chunks">Chunk manager providing lock-free <c>ChunkSnapshot</c>s.</param>
    /// <param name="blocks">Frozen block registry.</param>
    public WorldCollisionReader(IChunkManager chunks, IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        ArgumentNullException.ThrowIfNull(blocks);

        _chunks = chunks;
        var stateCount = blocks.StateCount;
        _isSolidByStateId = new bool[stateCount];
        _fluidLevelByStateId = new sbyte[stateCount];

        for (ushort id = 0; id < stateCount; id++)
        {
            var meta = blocks.GetStateMetadata(new BlockState(id));
            _isSolidByStateId[id] = meta.IsSolid;

            if (meta.IsFluid)
            {
                var parsedLevel = meta.Properties.TryGetValue("level", out var lvlStr) &&
                    sbyte.TryParse(lvlStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lvl)
                    ? lvl
                    : (sbyte)0;
                _fluidLevelByStateId[id] = parsedLevel;
            }
            else
            {
                _fluidLevelByStateId[id] = -1;
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
}
