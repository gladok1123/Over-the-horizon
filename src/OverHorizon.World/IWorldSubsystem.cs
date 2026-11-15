using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.World.ChunkManagement;

namespace OverHorizon.World;

/// <summary>
/// Manages chunks, world generation, lighting, Copy-on-Write snapshots, and persistence during server ticks.
/// </summary>
public interface IWorldSubsystem : ITickSubsystem, IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Gets the underlying <see cref="IChunkManager"/> for the Overworld dimension.
    /// </summary>
    IChunkManager Chunks { get; }

    /// <summary>
    /// Resolves the <see cref="IChunkManager"/> responsible for <paramref name="dimension"/>.
    /// </summary>
    /// <param name="dimension">Target dimension.</param>
    /// <returns>Dimension-scoped chunk manager.</returns>
    IChunkManager GetChunks(DimensionId dimension);

    /// <summary>
    /// Reads the <see cref="BlockState"/> at <paramref name="worldPos"/> from the Overworld snapshot,
    /// returning <see cref="BlockState.Air"/> if the chunk is not loaded or outside vertical bounds.
    /// </summary>
    /// <param name="worldPos">World block coordinate.</param>
    /// <returns>Block state at <paramref name="worldPos"/>.</returns>
    BlockState GetBlockState(BlockPos worldPos);

    /// <summary>
    /// Reads the <see cref="BlockState"/> at <paramref name="worldPos"/> inside <paramref name="dimension"/>.
    /// </summary>
    /// <param name="worldPos">World block coordinate.</param>
    /// <param name="dimension">Target dimension.</param>
    /// <returns>Block state at <paramref name="worldPos"/>.</returns>
    BlockState GetBlockState(BlockPos worldPos, DimensionId dimension);

    /// <summary>
    /// Updates the <see cref="BlockState"/> at <paramref name="worldPos"/> in the Overworld if the containing chunk is loaded.
    /// </summary>
    /// <param name="worldPos">World block coordinate.</param>
    /// <param name="newState">New block state.</param>
    /// <param name="tickNumber">Current simulation tick.</param>
    /// <returns><see langword="true"/> if applied.</returns>
    bool SetBlockState(BlockPos worldPos, BlockState newState, long tickNumber = 0);

    /// <summary>
    /// Updates the <see cref="BlockState"/> at <paramref name="worldPos"/> inside <paramref name="dimension"/>.
    /// </summary>
    /// <param name="worldPos">World block coordinate.</param>
    /// <param name="newState">New block state.</param>
    /// <param name="tickNumber">Current simulation tick.</param>
    /// <param name="dimension">Target dimension.</param>
    /// <returns><see langword="true"/> if applied.</returns>
    bool SetBlockState(BlockPos worldPos, BlockState newState, long tickNumber, DimensionId dimension);

    /// <summary>
    /// Applies a batch of Overworld block modifications across loaded chunks and refreshes lighting and snapshots once per touched chunk.
    /// </summary>
    /// <param name="changes">Span of world block coordinate and target state pairs.</param>
    /// <param name="tickNumber">Current simulation tick.</param>
    /// <returns>Number of blocks whose state changed.</returns>
    int SetBlockStatesBatch(ReadOnlySpan<(BlockPos Position, BlockState State)> changes, long tickNumber = 0);

    /// <summary>
    /// Applies a batch of block modifications inside <paramref name="dimension"/>.
    /// </summary>
    /// <param name="changes">Span of world block coordinate and target state pairs.</param>
    /// <param name="tickNumber">Current simulation tick.</param>
    /// <param name="dimension">Target dimension.</param>
    /// <returns>Number of blocks whose state changed.</returns>
    int SetBlockStatesBatch(ReadOnlySpan<(BlockPos Position, BlockState State)> changes, long tickNumber, DimensionId dimension);
}
