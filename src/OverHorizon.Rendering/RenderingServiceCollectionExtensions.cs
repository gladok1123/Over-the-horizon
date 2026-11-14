using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OverHorizon.Rendering.Backends;

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

        services.AddSingleton<IRenderSubsystem>(static sp =>
        {
            var opts = sp.GetRequiredService<RenderingOptions>();
            return opts.Headless
                ? new HeadlessRenderSubsystem()
                : new SilkOpenGlRenderSubsystem(opts, sp.GetRequiredService<ILogger<SilkOpenGlRenderSubsystem>>());
        });

        return services;
    }
}
