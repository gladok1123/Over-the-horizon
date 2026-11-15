using OverHorizon.Core.Registry;
using OverHorizon.Modding.Commands;
using OverHorizon.Modding.DataPacks;
using OverHorizon.Modding.Events;
using OverHorizon.Modding.Loading;
using OverHorizon.Modding.Manifest;

namespace OverHorizon.Modding;

/// <summary>
/// Concrete <see cref="IModHost"/> resolving mod dependencies, running pre-freeze content registrations,
/// and hosting <see cref="IEventBus"/> and <see cref="ICommandDispatcher"/>.
/// </summary>
public sealed class ModHost : IModHost
{
    private readonly ModManifest[] _loadedMods;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModHost"/> class, executing all <paramref name="mods"/>
    /// (plus any JSON datapacks in <paramref name="dataPackDirectory"/>) in topological dependency order before freezing registries.
    /// </summary>
    /// <param name="mods">Code or parsed datapack <see cref="IModInitializer"/> instances.</param>
    /// <param name="dataPackDirectory">Optional directory scanned for <c>*.datapack.json</c> files.</param>
    /// <param name="eventBus">Optional event bus override.</param>
    /// <param name="commandDispatcher">Optional command dispatcher override.</param>
    public ModHost(
        IEnumerable<IModInitializer>? mods = null,
        string? dataPackDirectory = null,
        IEventBus? eventBus = null,
        ICommandDispatcher? commandDispatcher = null)
    {
        Events = eventBus ?? new EventBus();
        Commands = commandDispatcher ?? new CommandDispatcher();

        var combined = new List<IModInitializer>();
        if (mods is not null)
        {
            combined.AddRange(mods);
        }

        if (!string.IsNullOrWhiteSpace(dataPackDirectory))
        {
            combined.AddRange(DataPackLoader.LoadFromDirectory(dataPackDirectory));
        }

        var ordered = ModDependencySorter.Sort(combined);
        var regContext = new ModRegistrationContext();
        var modContext = new ModContext(regContext, Events, Commands);

        _loadedMods = new ModManifest[ordered.Count];
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Initialize(modContext);
            _loadedMods[i] = ordered[i].Manifest;
        }

        Registries = regContext.FreezeAndBuild();
    }

    /// <inheritdoc />
    public int LoadedModCount => _loadedMods.Length;

    /// <inheritdoc />
    public IReadOnlyList<ModManifest> LoadedMods => _loadedMods;

    /// <inheritdoc />
    public IEventBus Events { get; }

    /// <inheritdoc />
    public ICommandDispatcher Commands { get; }

    /// <inheritdoc />
    public CoreRegistries Registries { get; }
}
