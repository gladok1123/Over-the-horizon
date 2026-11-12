using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OverHorizon.Core;
using OverHorizon.Networking.Codec;
using OverHorizon.Networking.Sessions;

namespace OverHorizon.Networking;

/// <summary>
/// Dependency injection registration extensions for the networking module.
/// </summary>
public static class NetworkingServiceCollectionExtensions
{
    /// <summary>
    /// Registers protocol codecs and the asynchronous <see cref="NetworkServer"/> into <paramref name="services"/>.
    /// </summary>
    /// <param name="services">Target service collection.</param>
    /// <param name="options">Optional network configuration override.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddNetworkingModule(
        this IServiceCollection services,
        NetworkServerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(options ?? new NetworkServerOptions());
        services.TryAddSingleton<IProtocolCodec>(static _ => new JavaProtocolCodec());
        services.AddSingleton<NetworkServer>(static sp => new NetworkServer(
            sp.GetRequiredService<NetworkServerOptions>(),
            sp.GetRequiredService<IProtocolCodec>(),
            sp.GetService<IServerPlayHandler>(),
            sp.GetService<ILogger<NetworkServer>>()));
        services.AddSingleton<INetworkServer>(static sp => sp.GetRequiredService<NetworkServer>());
        services.AddSingleton<ITickSubsystem>(static sp => sp.GetRequiredService<INetworkServer>());

        return services;
    }
}
