namespace OverHorizon.Modding.Events;

/// <summary>
/// Marks an <see cref="IGameEvent"/> whose default engine side-effect can be vetoed by subscribers.
/// </summary>
public interface ICancellableEvent : IGameEvent
{
    /// <summary>Gets or sets a value indicating whether the action has been cancelled.</summary>
    bool IsCancelled { get; set; }
}
