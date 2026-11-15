using Microsoft.Extensions.DependencyInjection;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Registry;
using OverHorizon.Physics;
using OverHorizon.World.ChunkManagement;

namespace OverHorizon.Gameplay;

/// <summary>
/// Dependency injection registration extensions for the gameplay and ECS module.
/// </summary>
public static class GameplayServiceCollectionExtensions
{
    /// <summary>
    /// Registers gameplay, world collision adapter, crafting recipes, and Arch ECS subsystem services into the container.
    /// </summary>
    /// <param name="services">Target service collection.</param>
    /// <param name="worldSeed">Deterministic 64-bit world seed.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddGameplayModule(
        this IServiceCollection services,
        long worldSeed = 0x5F3759DF42424242L)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IVoxelCollisionReader>(static sp => new WorldCollisionReader(
            sp.GetRequiredService<IChunkManager>(),
            sp.GetRequiredService<IBlockRegistry>()));

        services.AddSingleton<IGameplaySubsystem>(sp => new GameplaySubsystem(
            worldSeed,
            sp.GetRequiredService<IVoxelCollisionReader>(),
            sp.GetRequiredService<IPhysicsSubsystem>(),
            sp.GetRequiredService<CoreRegistries>()));

        services.AddSingleton<ITickSubsystem>(static sp => sp.GetRequiredService<IGameplaySubsystem>());
        return services;
    }
}
