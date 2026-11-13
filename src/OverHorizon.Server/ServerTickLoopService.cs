using System.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OverHorizon.Core;
using OverHorizon.Modding.Events;
using OverHorizon.Networking;
using OverHorizon.Shared;
using OverHorizon.World;

namespace OverHorizon.Server;

/// <summary>
/// Hosted 20 TPS deterministic simulation loop for the dedicated server with periodic and shutdown world persistence.
/// </summary>
public sealed class ServerTickLoopService : BackgroundService
{
    private const long AutoSaveIntervalTicks = 300; // Every 15 seconds at 20 TPS

    private readonly ITickSubsystem[] _subsystems;
    private readonly IClock _clock;
    private readonly ServerOptions _options;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<ServerTickLoopService> _logger;
    private readonly INetworkServer? _networkServer;
    private readonly IEventBus? _eventBus;
    private readonly IWorldSubsystem? _worldSubsystem;
    private readonly ServerPlaySessionBridge? _playBridge;
    private long _completedTicks;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerTickLoopService"/> class.
    /// </summary>
    public ServerTickLoopService(
        IEnumerable<ITickSubsystem> subsystems,
        IClock clock,
        ServerOptions options,
        IHostApplicationLifetime lifetime,
        ILogger<ServerTickLoopService> logger,
        INetworkServer? networkServer = null,
        IEventBus? eventBus = null,
        IWorldSubsystem? worldSubsystem = null,
        ServerPlaySessionBridge? playBridge = null)
    {
        ArgumentNullException.ThrowIfNull(subsystems);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(lifetime);
        ArgumentNullException.ThrowIfNull(logger);

        if (options.TicksPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "TicksPerSecond must be positive.");
        }

        _subsystems = subsystems
            .OrderBy(static s => s.PhaseOrder)
            .ThenBy(static s => s.Name, StringComparer.Ordinal)
            .ToArray();
        _clock = clock;
        _options = options;
        _lifetime = lifetime;
        _logger = logger;
        _networkServer = networkServer;
        _eventBus = eventBus;
        _worldSubsystem = worldSubsystem;
        _playBridge = playBridge;
    }

    /// <summary>
    /// Gets the total number of ticks completed since startup.
    /// </summary>
    public long CompletedTicks => Interlocked.Read(ref _completedTicks);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.ListenPort is { } port && _networkServer is not null && _networkServer.BoundEndPoint is null)
        {
            var bound = await _networkServer.StartListeningAsync(new IPEndPoint(IPAddress.Any, port), stoppingToken)
                .ConfigureAwait(false);
            ServerLog.ListeningReady(
                _logger,
                bound.Port,
                _options.PlayerViewDistanceChunks,
                _options.WorldSeed,
                _options.WorldSaveDirectory ?? "in-memory");
        }

        var tickBudget = TimeSpan.FromSeconds(1.0 / _options.TicksPerSecond);
        var budgetMs = tickBudget.TotalMilliseconds;

        ServerLog.ServerStarting(_logger, _options.TicksPerSecond, _subsystems.Length);

        using var timer = new PeriodicTimer(tickBudget);
        long tickNumber = 0;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_options.MaxTicks is { } maxTicks && tickNumber >= maxTicks)
                {
                    _lifetime.StopApplication();
                    break;
                }

                var startTimestamp = _clock.GetTimestamp();

                for (var i = 0; i < _subsystems.Length; i++)
                {
                    await _subsystems[i].TickAsync(tickNumber, stoppingToken).ConfigureAwait(false);
                }

                if (_eventBus is not null)
                {
                    var tickEvent = new ServerTickEvent(tickNumber);
                    _eventBus.Publish(ref tickEvent);
                }

                if (_playBridge is not null)
                {
                    _playBridge.WorldTime.AdvanceSeconds(1.0 / _options.TicksPerSecond);
                    if (_networkServer is not null && (tickNumber % 100) == 0)
                    {
                        _networkServer.BroadcastPlayPacket(
                            new Networking.Packets.Clientbound.WorldTimeUpdatePacket(_playBridge.WorldTime.TotalTicks));
                    }
                }

                if (_worldSubsystem is not null &&
                    !string.IsNullOrWhiteSpace(_options.WorldSaveDirectory) &&
                    tickNumber > 0 &&
                    (tickNumber % AutoSaveIntervalTicks) == 0)
                {
                    var saved = await _worldSubsystem.Chunks.SaveDirtyChunksAsync(stoppingToken).ConfigureAwait(false);
                    if (saved > 0)
                    {
                        ServerLog.WorldSaved(_logger, saved);
                    }
                }

                var elapsed = _clock.GetElapsedTime(startTimestamp);
                if (elapsed > tickBudget)
                {
                    ServerLog.TickOverrun(_logger, tickNumber, elapsed.TotalMilliseconds, budgetMs);
                }

                tickNumber++;
                Interlocked.Exchange(ref _completedTicks, tickNumber);

                if (_options.MaxTicks is { } limit && tickNumber >= limit)
                {
                    _lifetime.StopApplication();
                    break;
                }

                var hasNextTick = await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
                if (!hasNextTick)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during graceful shutdown.
        }
        finally
        {
            if (_worldSubsystem is not null && !string.IsNullOrWhiteSpace(_options.WorldSaveDirectory))
            {
                var finalSaved = await _worldSubsystem.Chunks.SaveDirtyChunksAsync(CancellationToken.None)
                    .ConfigureAwait(false);
                if (finalSaved > 0)
                {
                    ServerLog.WorldSaved(_logger, finalSaved);
                }
            }

            ServerLog.ServerStopped(_logger, Interlocked.Read(ref _completedTicks));
        }
    }
}
