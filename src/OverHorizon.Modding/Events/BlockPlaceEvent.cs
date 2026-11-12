using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;

namespace OverHorizon.Modding.Events;

/// <summary>
/// Cancellable and mutable event fired before a player places a block in the world.
/// Subscribers may cancel placement or replace <see cref="PlacedState"/>.
/// </summary>
/// <param name="SessionId">Initiating player session ID.</param>
/// <param name="PlayerName">Player username.</param>
/// <param name="Position">Target block position.</param>
/// <param name="PlacedState">Block state to place (modifiable by subscribers).</param>
/// <param name="IsCancelled">Whether placement is vetoed.</param>
public record struct BlockPlaceEvent(
    long SessionId,
    string PlayerName,
    BlockPos Position,
    BlockState PlacedState,
    bool IsCancelled = false) : ICancellableEvent
{
    /// <inheritdoc />
    public readonly string EventName => "overhorizon:block_place";
}
