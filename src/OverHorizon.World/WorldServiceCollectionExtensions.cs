using Microsoft.Extensions.DependencyInjection;
using OverHorizon.Core;
using OverHorizon.Core.Registry;
using OverHorizon.World.ChunkManagement;
using OverHorizon.World.Generation;
using OverHorizon.World.Serialization;

namespace OverHorizon.World;

/// <summary>
/// Dependency injection registration extensions for the world module.
/// </summary>
public static class WorldServiceCollectionExtensions
{
    /// <summary>
    /// Registers world generation, chunk storage, chunk manager, and world tick subsystem into <paramref name="services"/>.
    /// </summary>
    /// <param name="services">Target service collection.</param>
    /// <param name="worldSeed">Deterministic 64-bit world seed.</param>
    /// <param name="worldSaveDirectory">Optional disk directory for persistent chunk storage (<see langword="null"/> uses in-memory storage).</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddWorldModule(
        this IServiceCollection services,
        long worldSeed = 0x5F3759DF42424242L,
        string? worldSaveDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IWorldGenerator>(sp =>
            new OverworldGenerator(worldSeed, sp.GetRequiredService<CoreRegistries>()));

        if (string.IsNullOrWhiteSpace(worldSaveDirectory))
        {
            services.AddSingleton<IChunkStorage>(static sp =>
                new InMemoryChunkStorage(sp.GetRequiredService<CoreRegistries>()));
        }
        else
        {
            var dir = worldSaveDirectory;
            services.AddSingleton<IChunkStorage>(sp =>
                new FileChunkStorage(dir, sp.GetRequiredService<CoreRegistries>()));
        }

        services.AddSingleton<ChunkManager>(static sp => new ChunkManager(
            sp.GetRequiredService<CoreRegistries>(),
            sp.GetRequiredService<IWorldGenerator>(),
            sp.GetRequiredService<IChunkStorage>()));

        services.AddSingleton<IChunkManager>(static sp => sp.GetRequiredService<ChunkManager>());
        services.AddSingleton<IWorldSubsystem>(static sp => new WorldSubsystem(sp.GetRequiredService<ChunkManager>()));
        services.AddSingleton<ITickSubsystem>(static sp => sp.GetRequiredService<IWorldSubsystem>());

        return services;
    }
}
