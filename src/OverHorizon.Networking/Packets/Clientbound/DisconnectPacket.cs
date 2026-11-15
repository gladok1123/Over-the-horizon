namespace OverHorizon.Networking.Packets.Clientbound;

/// <summary>
/// Instructs the client to disconnect with a human-readable reason during <c>Login</c> or <c>Play</c>.
/// </summary>
/// <param name="TargetState">Protocol state in which the disconnect is sent (<c>Login</c> or <c>Play</c>).</param>
/// <param name="Reason">Disconnect message.</param>
public sealed record DisconnectPacket(ConnectionState TargetState, string Reason) : IClientboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => TargetState;
}
