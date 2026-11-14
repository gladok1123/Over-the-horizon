namespace OverHorizon.Modding.Events;

/// <summary>
/// Fired on the main simulation thread at the end of each completed server tick.
/// </summary>
/// <param name="TickNumber">Completed zero-based simulation tick number.</param>
public record struct ServerTickEvent(long TickNumber) : IGameEvent
{
    /// <inheritdoc />
    public readonly string EventName => "overhorizon:server_tick";
}
