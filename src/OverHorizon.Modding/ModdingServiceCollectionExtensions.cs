using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OverHorizon.Modding.Commands;
using OverHorizon.Modding.Events;

namespace OverHorizon.Modding;

/// <summary>
/// Dependency injection registration extensions for the modding module.
/// </summary>
public static class ModdingServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IEventBus"/>, <see cref="ICommandDispatcher"/>, and <see cref="IModHost"/> into <paramref name="services"/>.
    /// </summary>
    /// <param name="services">Target service collection.</param>
    /// <param name="dataPackDirectory">Optional filesystem directory scanned for <c>*.datapack.json</c> packs.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddModdingModule(
        this IServiceCollection services,
        string? dataPackDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IEventBus>(static _ => new EventBus());
        services.TryAddSingleton<ICommandDispatcher>(static _ => new CommandDispatcher());
        services.AddSingleton<IModHost>(sp => new ModHost(
            sp.GetServices<IModInitializer>(),
            dataPackDirectory,
            sp.GetRequiredService<IEventBus>(),
            sp.GetRequiredService<ICommandDispatcher>()));

        return services;
    }
}
