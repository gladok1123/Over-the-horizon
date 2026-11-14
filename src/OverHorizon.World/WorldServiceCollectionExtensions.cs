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

        // One independent chunk manager (generator + storage) per dimension: Overworld and Nether.
        services.AddSingleton(static sp => BuildDimensions(sp, worldSeed, worldSaveDirectory));

        services.AddSingleton(static sp => sp.GetRequiredService<DimensionWorldCollection>()[DimensionId.Overworld]);
        services.AddSingleton<IChunkManager>(static sp => sp.GetRequiredService<ChunkManager>());
        services.AddSingleton<IWorldSubsystem>(static sp => new WorldSubsystem(sp.GetRequiredService<DimensionWorldCollection>()));
        services.AddSingleton<ITickSubsystem>(static sp => sp.GetRequiredService<IWorldSubsystem>());

        return services;
    }

    private static DimensionWorldCollection BuildDimensions(
        IServiceProvider services,
        long worldSeed,
        string? worldSaveDirectory)
    {
        var registries = services.GetRequiredService<CoreRegistries>();
        var collection = new DimensionWorldCollection();

        collection.Register(DimensionId.Overworld, new ChunkManager(
            registries,
            new OverworldGenerator(worldSeed, registries),
            CreateStorage(registries, worldSaveDirectory, DimensionId.Overworld),
            DimensionId.Overworld));

        collection.Register(DimensionId.Nether, new ChunkManager(
            registries,
            new NetherGenerator(worldSeed, registries),
            CreateStorage(registries, worldSaveDirectory, DimensionId.Nether),
            DimensionId.Nether));

        return collection;
    }

    private static IChunkStorage CreateStorage(
        CoreRegistries registries,
        string? worldSaveDirectory,
        DimensionId dimension)
    {
        if (string.IsNullOrWhiteSpace(worldSaveDirectory))
        {
            return new InMemoryChunkStorage(registries);
        }

        // Overworld chunks stay at the save root for backwards compatibility; the Nether gets its own DIM-1 folder.
        var root = dimension == DimensionId.Nether
            ? Path.Combine(worldSaveDirectory, "DIM-1")
            : worldSaveDirectory;
        return new FileChunkStorage(root, registries);
    }
}
