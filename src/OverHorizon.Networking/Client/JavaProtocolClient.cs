using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using OverHorizon.Networking.Codec;
using OverHorizon.Networking.Framing;
using OverHorizon.Networking.Packets;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Networking.Packets.Serverbound;
using OverHorizon.Networking.Primitives;
using OverHorizon.Networking.Status;

namespace OverHorizon.Networking.Client;

/// <summary>
/// Asynchronous Java-protocol client supporting server-list status pings and full compressed gameplay sessions
/// over either TCP sockets or in-memory <see cref="System.IO.Pipelines"/> pairs.
/// </summary>
public sealed class JavaProtocolClient : IAsyncDisposable
{
    private readonly PipeReader _reader;
    private readonly PipeWriter _writer;
    private readonly TcpClient? _ownedTcpClient;
    private readonly IProtocolCodec _codec;
    private readonly ArrayBufferWriter<byte> _sendScratch = new(512);

    private ConnectionState _state = ConnectionState.Handshaking;
    private int _compressionThreshold = -1;
    private int _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="JavaProtocolClient"/> class over pipeline endpoints.
    /// </summary>
    /// <param name="reader">Server-to-client <see cref="PipeReader"/>.</param>
    /// <param name="writer">Client-to-server <see cref="PipeWriter"/>.</param>
    /// <param name="codec">Optional protocol codec override.</param>
    /// <param name="ownedTcpClient">Optional underlying TCP socket to dispose with the client.</param>
    public JavaProtocolClient(
        PipeReader reader,
        PipeWriter writer,
        IProtocolCodec? codec = null,
        TcpClient? ownedTcpClient = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(writer);

        _reader = reader;
        _writer = writer;
        _codec = codec ?? new JavaProtocolCodec();
        _ownedTcpClient = ownedTcpClient;
    }

    /// <summary>Gets the current protocol connection state.</summary>
    public ConnectionState State => _state;

    /// <summary>Gets the active ZLib compression threshold (<c>-1</c> if uncompressed).</summary>
    public int CompressionThreshold => _compressionThreshold;

    /// <summary>
    /// Connects to a TCP server endpoint and wraps the socket in a <see cref="JavaProtocolClient"/>.
    /// </summary>
    /// <param name="endPoint">Remote server IP and port.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Connected <see cref="JavaProtocolClient"/>.</returns>
    public static async ValueTask<JavaProtocolClient> ConnectTcpAsync(
        IPEndPoint endPoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endPoint);

