namespace OverHorizon.Modding.Events;

/// <summary>
/// Allocation-free by-reference callback invoked for subscribers of <typeparamref name="TEvent"/>.
/// </summary>
/// <typeparam name="TEvent">Value-type event struct.</typeparam>
/// <param name="payload">Mutable event struct passed by reference.</param>
public delegate void GameEventAction<TEvent>(ref TEvent payload)
    where TEvent : struct, IGameEvent;
