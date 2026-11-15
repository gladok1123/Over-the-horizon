using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OverHorizon.Networking.Codec;
using OverHorizon.Networking.Packets;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Networking.Packets.Serverbound;
using OverHorizon.Networking.Sessions;
using OverHorizon.Networking.Status;

namespace OverHorizon.Networking;

/// <summary>
/// Horizon-style asynchronous network server using <see cref="System.IO.Pipelines"/> and lock-free <see cref="Channel{T}"/> queues.
/// Drains and processes all gameplay events deterministically on the main thread during Phase 0 (<see cref="PhaseOrder"/> = 0).
/// </summary>
public sealed class NetworkServer : INetworkServer
{
    private readonly NetworkServerOptions _options;
    private readonly IProtocolCodec _codec;
    private readonly IServerPlayHandler? _playHandler;
    private readonly ILogger<NetworkServer> _logger;

    private readonly ConcurrentDictionary<long, NetworkSession> _sessions = new();
    private readonly Channel<InboundSessionEvent> _inboundEvents;
    private readonly CancellationTokenSource _serverCts = new();

    private Socket? _listenerSocket;
    private Task? _acceptLoopTask;
    private long _nextSessionId;
    private int _onlinePlayerCount;
    private int _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="NetworkServer"/> class.
    /// </summary>
    /// <param name="options">Server protocol and framing options.</param>
    /// <param name="codec">Wire protocol codec.</param>
    /// <param name="playHandler">Optional main-thread gameplay bridge.</param>
    /// <param name="logger">Structured logger.</param>
    public NetworkServer(
        NetworkServerOptions? options = null,
        IProtocolCodec? codec = null,
        IServerPlayHandler? playHandler = null,
        ILogger<NetworkServer>? logger = null)
    {
        _options = options ?? new NetworkServerOptions();
        _codec = codec ?? new JavaProtocolCodec();
        _playHandler = playHandler;
        _logger = logger ?? NullLogger<NetworkServer>.Instance;

        _inboundEvents = Channel.CreateBounded<InboundSessionEvent>(new BoundedChannelOptions(4096)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });
    }

    /// <inheritdoc />
    public string Name => "Networking";

    /// <inheritdoc />
    public int PhaseOrder => 0;

    /// <inheritdoc />
    public int ActiveSessionCount
    {
        get
        {
            PruneClosedSessions();
            return _sessions.Count;
        }
    }

    /// <inheritdoc />
    public int OnlinePlayerCount => Volatile.Read(ref _onlinePlayerCount);

    /// <inheritdoc />
    public IPEndPoint? BoundEndPoint { get; private set; }

    /// <inheritdoc />
    public ValueTask<IPEndPoint> StartListeningAsync(
        IPEndPoint bindEndPoint,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(bindEndPoint);
        cancellationToken.ThrowIfCancellationRequested();

        if (_listenerSocket is not null)
        {
            throw new InvalidOperationException("NetworkServer is already listening.");
        }

        var socket = new Socket(bindEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true,
        };

        socket.Bind(bindEndPoint);
        socket.Listen(backlog: 128);

        var actualEndPoint = (IPEndPoint)socket.LocalEndPoint!;
        _listenerSocket = socket;
        BoundEndPoint = actualEndPoint;
        _acceptLoopTask = RunAcceptLoopAsync(socket, _serverCts.Token);

        NetworkingLog.ServerListening(_logger, actualEndPoint.ToString(), _options.ProtocolVersion);
        return ValueTask.FromResult(actualEndPoint);
    }

    /// <inheritdoc />
    public long AttachPipeSession(PipeReader input, PipeWriter output)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        return RegisterSession(input, output, ownedSocket: null);
    }

    /// <inheritdoc />
    public bool SendToSession(long sessionId, IClientboundPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (!_sessions.TryGetValue(sessionId, out var session) || !session.IsPlayActive)
        {
            return false;
        }

        return session.TryEnqueueOutbound(new OutboundEnvelope(ConnectionState.Play, packet));
    }

    /// <inheritdoc />
    public void BroadcastToPlaySessions(IClientboundPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        var envelope = new OutboundEnvelope(ConnectionState.Play, packet);
        foreach (var (_, session) in _sessions)
        {
            if (session.IsPlayActive)
            {
                _ = session.TryEnqueueOutbound(envelope);
            }
        }
    }

    /// <inheritdoc />
    public void DisconnectSession(long sessionId, string reason)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            var state = session.IsPlayActive ? ConnectionState.Play : ConnectionState.Login;
            session.DisconnectWithReason(state, reason);
        }
    }

    /// <inheritdoc />
    public async ValueTask TickAsync(long tickNumber, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();

        PruneClosedSessions();

        // Phase 0: Drain all pending inbound events and sort deterministically by (SessionId, SequenceNumber).
        var count = 0;
        var buffer = ArrayPool<InboundSessionEvent>.Shared.Rent(256);

        try
        {
            while (_inboundEvents.Reader.TryRead(out var ev))
            {
                if (count == buffer.Length)
                {
                    var grown = ArrayPool<InboundSessionEvent>.Shared.Rent(buffer.Length * 2);
                    Array.Copy(buffer, grown, count);
                    ArrayPool<InboundSessionEvent>.Shared.Return(buffer, clearArray: true);
                    buffer = grown;
                }

                buffer[count++] = ev;
            }

            if (count > 1)
            {
                Array.Sort(buffer, 0, count);
            }

            var initialPackets = count > 0 ? new List<IClientboundPacket>(16) : null;
            for (var i = 0; i < count; i++)
            {
                await ProcessInboundEventAsync(
                    buffer[i],
                    tickNumber,
                    initialPackets!,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<InboundSessionEvent>.Shared.Return(buffer, clearArray: true);
        }

        ProcessKeepAlives(tickNumber);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _serverCts.CancelAsync().ConfigureAwait(false);
        _listenerSocket?.Dispose();

        if (_acceptLoopTask is not null)
        {
            try
            {
                await _acceptLoopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected.
            }
        }

        foreach (var (id, session) in _sessions)
        {
            if (_sessions.TryRemove(id, out _))
            {
                FinalizeDisconnectedPlayer(session);
                await session.DisposeAsync().ConfigureAwait(false);
            }
        }

        _inboundEvents.Writer.TryComplete();
        _serverCts.Dispose();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _serverCts.Cancel();
        _listenerSocket?.Dispose();

        foreach (var (id, session) in _sessions)
        {
            if (_sessions.TryRemove(id, out _))
            {
                FinalizeDisconnectedPlayer(session);
                session.InitiateClose("Server disposed");
            }
        }

        _inboundEvents.Writer.TryComplete();
        _serverCts.Dispose();
    }

    private long RegisterSession(PipeReader reader, PipeWriter writer, Socket? ownedSocket)
    {
        var sessionId = Interlocked.Increment(ref _nextSessionId);
        var session = new NetworkSession(
            sessionId,
            reader,
            writer,
            ownedSocket,
            _codec,
            _options,
            _inboundEvents.Writer,
            BuildStatusJson,
            _logger,
            _serverCts.Token);

        _sessions[sessionId] = session;
        return sessionId;
    }

    private async Task RunAcceptLoopAsync(Socket listener, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var clientSocket = await listener.AcceptAsync(ct).ConfigureAwait(false);
                clientSocket.NoDelay = true;

                var networkStream = new NetworkStream(clientSocket, ownsSocket: false);
                var reader = PipeReader.Create(networkStream, new StreamPipeReaderOptions(leaveOpen: true));
                var writer = PipeWriter.Create(networkStream, new StreamPipeWriterOptions(leaveOpen: false));

                _ = RegisterSession(reader, writer, clientSocket);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Clean shutdown.
        }
        catch (ObjectDisposedException)
        {
            // Socket closed on shutdown.
        }
        catch (SocketException) when (ct.IsCancellationRequested)
        {
            // Socket aborted on shutdown.
        }
    }

    private async ValueTask ProcessInboundEventAsync(
        InboundSessionEvent ev,
        long tickNumber,
        List<IClientboundPacket> initialPacketsScratch,
        CancellationToken ct)
    {
        switch (ev.Kind)
        {
            case InboundEventKind.Login:
                if (!_sessions.TryGetValue(ev.SessionId, out var loginSession) || loginSession.IsClosed)
                {
                    return;
                }

                if (Volatile.Read(ref _onlinePlayerCount) >= _options.MaxPlayers)
                {
                    loginSession.DisconnectWithReason(ConnectionState.Login, "Server is full.");
                    return;
                }

                initialPacketsScratch.Clear();
                var preLoginInfo = new PlayerSessionInfo(
                    ev.SessionId,
                    _codec.Edition,
                    ev.Username ?? string.Empty,
                    ev.PlayerUuid,
                    AssignedEntityId: 0);

                var entityId = _playHandler is not null
                    ? await _playHandler.OnPlayerLoginAsync(preLoginInfo, tickNumber, initialPacketsScratch, ct).ConfigureAwait(false)
                    : ev.SessionId;

                loginSession.MarkPlayActive(entityId, tickNumber);
                _ = Interlocked.Increment(ref _onlinePlayerCount);

                _ = loginSession.TryEnqueueOutbound(new OutboundEnvelope(
                    ConnectionState.Login,
                    new LoginSuccessPacket(loginSession.PlayerUuid, loginSession.Username, entityId)));

                for (var i = 0; i < initialPacketsScratch.Count; i++)
                {
                    _ = loginSession.TryEnqueueOutbound(new OutboundEnvelope(
                        ConnectionState.Play,
                        initialPacketsScratch[i]));
                }

                break;

            case InboundEventKind.PlayPacket:
                if (!_sessions.TryGetValue(ev.SessionId, out var playSession) ||
                    !playSession.IsPlayActive ||
                    ev.Packet is null)
                {
                    return;
                }

                if (ev.Packet is KeepAliveResponsePacket)
                {
                    playSession.LastKeepAliveAckTick = tickNumber;
                    return;
                }

                if (_playHandler is not null)
                {
                    await _playHandler.OnPlayPacketAsync(
                        playSession.ToPlayerInfo(),
                        ev.Packet,
                        tickNumber,
                        this,
                        ct).ConfigureAwait(false);
                }

                break;

            case InboundEventKind.Disconnect:
                if (_sessions.TryRemove(ev.SessionId, out var removed))
                {
                    FinalizeDisconnectedPlayer(removed);
                }

                break;
        }
    }

    private void ProcessKeepAlives(long tickNumber)
    {
        var sendChallenge = _options.KeepAliveIntervalTicks > 0 &&
            tickNumber > 0 &&
            tickNumber % _options.KeepAliveIntervalTicks == 0;

        foreach (var (_, session) in _sessions)
        {
            if (!session.IsPlayActive)
            {
                continue;
            }

            if (_options.KeepAliveTimeoutTicks > 0 &&
                tickNumber - session.LastKeepAliveAckTick > _options.KeepAliveTimeoutTicks)
            {
                session.DisconnectWithReason(ConnectionState.Play, "Timed out waiting for KeepAlive.");
                continue;
            }

            if (sendChallenge)
            {
                _ = session.TryEnqueueOutbound(new OutboundEnvelope(
                    ConnectionState.Play,
                    new KeepAliveRequestPacket(tickNumber)));
            }
        }
    }

    private void PruneClosedSessions()
    {
        foreach (var (id, session) in _sessions)
        {
            if (session.IsClosed && _sessions.TryRemove(id, out var removed))
            {
                FinalizeDisconnectedPlayer(removed);
            }
        }
    }

    private void FinalizeDisconnectedPlayer(NetworkSession session)
    {
        if (session.TryClearPlayActive())
        {
            _ = Interlocked.Decrement(ref _onlinePlayerCount);
            _playHandler?.OnPlayerDisconnected(session.ToPlayerInfo());
        }
    }

    private string BuildStatusJson()
    {
        var status = new ServerStatusResponse(
            _options.VersionName,
            _options.ProtocolVersion,
            _options.MaxPlayers,
            Volatile.Read(ref _onlinePlayerCount),
            _options.Motd);

        return JsonSerializer.Serialize(status, ServerStatusJsonContext.Default.ServerStatusResponse);
    }
}
