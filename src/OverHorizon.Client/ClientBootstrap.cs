using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OverHorizon.Audio;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Modding;
using OverHorizon.Networking;
using OverHorizon.Physics;
using OverHorizon.Rendering;
using OverHorizon.Shared;
using OverHorizon.World;
using Serilog;

namespace OverHorizon.Client;

/// <summary>
/// Composition root for parsing options, configuring DI services, and building the client <see cref="IHost"/>.
/// </summary>
public static class ClientBootstrap
{
    /// <summary>
    /// Parses command-line arguments into a strongly typed <see cref="ClientOptions"/> record.
    /// </summary>
    /// <param name="args">Command-line tokens.</param>
    /// <returns>Parsed <see cref="ClientOptions"/>.</returns>
    public static ClientOptions ParseOptions(ReadOnlySpan<string> args)
    {
        var headlessCheckOnly = false;
        var headless = false;
        var startInWorld = false;
        int? maxFrames = null;
        string? serverHost = null;
        ushort serverPort = 25565;
        var username = "Explorer";
        var viewDistance = 4;

        var i = 0;
        while (i < args.Length)
        {
            var token = args[i];
            if (string.Equals(token, "--headless-check", StringComparison.Ordinal))
            {
                headlessCheckOnly = true;
                i++;
                continue;
            }

            if (string.Equals(token, "--headless", StringComparison.Ordinal))
            {
                headless = true;
                i++;
                continue;
            }

            if (string.Equals(token, "--singleplayer", StringComparison.Ordinal))
            {
                startInWorld = true;
                i++;
                continue;
            }

            if (string.Equals(token, "--max-frames", StringComparison.Ordinal) && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var frames) && frames > 0)
                {
                    maxFrames = frames;
                }

                i += 2;
                continue;
            }

            if (string.Equals(token, "--connect", StringComparison.Ordinal) && i + 1 < args.Length)
            {
                var endpoint = args[i + 1];
                var colon = endpoint.LastIndexOf(':');
                if (colon > 0 && ushort.TryParse(endpoint.AsSpan(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPort))
                {
                    serverHost = endpoint[..colon];
                    serverPort = parsedPort;
                }
                else
                {
                    serverHost = endpoint;
                }

                i += 2;
                continue;
            }

            if (string.Equals(token, "--username", StringComparison.Ordinal) && i + 1 < args.Length)
            {
                var candidate = args[i + 1];
                if (!string.IsNullOrWhiteSpace(candidate) && candidate.Length <= 16)
                {
                    username = candidate;
                }

                i += 2;
                continue;
            }

            if (string.Equals(token, "--view-distance", StringComparison.Ordinal) && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var vd) && vd is >= 1 and <= 16)
                {
                    viewDistance = vd;
                }

                i += 2;
                continue;
            }

            i++;
        }

        return new ClientOptions(
            HeadlessCheckOnly: headlessCheckOnly,
            Headless: headless,
            MaxFrames: maxFrames,
            ServerHost: serverHost,
            ServerPort: serverPort,
            Username: username,
            ViewDistanceChunks: viewDistance,
            StartInWorld: startInWorld);
    }

    /// <summary>
    /// Registers all client modules and services into <paramref name="services"/>.
    /// </summary>
    public static IServiceCollection ConfigureServices(
        IServiceCollection services,
        ClientOptions? options = null,
        IEnumerable<IModInitializer>? mods = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var resolvedOptions = options ?? new ClientOptions();
        services.AddSingleton(resolvedOptions);
        services.AddSingleton<IClock>(_ => new MonotonicClock());

        if (mods is not null)
        {
            foreach (var mod in mods)
            {
                services.AddSingleton(mod);
            }
        }

        services.AddModdingModule();
        services.AddSingleton<CoreRegistries>(static sp => sp.GetRequiredService<IModHost>().Registries);
        services.AddSingleton<IBlockRegistry>(static sp => sp.GetRequiredService<CoreRegistries>().Blocks);

        services.AddNetworkingModule();
        services.AddGameplayModule();
        services.AddPhysicsModule();
        var saveDir = resolvedOptions.Headless || resolvedOptions.HeadlessCheckOnly ? null : "./saves/SingleplayerWorld";
        services.AddWorldModule(worldSaveDirectory: saveDir);
        services.AddRenderingModule(new RenderingOptions(Headless: resolvedOptions.Headless));
        services.AddAudioModule();
        services.AddSingleton<ClientSessionRunner>();

        return services;
    }

    /// <summary>
    /// Builds the client <see cref="IHost"/>.
    /// </summary>
    public static IHost BuildHost(
        string[] args,
        ClientOptions? options = null,
        IEnumerable<IModInitializer>? mods = null)
    {
        ArgumentNullException.ThrowIfNull(args);

        var resolvedOptions = options ?? ParseOptions(args);

        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
        });

        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
            .CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(serilogLogger, dispose: true);

        ConfigureServices(builder.Services, resolvedOptions, mods);

        return builder.Build();
    }
}
