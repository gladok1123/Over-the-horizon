using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using OverHorizon.Networking.Codec;
using OverHorizon.Networking.Framing;
using OverHorizon.Networking.Packets;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Networking.Packets.Serverbound;
using OverHorizon.Networking.Primitives;
using OverHorizon.Shared.Hashing;

namespace OverHorizon.Networking.Sessions;

internal sealed class NetworkSession : IAsyncDisposable
{
    private readonly PipeReader _reader;
    private readonly PipeWriter _writer;
    private readonly Socket? _ownedSocket;
    private readonly IProtocolCodec _codec;
    private readonly NetworkServerOptions _options;
    private readonly ChannelWriter<InboundSessionEvent> _inboundEvents;
    private readonly Func<string> _statusJsonFactory;
    private readonly ILogger _logger;
    private readonly Channel<OutboundEnvelope> _outboundQueue;
    private readonly CancellationTokenSource _cts;
    private readonly Task _readTask;
    private readonly Task _writeTask;

    private long _sequenceCounter;
    private long _assignedEntityId;
    private long _lastKeepAliveAckTick;
    private int _playActive;
    private int _closed;

    public NetworkSession(
        long sessionId,
        PipeReader reader,
        PipeWriter writer,
        Socket? ownedSocket,
        IProtocolCodec codec,
        NetworkServerOptions options,
        ChannelWriter<InboundSessionEvent> inboundEvents,
        Func<string> statusJsonFactory,
        ILogger logger,
        CancellationToken serverStoppingToken)
    {
        SessionId = sessionId;
        _reader = reader;
        _writer = writer;
        _ownedSocket = ownedSocket;
        _codec = codec;
        _options = options;
        _inboundEvents = inboundEvents;
        _statusJsonFactory = statusJsonFactory;
        _logger = logger;

        _outboundQueue = Channel.CreateBounded<OutboundEnvelope>(new BoundedChannelOptions(512)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest,
        });

