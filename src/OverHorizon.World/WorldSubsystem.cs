using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.World.ChunkManagement;

namespace OverHorizon.World;

/// <summary>
/// Concrete <see cref="IWorldSubsystem"/> coordinating ticketed chunk loading, snapshots, and periodic dirty saves
/// across every registered <see cref="DimensionId"/> (Overworld + Nether).
/// </summary>
public sealed class WorldSubsystem : IWorldSubsystem
{
    private const int DefaultMaxChunkLoadsPerTick = 24;
    private const int AutoSaveIntervalTicks = 200; // Every 10 seconds at 20 TPS

    private readonly DimensionWorldCollection _dimensions;
    private readonly int _maxChunkLoadsPerTick;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldSubsystem"/> class.
    /// </summary>
    /// <param name="dimensions">Dimension-scoped chunk managers.</param>
    /// <param name="maxChunkLoadsPerTick">Maximum chunks to load or generate per tick and dimension.</param>
    public WorldSubsystem(DimensionWorldCollection dimensions, int maxChunkLoadsPerTick = DefaultMaxChunkLoadsPerTick)
    {
        ArgumentNullException.ThrowIfNull(dimensions);
        if (maxChunkLoadsPerTick <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxChunkLoadsPerTick), "Max chunk loads per tick must be positive.");
        }

        _dimensions = dimensions;
        _maxChunkLoadsPerTick = maxChunkLoadsPerTick;
    }

    /// <summary>
    /// Initializes a single-dimension instance of the <see cref="WorldSubsystem"/> class (test &amp; tooling helper).
    /// </summary>
    /// <param name="chunkManager">Chunk manager for the Overworld.</param>
    /// <param name="maxChunkLoadsPerTick">Maximum chunks to load or generate per tick.</param>
    public WorldSubsystem(ChunkManager chunkManager, int maxChunkLoadsPerTick = DefaultMaxChunkLoadsPerTick)
        : this(CreateSingleDimensionCollection(chunkManager), maxChunkLoadsPerTick)
    {
    }

    /// <inheritdoc />
    public string Name => "World";

    /// <inheritdoc />
    public int PhaseOrder => 30;

    /// <inheritdoc />
    public IChunkManager Chunks => _dimensions[DimensionId.Overworld];

    /// <summary>Gets every dimension currently registered with this world.</summary>
    public IReadOnlyList<DimensionId> Dimensions => _dimensions.Dimensions;

    /// <inheritdoc />
    public IChunkManager GetChunks(DimensionId dimension) => _dimensions[dimension];

    /// <inheritdoc />
    public BlockState GetBlockState(BlockPos worldPos, DimensionId dimension = DimensionId.Overworld)
    {
        if (worldPos.Y < EngineConstants.WorldMinY || worldPos.Y >= EngineConstants.WorldMaxY)
        {
            return BlockState.Air;
        }

        if (!_dimensions[dimension].TryGetSnapshot(worldPos.ToChunkPos(), out var snapshot))
        {
            return BlockState.Air;
        }

        return snapshot.GetBlockState(worldPos.SectionLocalX, worldPos.Y, worldPos.SectionLocalZ);
    }

    /// <inheritdoc />
    public bool SetBlockState(BlockPos worldPos, BlockState newState, long tickNumber = 0, DimensionId dimension = DimensionId.Overworld) =>
        _dimensions[dimension].SetBlockState(worldPos, newState, tickNumber);

    /// <inheritdoc />
    public int SetBlockStatesBatch(ReadOnlySpan<(BlockPos Position, BlockState State)> changes, long tickNumber = 0, DimensionId dimension = DimensionId.Overworld) =>
        _dimensions[dimension].SetBlockStatesBatch(changes, tickNumber);

    /// <inheritdoc />
    public async ValueTask TickAsync(long tickNumber, CancellationToken cancellationToken)
    {
        _ = await _dimensions.ProcessTicketsAsync(_maxChunkLoadsPerTick, cancellationToken).ConfigureAwait(false);

        if (tickNumber > 0 && tickNumber % AutoSaveIntervalTicks == 0)
        {
            _ = await _dimensions.SaveDirtyChunksAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _dimensions.DisposeAsync();

    /// <inheritdoc />
    public void Dispose() => _dimensions.Dispose();

    private static DimensionWorldCollection CreateSingleDimensionCollection(ChunkManager chunkManager)
    {
        ArgumentNullException.ThrowIfNull(chunkManager);

        var dimensions = new DimensionWorldCollection();
        dimensions.Register(DimensionId.Overworld, chunkManager);
        return dimensions;
    }
}
