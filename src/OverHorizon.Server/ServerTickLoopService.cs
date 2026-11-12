using System.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OverHorizon.Core;
using OverHorizon.Modding.Events;
using OverHorizon.Networking;
using OverHorizon.Shared;

namespace OverHorizon.Server;

/// <summary>
/// Hosted 20 TPS deterministic simulation loop for the dedicated server.
/// </summary>
public sealed class ServerTickLoopService : BackgroundService
{
    private readonly ITickSubsystem[] _subsystems;
    private readonly IClock _clock;
    private readonly ServerOptions _options;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<ServerTickLoopService> _logger;
    private readonly INetworkServer? _networkServer;
    private readonly IEventBus? _eventBus;
    private long _completedTicks;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerTickLoopService"/> class.
    /// </summary>
    /// <param name="subsystems">Registered tick subsystems.</param>
    /// <param name="clock">Monotonic clock used to measure tick duration.</param>
    /// <param name="options">Server tick configuration.</param>
    /// <param name="lifetime">Host application lifetime controller.</param>
    /// <param name="logger">Structured logger instance.</param>
    /// <param name="networkServer">Optional network server started when <see cref="ServerOptions.ListenPort"/> is specified.</param>
    /// <param name="eventBus">Optional mod event bus receiving <see cref="ServerTickEvent"/> notifications.</param>
    public ServerTickLoopService(
        IEnumerable<ITickSubsystem> subsystems,
        IClock clock,
        ServerOptions options,
        IHostApplicationLifetime lifetime,
        ILogger<ServerTickLoopService> logger,
        INetworkServer? networkServer = null,
        IEventBus? eventBus = null)
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
            _ = await _networkServer.StartListeningAsync(new IPEndPoint(IPAddress.Any, port), stoppingToken)
                .ConfigureAwait(false);
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
            ServerLog.ServerStopped(_logger, Interlocked.Read(ref _completedTicks));
        }
    }
}
