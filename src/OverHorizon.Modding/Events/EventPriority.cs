namespace OverHorizon.Modding.Events;

/// <summary>
/// Execution priority for <see cref="IEventBus"/> subscribers, ordered from <see cref="Lowest"/> (runs first)
/// to <see cref="Monitor"/> (runs last to observe final outcome).
/// </summary>
public enum EventPriority : byte
{
    /// <summary>First handlers to execute; lower priorities can override their decisions.</summary>
    Lowest = 0,

    /// <summary>Early priority.</summary>
    Low = 1,

    /// <summary>Default subscriber priority.</summary>
    Normal = 2,

    /// <summary>Late priority capable of overriding normal handlers.</summary>
    High = 3,

    /// <summary>Final decision-making priority before monitors.</summary>
    Highest = 4,

    /// <summary>Read-only observation phase executed last regardless of <c>ignoreCancelled</c>.</summary>
    Monitor = 5,
}
