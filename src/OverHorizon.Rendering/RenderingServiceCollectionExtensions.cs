using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OverHorizon.Rendering.Backends;
using OverHorizon.Rendering.Backends.Vulkan;

namespace OverHorizon.Rendering;

/// <summary>
/// Dependency injection registration extensions for the client rendering module.
/// </summary>
public static class RenderingServiceCollectionExtensions
{
    /// <summary>
    /// Registers client rendering subsystem services into the container.
    /// </summary>
    /// <param name="services">Target service collection.</param>
    /// <param name="options">Optional rendering options override.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddRenderingModule(
        this IServiceCollection services,
        RenderingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (options is not null)
        {
            services.AddSingleton(options);
        }
        else
        {
            services.TryAddSingleton(static _ => new RenderingOptions());
        }

        services.AddSingleton<IWindowRenderSubsystem>(static sp =>
        {
            var opts = sp.GetRequiredService<RenderingOptions>();
            return opts.Headless
                ? new HeadlessRenderSubsystem()
                : new VulkanRenderSubsystem(opts, sp.GetRequiredService<ILogger<VulkanRenderSubsystem>>());
        });

        // Windowed consumers (menu, session runner) need the richer contract; everything else only needs
        // IRenderSubsystem, so both registrations resolve to the same singleton instance.
        services.AddSingleton<IRenderSubsystem>(static sp => sp.GetRequiredService<IWindowRenderSubsystem>());

        return services;
    }
}
