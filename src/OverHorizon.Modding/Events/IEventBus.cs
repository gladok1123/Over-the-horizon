namespace OverHorizon.Modding.Events;

/// <summary>
/// Lock-free, zero-allocation (on publish) strongly-typed event bus without reflection.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Subscribes <paramref name="callback"/> to <typeparamref name="TEvent"/> and returns an <see cref="IDisposable"/> handle that unsubscribes on dispose.
    /// </summary>
    /// <typeparam name="TEvent">Event value type.</typeparam>
    /// <param name="callback">Subscriber callback.</param>
    /// <param name="priority">Dispatch priority order.</param>
    /// <param name="ignoreCancelled">When <see langword="true"/>, skips invocation if an earlier handler cancelled the event (ignored for <see cref="EventPriority.Monitor"/>).</param>
    /// <returns>Disposable subscription token.</returns>
    IDisposable Subscribe<TEvent>(
        GameEventAction<TEvent> callback,
        EventPriority priority = EventPriority.Normal,
        bool ignoreCancelled = true)
        where TEvent : struct, IGameEvent;

    /// <summary>
    /// Publishes <paramref name="payload"/> synchronously by reference to all active subscribers in deterministic priority order.
    /// </summary>
    /// <typeparam name="TEvent">Event value type.</typeparam>
    /// <param name="payload">Event payload passed by reference.</param>
    void Publish<TEvent>(ref TEvent payload)
        where TEvent : struct, IGameEvent;
}
