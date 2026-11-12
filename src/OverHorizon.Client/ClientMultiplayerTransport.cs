using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Threading.Channels;
using OverHorizon.Networking.Client;
using OverHorizon.Networking.Packets;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Networking.Packets.Serverbound;
using OverHorizon.Networking.Primitives;

namespace OverHorizon.Client;

/// <summary>
/// Lock-free dual-channel async transport pump wrapping <see cref="JavaProtocolClient"/> for multiplayer client sessions.
/// </summary>
internal sealed class ClientMultiplayerTransport : IAsyncDisposable
{
    private readonly Channel<IClientboundPacket> _inbound = Channel.CreateUnbounded<IClientboundPacket>();
    private readonly Channel<IServerboundPacket> _outbound = Channel.CreateUnbounded<IServerboundPacket>();

    private JavaProtocolClient? _client;
    private CancellationTokenSource? _cts;
    private Task? _readTask;
    private Task? _writeTask;

    public bool IsConnected => _client is not null;

    public long AssignedEntityId { get; private set; }

    public async ValueTask<LoginSuccessPacket> ConnectAndLoginAsync(
        string host,
        ushort port,
        string username,
        CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
        var endpoint = new IPEndPoint(addresses[0], port);
        var client = await JavaProtocolClient.ConnectTcpAsync(endpoint, cancellationToken).ConfigureAwait(false);
        var login = await client.LoginAsync(username, cancellationToken: cancellationToken).ConfigureAwait(false);

        AssignedEntityId = login.AssignedEntityId;
        _client = client;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _readTask = PumpServerPacketsAsync(client, _cts.Token);
        _writeTask = PumpOutboundPacketsAsync(client, _cts.Token);
        return login;
    }

    public bool TryReadInbound([MaybeNullWhen(false)] out IClientboundPacket packet) =>
        _inbound.Reader.TryRead(out packet);

    public void EnqueueOutbound(IServerboundPacket packet)
    {
        if (_client is not null)
        {
            _ = _outbound.Writer.TryWrite(packet);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _outbound.Writer.TryComplete();
        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
        }

        await AwaitQuietlyAsync(_readTask).ConfigureAwait(false);
        await AwaitQuietlyAsync(_writeTask).ConfigureAwait(false);

        _cts?.Dispose();
        _cts = null;

        if (_client is not null)
        {
            await _client.DisposeAsync().ConfigureAwait(false);
            _client = null;
        }
    }

    private static async ValueTask AwaitQuietlyAsync(Task? task)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown cancellation.
        }
    }

    private async Task PumpServerPacketsAsync(JavaProtocolClient client, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var packet = await client.ReceivePacketAsync(ct).ConfigureAwait(false);
                if (packet is KeepAliveRequestPacket keepAlive)
                {
                    _ = _outbound.Writer.TryWrite(new KeepAliveResponsePacket(keepAlive.KeepAliveId));
                    continue;
                }

                _ = _inbound.Writer.TryWrite(packet);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or ProtocolException)
        {
            _inbound.Writer.TryComplete();
        }
    }

    private async Task PumpOutboundPacketsAsync(JavaProtocolClient client, CancellationToken ct)
    {
        try
        {
            await foreach (var packet in _outbound.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                await client.SendPlayPacketAsync(packet, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or ProtocolException)
        {
            // Connection closed.
        }
    }
}
