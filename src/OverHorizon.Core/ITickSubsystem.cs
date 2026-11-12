namespace OverHorizon.Core;

/// <summary>
/// Represents a deterministic, cancellation-aware simulation subsystem executed during a server tick.
/// </summary>
public interface ITickSubsystem
{
    /// <summary>
    /// Gets the diagnostic name of the subsystem.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the deterministic execution phase order (lower values run earlier in the tick).
    /// </summary>
    int PhaseOrder { get; }

    /// <summary>
    /// Executes one simulation tick for this subsystem.
    /// </summary>
    /// <param name="tickNumber">Zero-based monotonically increasing tick index.</param>
    /// <param name="cancellationToken">Token used to cancel ongoing tick work.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask TickAsync(long tickNumber, CancellationToken cancellationToken);
}
