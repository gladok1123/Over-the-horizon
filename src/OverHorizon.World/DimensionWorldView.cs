using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.World.ChunkManagement;

namespace OverHorizon.World;

/// <summary>
/// Read/write facade pinning an <see cref="IWorldSubsystem"/> to one <see cref="DimensionId"/> so that
/// dimension-agnostic consumers (world bridges, tickers, Redstone, mob AI) transparently operate on the
/// Nether simply by swapping the active view.
/// </summary>
public sealed class DimensionWorldView : IWorldSubsystem
{
    private readonly IWorldSubsystem _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="DimensionWorldView"/> class.
    /// </summary>
    /// <param name="inner">Underlying multi-dimension world subsystem.</param>
    /// <param name="dimension">Dimension every delegated call is pinned to.</param>
    public DimensionWorldView(IWorldSubsystem inner, DimensionId dimension = DimensionId.Overworld)
    {
        ArgumentNullException.ThrowIfNull(inner);

        _inner = inner;
        Dimension = dimension;
    }

    /// <summary>Gets the dimension this view is pinned to.</summary>
    public DimensionId Dimension { get; }

    /// <inheritdoc />
    public string Name => _inner.Name;

    /// <inheritdoc />
    public int PhaseOrder => _inner.PhaseOrder;

    /// <summary>Gets the underlying multi-dimension world subsystem.</summary>
    public IWorldSubsystem Inner => _inner;

    /// <inheritdoc />
    public IChunkManager Chunks => _inner.GetChunks(Dimension);

    /// <inheritdoc />
    public IChunkManager GetChunks(DimensionId dimension) => _inner.GetChunks(dimension);

    /// <inheritdoc />
    public BlockState GetBlockState(BlockPos worldPos) => _inner.GetBlockState(worldPos, Dimension);

    /// <inheritdoc />
    public bool SetBlockState(BlockPos worldPos, BlockState newState, long tickNumber = 0) =>
        _inner.SetBlockState(worldPos, newState, tickNumber, Dimension);

    /// <inheritdoc />
    public int SetBlockStatesBatch(ReadOnlySpan<(BlockPos Position, BlockState State)> changes, long tickNumber = 0) =>
        _inner.SetBlockStatesBatch(changes, tickNumber, Dimension);

    /// <inheritdoc />
    public ValueTask TickAsync(long tickNumber, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
