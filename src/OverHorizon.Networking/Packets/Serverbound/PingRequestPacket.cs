namespace OverHorizon.Networking.Packets.Serverbound;

/// <summary>
/// Latency measurement ping sent by the client during <see cref="ConnectionState.Status"/>.
/// </summary>
/// <param name="ClientTimestamp">Opaque 64-bit timestamp echoed back in <c>PongResponsePacket</c>.</param>
public sealed record PingRequestPacket(long ClientTimestamp) : IServerboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Status;
}
