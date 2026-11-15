using Microsoft.Extensions.DependencyInjection;
using OverHorizon.Core;

namespace OverHorizon.Physics;

/// <summary>
/// Dependency injection registration extensions for the physics module.
/// </summary>
public static class PhysicsServiceCollectionExtensions
{
    /// <summary>
    /// Registers physics subsystem services into the container.
    /// </summary>
    /// <param name="services">Target service collection.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddPhysicsModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IPhysicsSubsystem>(_ => new PhysicsSubsystem());
        services.AddSingleton<ITickSubsystem>(static sp => sp.GetRequiredService<IPhysicsSubsystem>());
        return services;
    }
}