        var tcp = new TcpClient { NoDelay = true };
        try
        {
            await tcp.ConnectAsync(endPoint.Address, endPoint.Port, cancellationToken).ConfigureAwait(false);
            var stream = tcp.GetStream();
            var reader = PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: true));
            var writer = PipeWriter.Create(stream, new StreamPipeWriterOptions(leaveOpen: false));
            return new JavaProtocolClient(reader, writer, ownedTcpClient: tcp);
        }
        catch
        {
            tcp.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Executes a complete <c>Handshaking -&gt; Status -&gt; Ping/Pong</c> query and returns the parsed status + echoed timestamp.
    /// </summary>
    /// <param name="protocolVersion">Client protocol version.</param>
    /// <param name="pingTimestamp">64-bit timestamp payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of deserialized <see cref="ServerStatusResponse"/> and echoed pong timestamp.</returns>
    public async ValueTask<(ServerStatusResponse Status, long EchoedTimestamp)> QueryStatusAndPingAsync(
        int protocolVersion,
        long pingTimestamp,
        CancellationToken cancellationToken = default)
    {
        await SendPacketInternalAsync(
            new HandshakePacket(protocolVersion, "localhost", 25565, HandshakeIntent.Status),
            cancellationToken).ConfigureAwait(false);
        _state = ConnectionState.Status;

        await SendPacketInternalAsync(StatusRequestPacket.Instance, cancellationToken).ConfigureAwait(false);
        var statusPkt = await ReceivePacketAsync(cancellationToken).ConfigureAwait(false) as StatusResponsePacket
            ?? throw new ProtocolException("Expected StatusResponsePacket.");

        var parsed = JsonSerializer.Deserialize(
            statusPkt.JsonPayload,
            ServerStatusJsonContext.Default.ServerStatusResponse)
            ?? throw new ProtocolException("Invalid status JSON payload.");

        await SendPacketInternalAsync(new PingRequestPacket(pingTimestamp), cancellationToken).ConfigureAwait(false);
        var pongPkt = await ReceivePacketAsync(cancellationToken).ConfigureAwait(false) as PongResponsePacket
            ?? throw new ProtocolException("Expected PongResponsePacket.");

        _state = ConnectionState.Closed;
        return (parsed, pongPkt.ClientTimestamp);
    }

    /// <summary>
    /// Performs <c>Handshaking -&gt; Login</c> (including transparent <c>SetCompression</c> negotiation)
    /// and transitions into <see cref="ConnectionState.Play"/>.
    /// </summary>
    /// <param name="username">Player username.</param>
    /// <param name="protocolVersion">Protocol version.</param>
    /// <param name="clientUuid">Optional client UUID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Received <see cref="LoginSuccessPacket"/>.</returns>
    public async ValueTask<LoginSuccessPacket> LoginAsync(
        string username,
        int protocolVersion = JavaProtocolCodec.CurrentProtocolVersion,
        Guid clientUuid = default,
        CancellationToken cancellationToken = default)
    {
        await SendPacketInternalAsync(
            new HandshakePacket(protocolVersion, "localhost", 25565, HandshakeIntent.Login),
            cancellationToken).ConfigureAwait(false);
        _state = ConnectionState.Login;

        await SendPacketInternalAsync(
            new LoginStartPacket(username, clientUuid),
            cancellationToken).ConfigureAwait(false);

        while (true)
        {
            var response = await ReceivePacketAsync(cancellationToken).ConfigureAwait(false);
            switch (response)
            {
                case SetCompressionPacket compression:
                    _compressionThreshold = compression.ThresholdBytes;
                    break;

                case LoginSuccessPacket success:
                    _state = ConnectionState.Play;
                    return success;

                case DisconnectPacket disconnect:
                    _state = ConnectionState.Closed;
                    throw new ProtocolException($"Login rejected by server: {disconnect.Reason}");

                default:
                    throw new ProtocolException($"Unexpected packet '{response.GetType().Name}' during Login.");
            }
        }
    }

    /// <summary>Sends a gameplay packet while in <see cref="ConnectionState.Play"/>.</summary>
    /// <param name="packet">Gameplay serverbound packet.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Completion task.</returns>
    public ValueTask SendPlayPacketAsync(
        IServerboundPacket packet,
        CancellationToken cancellationToken = default)
    {
        if (_state != ConnectionState.Play)
        {
            throw new InvalidOperationException($"Client is in state {_state}, expected Play.");
        }

        return SendPacketInternalAsync(packet, cancellationToken);
    }

    /// <summary>Reads and decodes the next <see cref="IClientboundPacket"/> from the server.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Decoded <see cref="IClientboundPacket"/>.</returns>
    public async ValueTask<IClientboundPacket> ReceivePacketAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        while (true)
        {
            var result = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            var buffer = result.Buffer;

            if (PacketFramer.TryReadFrame(
                    ref buffer,
                    _compressionThreshold,
                    out var rented,
                    out var payloadLength))
            {
                _reader.AdvanceTo(buffer.Start);
                try
                {
                    return _codec.DecodeClientbound(_state, rented.AsSpan(0, payloadLength));
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }

            _reader.AdvanceTo(buffer.Start, buffer.End);
            if (result.IsCompleted)
            {
                throw new ProtocolException("Connection closed by server while awaiting packet.");
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _state = ConnectionState.Closed;
        try
        {
            _ownedTcpClient?.Client?.Shutdown(System.Net.Sockets.SocketShutdown.Both);
        }
        catch
        {
            // Socket may already be closed or loopback pipe-only.
        }

        _ownedTcpClient?.Dispose();
        try
        {
            await _writer.CompleteAsync().ConfigureAwait(false);
        }
        catch
        {
            // Ignore pipe completion race during disconnect.
        }

        try
        {
            await _reader.CompleteAsync().ConfigureAwait(false);
        }
        catch
        {
            // Ignore pipe completion race while background receiver unwinds.
        }
    }

    private async ValueTask SendPacketInternalAsync(IServerboundPacket packet, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        _sendScratch.Clear();
        _codec.EncodeServerbound(_state, packet, _sendScratch);
        PacketFramer.WriteFrame(_writer, _sendScratch.WrittenSpan, _compressionThreshold);
        _ = await _writer.FlushAsync(ct).ConfigureAwait(false);
    }
}
