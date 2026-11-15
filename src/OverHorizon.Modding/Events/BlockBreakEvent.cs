using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;

namespace OverHorizon.Modding.Events;

/// <summary>
/// Cancellable event fired before a player breaks a block in the world.
/// </summary>
/// <param name="SessionId">Initiating player session ID.</param>
/// <param name="PlayerName">Player username.</param>
/// <param name="Position">Target block position.</param>
/// <param name="ExistingState">Current block state before destruction.</param>
/// <param name="IsCancelled">Whether destruction is vetoed.</param>
public record struct BlockBreakEvent(
    long SessionId,
    string PlayerName,
    BlockPos Position,
    BlockState ExistingState,
    bool IsCancelled = false) : ICancellableEvent
{
    /// <inheritdoc />
    public readonly string EventName => "overhorizon:block_break";
}
