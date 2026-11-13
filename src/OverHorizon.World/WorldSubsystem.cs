using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.World.ChunkManagement;

namespace OverHorizon.World;

/// <summary>
/// Concrete <see cref="IWorldSubsystem"/> coordinating ticketed chunk loading, snapshots, and periodic dirty saves.
/// </summary>
public sealed class WorldSubsystem : IWorldSubsystem
{
    private const int DefaultMaxChunkLoadsPerTick = 24;
    private const int AutoSaveIntervalTicks = 200; // Every 10 seconds at 20 TPS

    private readonly ChunkManager _chunkManager;
    private readonly int _maxChunkLoadsPerTick;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldSubsystem"/> class.
    /// </summary>
    /// <param name="chunkManager">Chunk manager instance.</param>
    /// <param name="maxChunkLoadsPerTick">Maximum chunks to load or generate per tick.</param>
    public WorldSubsystem(ChunkManager chunkManager, int maxChunkLoadsPerTick = DefaultMaxChunkLoadsPerTick)
    {
        ArgumentNullException.ThrowIfNull(chunkManager);
        if (maxChunkLoadsPerTick <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxChunkLoadsPerTick), "Max chunk loads per tick must be positive.");
        }

        _chunkManager = chunkManager;
        _maxChunkLoadsPerTick = maxChunkLoadsPerTick;
    }

    /// <inheritdoc />
    public string Name => "World";

    /// <inheritdoc />
    public int PhaseOrder => 30;

    /// <inheritdoc />
    public IChunkManager Chunks => _chunkManager;

    /// <inheritdoc />
    public BlockState GetBlockState(BlockPos worldPos)
    {
        if (worldPos.Y < EngineConstants.WorldMinY || worldPos.Y >= EngineConstants.WorldMaxY)
        {
            return BlockState.Air;
        }

        if (!_chunkManager.TryGetSnapshot(worldPos.ToChunkPos(), out var snapshot))
        {
            return BlockState.Air;
        }

        return snapshot.GetBlockState(worldPos.SectionLocalX, worldPos.Y, worldPos.SectionLocalZ);
    }

    /// <inheritdoc />
    public bool SetBlockState(BlockPos worldPos, BlockState newState, long tickNumber = 0) =>
        _chunkManager.SetBlockState(worldPos, newState, tickNumber);

    /// <inheritdoc />
    public async ValueTask TickAsync(long tickNumber, CancellationToken cancellationToken)
    {
        _ = await _chunkManager.ProcessTicketsAsync(_maxChunkLoadsPerTick, cancellationToken).ConfigureAwait(false);

        if (tickNumber > 0 && tickNumber % AutoSaveIntervalTicks == 0)
        {
            _ = await _chunkManager.SaveDirtyChunksAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _chunkManager.DisposeAsync();

    /// <inheritdoc />
    public void Dispose() => _chunkManager.Dispose();
}
