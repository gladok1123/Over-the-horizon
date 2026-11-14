namespace OverHorizon.Networking.Packets.Clientbound;

/// <summary>
/// Echoes a client's ping timestamp during <see cref="ConnectionState.Status"/>.
/// </summary>
/// <param name="ClientTimestamp">Echoed 64-bit timestamp.</param>
public sealed record PongResponsePacket(long ClientTimestamp) : IClientboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Status;
}
