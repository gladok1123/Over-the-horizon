using OverHorizon.Modding.Commands;
using OverHorizon.Modding.Events;

namespace OverHorizon.Modding;

internal sealed class ModContext(
    IModRegistrationContext registries,
    IEventBus events,
    ICommandDispatcher commands) : IModContext
{
    public IModRegistrationContext Registries { get; } = registries;

    public IEventBus Events { get; } = events;

    public ICommandDispatcher Commands { get; } = commands;
}
