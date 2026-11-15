namespace OverHorizon.Networking.Packets.Clientbound;

/// <summary>
/// Periodic server liveness challenge sent during <see cref="ConnectionState.Play"/>.
/// </summary>
/// <param name="KeepAliveId">64-bit challenge identifier.</param>
public sealed record KeepAliveRequestPacket(long KeepAliveId) : IClientboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Play;
}
