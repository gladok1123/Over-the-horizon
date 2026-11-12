using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OverHorizon.Audio;

/// <summary>
/// Dependency injection registration extensions for the client audio module.
/// </summary>
public static class AudioServiceCollectionExtensions
{
    /// <summary>
    /// Registers client audio subsystem services into the container.
    /// </summary>
    /// <param name="services">Target service collection.</param>
    /// <param name="externalAssetsPath">Optional directory, <c>.zip</c>, or <c>.jar</c> containing sound overrides.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddAudioModule(this IServiceCollection services, string? externalAssetsPath = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var assets = externalAssetsPath;
        services.AddSingleton<IAudioSubsystem>(sp =>
            new OpenAlAudioSubsystem(sp.GetRequiredService<ILogger<OpenAlAudioSubsystem>>(), assets));
        return services;
    }
}
