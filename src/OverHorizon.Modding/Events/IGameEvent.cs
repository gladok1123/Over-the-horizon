namespace OverHorizon.Modding.Events;

/// <summary>
/// Base contract for strongly-typed value-type engine events dispatched through <see cref="IEventBus"/>.
/// </summary>
public interface IGameEvent
{
    /// <summary>Gets the diagnostic identifier of this event kind.</summary>
    string EventName { get; }
}
