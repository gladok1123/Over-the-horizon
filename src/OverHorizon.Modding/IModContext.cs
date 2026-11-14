using OverHorizon.Modding.Commands;
using OverHorizon.Modding.Events;

namespace OverHorizon.Modding;

/// <summary>
/// Initialization context passed to <see cref="IModInitializer.Initialize"/> during startup.
/// </summary>
public interface IModContext
{
    /// <summary>Gets the pre-freeze content registration API.</summary>
    IModRegistrationContext Registries { get; }

    /// <summary>Gets the strongly-typed engine event bus.</summary>
    IEventBus Events { get; }

    /// <summary>Gets the slash-command registry and dispatcher.</summary>
    ICommandDispatcher Commands { get; }
}
