namespace OverHorizon.Networking.Packets.Serverbound;

/// <summary>
/// Initial handshake packet negotiating protocol version and target state (<c>Status</c> or <c>Login</c>).
/// </summary>
/// <param name="ProtocolVersion">Client protocol version number.</param>
/// <param name="ServerAddress">Requested hostname or IP address.</param>
/// <param name="ServerPort">Requested TCP port.</param>
/// <param name="Intent">Requested next <see cref="HandshakeIntent"/>.</param>
public sealed record HandshakePacket(
    int ProtocolVersion,
    string ServerAddress,
    ushort ServerPort,
    HandshakeIntent Intent) : IServerboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Handshaking;
}