        _cts = CancellationTokenSource.CreateLinkedTokenSource(serverStoppingToken);
        _readTask = RunReadLoopAsync(_cts.Token);
        _writeTask = RunWriteLoopAsync(_cts.Token);
    }

    public long SessionId { get; }

    public string Username { get; private set; } = string.Empty;

    public Guid PlayerUuid { get; private set; }

    public long AssignedEntityId
    {
        get => Interlocked.Read(ref _assignedEntityId);
        set => Interlocked.Exchange(ref _assignedEntityId, value);
    }

    public long LastKeepAliveAckTick
    {
        get => Interlocked.Read(ref _lastKeepAliveAckTick);
        set => Interlocked.Exchange(ref _lastKeepAliveAckTick, value);
    }

    public bool IsPlayActive => Volatile.Read(ref _playActive) != 0;

    public bool IsClosed => Volatile.Read(ref _closed) != 0;

    public static Guid ComputeOfflinePlayerUuid(string username)
    {
        var h1 = Fnv1a64.Create();
        h1.AppendString("OfflinePlayer:");
        h1.AppendString(username);

        var h2 = Fnv1a64.Create();
        h2.AppendInt64(unchecked((long)h1.Value));
        h2.AppendString(username);

        Span<byte> bytes = stackalloc byte[16];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(bytes[..8], h1.Value);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(bytes[8..], h2.Value);

        // Stamp RFC 4122 version 3 / variant bits for standard UUID formatting.
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x30);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }

    public void MarkPlayActive(long assignedEntityId, long currentTick)
    {
        AssignedEntityId = assignedEntityId;
        LastKeepAliveAckTick = currentTick;
        Volatile.Write(ref _playActive, 1);
    }

    public bool TryClearPlayActive() =>
        Interlocked.Exchange(ref _playActive, 0) != 0;

    public PlayerSessionInfo ToPlayerInfo() =>
        new(SessionId, _codec.Edition, Username, PlayerUuid, AssignedEntityId);

    public bool TryEnqueueOutbound(OutboundEnvelope envelope) =>
        !IsClosed && _outboundQueue.Writer.TryWrite(envelope);

    public void DisconnectWithReason(ConnectionState state, string reason)
    {
        if (state is ConnectionState.Login or ConnectionState.Play)
        {
            _ = _outboundQueue.Writer.TryWrite(
                new OutboundEnvelope(state, new DisconnectPacket(state, reason), CloseAfterSend: true));
        }
        else
        {
            InitiateClose(reason);
        }
    }

    public void InitiateClose(string reason)
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
        {
            return;
        }

        _outboundQueue.Writer.TryComplete();
        _cts.Cancel();
        NetworkingLog.SessionClosed(_logger, SessionId, reason);
    }

    public async ValueTask DisposeAsync()
    {
        InitiateClose("Server disposing session");

        try
        {
            await Task.WhenAll(_readTask, _writeTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }

        _ownedSocket?.Dispose();
        _cts.Dispose();
    }

    private async Task RunReadLoopAsync(CancellationToken ct)
    {
        var inboundState = ConnectionState.Handshaking;
        var inboundCompression = -1;
        var clientProtocolVersion = _options.ProtocolVersion;
        var loginQueued = false;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var readResult = await _reader.ReadAsync(ct).ConfigureAwait(false);
                var buffer = readResult.Buffer;

                while (PacketFramer.TryReadFrame(
                           ref buffer,
                           inboundCompression,
                           out var rentedPayload,
                           out var payloadLength))
                {
                    try
                    {
                        var packet = _codec.DecodeServerbound(
                            inboundState,
                            rentedPayload.AsSpan(0, payloadLength));

                        var shouldStop = await HandleDecodedInboundAsync(
                            packet,
                            ref inboundState,
                            ref inboundCompression,
                            ref clientProtocolVersion,
                            ref loginQueued,
                            ct).ConfigureAwait(false);

                        if (shouldStop)
                        {
                            _reader.AdvanceTo(buffer.Start, buffer.End);
                            return;
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(rentedPayload);
                    }
                }

                _reader.AdvanceTo(buffer.Start, buffer.End);

                if (readResult.IsCompleted)
                {
                    break;
                }
            }
        }
        catch (ProtocolException ex)
        {
            NetworkingLog.ProtocolViolation(_logger, SessionId, ex.Message);
            InitiateClose(ex.Message);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Expected cancellation.
        }
        catch (IOException ex)
        {
            InitiateClose(ex.Message);
        }
        catch (SocketException ex)
        {
            InitiateClose(ex.Message);
        }
        finally
        {
            await _reader.CompleteAsync().ConfigureAwait(false);

            if (loginQueued)
            {
                var seq = Interlocked.Increment(ref _sequenceCounter);
                _ = _inboundEvents.TryWrite(new InboundSessionEvent(
                    SessionId,
                    seq,
                    InboundEventKind.Disconnect,
                    Username,
                    PlayerUuid,
                    Packet: null));
            }

            InitiateClose("Read stream completed");
        }
    }

    private async ValueTask<bool> HandleDecodedInboundAsync(
        IServerboundPacket packet,
        ref ConnectionState inboundState,
        ref int inboundCompression,
        ref int clientProtocolVersion,
        ref bool loginQueued,
        CancellationToken ct)
    {
        switch (packet)
        {
            case HandshakePacket hs:
                clientProtocolVersion = hs.ProtocolVersion;
                inboundState = hs.Intent == HandshakeIntent.Status
                    ? ConnectionState.Status
                    : ConnectionState.Login;
                return false;

            case StatusRequestPacket:
                _ = TryEnqueueOutbound(new OutboundEnvelope(
                    ConnectionState.Status,
                    new StatusResponsePacket(_statusJsonFactory())));
                return false;

            case PingRequestPacket ping:
                _ = TryEnqueueOutbound(new OutboundEnvelope(
                    ConnectionState.Status,
                    new PongResponsePacket(ping.ClientTimestamp),
                    CloseAfterSend: true));
                return true;

            case LoginStartPacket login:
                if (clientProtocolVersion != _options.ProtocolVersion)
                {
                    DisconnectWithReason(
                        ConnectionState.Login,
                        $"Incompatible protocol version {clientProtocolVersion} (expected {_options.ProtocolVersion}).");
                    return true;
                }

                Username = login.Username;
                PlayerUuid = login.ClientUuid != Guid.Empty
                    ? login.ClientUuid
                    : ComputeOfflinePlayerUuid(login.Username);

                if (_options.CompressionThresholdBytes >= 0)
                {
                    var threshold = _options.CompressionThresholdBytes;
                    _ = TryEnqueueOutbound(new OutboundEnvelope(
                        ConnectionState.Login,
                        new SetCompressionPacket(threshold),
                        NewCompressionThresholdAfterSend: threshold));
                    inboundCompression = threshold;
                }

                inboundState = ConnectionState.Play;
                loginQueued = true;

                var loginSeq = Interlocked.Increment(ref _sequenceCounter);
                await _inboundEvents.WriteAsync(
                    new InboundSessionEvent(
                        SessionId,
                        loginSeq,
                        InboundEventKind.Login,
                        Username,
                        PlayerUuid,
                        Packet: null),
                    ct).ConfigureAwait(false);
                return false;

            default:
                var seq = Interlocked.Increment(ref _sequenceCounter);
                await _inboundEvents.WriteAsync(
                    new InboundSessionEvent(
                        SessionId,
                        seq,
                        InboundEventKind.PlayPacket,
                        Username,
                        PlayerUuid,
                        packet),
                    ct).ConfigureAwait(false);
                return false;
        }
    }

    private async Task RunWriteLoopAsync(CancellationToken ct)
    {
        var outboundCompression = -1;
        var scratch = new ArrayBufferWriter<byte>(512);

        try
        {
            await foreach (var envelope in _outboundQueue.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                scratch.Clear();
                _codec.EncodeClientbound(envelope.EncodeState, envelope.Packet, scratch);
                PacketFramer.WriteFrame(_writer, scratch.WrittenSpan, outboundCompression);

                var flush = await _writer.FlushAsync(ct).ConfigureAwait(false);
                if (envelope.NewCompressionThresholdAfterSend is { } nextThreshold)
                {
                    outboundCompression = nextThreshold;
                }

                if (envelope.CloseAfterSend || flush.IsCompleted || flush.IsCanceled)
                {
                    InitiateClose("Outbound close requested");
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Expected cancellation.
        }
        catch (IOException)
        {
            // Peer closed socket.
        }
        catch (SocketException)
        {
            // Peer reset socket.
        }
        finally
        {
            await _writer.CompleteAsync().ConfigureAwait(false);
            if (_ownedSocket is not null)
            {
                try
                {
                    _ownedSocket.Shutdown(SocketShutdown.Both);
                }
                catch (SocketException)
                {
                    // Already closed.
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed.
                }
            }
        }
    }
}
