using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Modding;
using OverHorizon.Modding.Commands;
using OverHorizon.Modding.Events;
using OverHorizon.Networking;
using OverHorizon.Networking.Sessions;
using OverHorizon.Physics;
using OverHorizon.Shared;
using OverHorizon.World;
using Serilog;

namespace OverHorizon.Server;

/// <summary>
/// Composition root for configuring and building the dedicated server host.
/// </summary>
public static class ServerBootstrap
{
    /// <summary>
    /// Parses command-line arguments into <see cref="ServerOptions"/> without runtime reflection.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Parsed <see cref="ServerOptions"/> instance.</returns>
    public static ServerOptions ParseOptions(ReadOnlySpan<string> args)
    {
        long? maxTicks = null;
        int? listenPort = null;
        string? dataPackDir = null;
        string? worldDir = null;
        var viewDistance = 1;
        var worldSeed = 0x5F3759DF42424242L;
        var defaultPerm = PermissionLevel.Everyone;

        var i = 0;
        while (i < args.Length)
        {
            var token = args[i];
            if (string.Equals(token, "--max-ticks", StringComparison.Ordinal) &&
                i + 1 < args.Length &&
                long.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedTicks) &&
                parsedTicks >= 0)
            {
                maxTicks = parsedTicks;
                i += 2;
                continue;
            }

            if (string.Equals(token, "--port", StringComparison.Ordinal) &&
                i + 1 < args.Length &&
                int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPort) &&
                parsedPort is >= 0 and <= ushort.MaxValue)
            {
                listenPort = parsedPort;
                i += 2;
                continue;
            }

            if (string.Equals(token, "--view-distance", StringComparison.Ordinal) &&
                i + 1 < args.Length &&
                int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var vd) &&
                vd is >= 1 and <= 16)
            {
                viewDistance = vd;
                i += 2;
                continue;
            }

            if (string.Equals(token, "--seed", StringComparison.Ordinal) &&
                i + 1 < args.Length &&
                long.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSeed))
            {
                worldSeed = parsedSeed;
                i += 2;
                continue;
            }

            if (string.Equals(token, "--world-dir", StringComparison.Ordinal) &&
                i + 1 < args.Length &&
                !string.IsNullOrWhiteSpace(args[i + 1]))
            {
                worldDir = args[i + 1];
                i += 2;
                continue;
            }

            if (string.Equals(token, "--datapacks", StringComparison.Ordinal) &&
                i + 1 < args.Length &&
                !string.IsNullOrWhiteSpace(args[i + 1]))
            {
                dataPackDir = args[i + 1];
                i += 2;
                continue;
            }

            if (string.Equals(token, "--op-all", StringComparison.Ordinal))
            {
                defaultPerm = PermissionLevel.Operator;
            }

            i++;
        }

        return new ServerOptions(
            MaxTicks: maxTicks,
            ListenPort: listenPort,
            PlayerViewDistanceChunks: viewDistance,
            DefaultPlayerPermission: defaultPerm,
            DataPackDirectory: dataPackDir,
            WorldSeed: worldSeed,
            WorldSaveDirectory: worldDir);
    }

    /// <summary>
    /// Registers all dedicated server modules and services into <paramref name="services"/>.
    /// </summary>
    /// <param name="services">Target service collection.</param>
    /// <param name="options">Optional server options override.</param>
    /// <param name="mods">Optional code or datapack <see cref="IModInitializer"/> plugins to load prior to registry freeze.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection ConfigureServices(
        IServiceCollection services,
        ServerOptions? options = null,
        IEnumerable<IModInitializer>? mods = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var effectiveOptions = options ?? new ServerOptions();
        services.AddSingleton(effectiveOptions);
        services.AddSingleton<IClock>(_ => new MonotonicClock());

        if (mods is not null)
        {
            foreach (var mod in mods)
            {
                services.AddSingleton(mod);
            }
        }

        services.AddModdingModule(effectiveOptions.DataPackDirectory);
        services.AddSingleton<CoreRegistries>(static sp => sp.GetRequiredService<IModHost>().Registries);
        services.AddSingleton<IBlockRegistry>(static sp => sp.GetRequiredService<CoreRegistries>().Blocks);

        services.AddSingleton<ServerPlaySessionBridge>(static sp =>
        {
            var bridge = new ServerPlaySessionBridge(
                sp.GetRequiredService<IWorldSubsystem>(),
                sp.GetRequiredService<IGameplaySubsystem>(),
                sp.GetRequiredService<IVoxelCollisionReader>(),
                sp.GetRequiredService<CoreRegistries>(),
                sp.GetRequiredService<ServerOptions>(),
                sp.GetRequiredService<IEventBus>(),
                sp.GetRequiredService<ICommandDispatcher>(),
                sp.GetRequiredService<INetworkServer>);

            BuiltInServerCommands.RegisterAll(
                sp.GetRequiredService<ICommandDispatcher>(),
                sp.GetRequiredService<IWorldSubsystem>(),
                sp.GetRequiredService<IGameplaySubsystem>(),
                sp.GetRequiredService<CoreRegistries>(),
                bridge,
                sp.GetRequiredService<INetworkServer>);

            return bridge;
        });
        services.AddSingleton<IServerPlayHandler>(static sp => sp.GetRequiredService<ServerPlaySessionBridge>());

        services.AddNetworkingModule();
        services.AddGameplayModule();
        services.AddPhysicsModule();
        services.AddWorldModule(effectiveOptions.WorldSeed, effectiveOptions.WorldSaveDirectory);

        services.AddSingleton<ServerTickLoopService>(static sp => new ServerTickLoopService(
            sp.GetServices<ITickSubsystem>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<ServerOptions>(),
            sp.GetRequiredService<IHostApplicationLifetime>(),
            sp.GetRequiredService<ILogger<ServerTickLoopService>>(),
            sp.GetService<INetworkServer>(),
            sp.GetService<IEventBus>(),
            sp.GetService<IWorldSubsystem>()));
        services.AddSingleton<IHostedService>(static sp => sp.GetRequiredService<ServerTickLoopService>());

        return services;
    }

    /// <summary>
    /// Builds an AOT-compatible dedicated server <see cref="IHost"/>.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="options">Optional server options override.</param>
    /// <param name="mods">Optional <see cref="IModInitializer"/> instances.</param>
    /// <returns>Configured <see cref="IHost"/> ready to run.</returns>
    public static IHost BuildHost(
        string[] args,
        ServerOptions? options = null,
        IEnumerable<IModInitializer>? mods = null)
    {
        ArgumentNullException.ThrowIfNull(args);

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

        var effectiveOptions = options ?? ParseOptions(args);
        ConfigureServices(builder.Services, effectiveOptions, mods);

        return builder.Build();
    }
}
