using OverHorizon.Core.Registry;
using OverHorizon.Modding.Commands;
using OverHorizon.Modding.Events;
using OverHorizon.Modding.Manifest;

namespace OverHorizon.Modding;

/// <summary>
/// Hosts code plugins, data-driven JSON packs, the strongly-typed <see cref="IEventBus"/>, and the <see cref="ICommandDispatcher"/>.
/// </summary>
public interface IModHost
{
    /// <summary>Gets the number of loaded mods and datapacks.</summary>
    int LoadedModCount { get; }

    /// <summary>Gets the manifests of all loaded mods in deterministic initialization order.</summary>
    IReadOnlyList<ModManifest> LoadedMods { get; }

    /// <summary>Gets the strongly-typed engine event bus.</summary>
    IEventBus Events { get; }

    /// <summary>Gets the server slash-command registry and dispatcher.</summary>
    ICommandDispatcher Commands { get; }

    /// <summary>Gets the frozen domain registries containing both built-in and mod-registered content.</summary>
    CoreRegistries Registries { get; }
}
