namespace OverHorizon.Networking.Packets;

/// <summary>
/// Represents an outgoing packet sent from the server to a client.
/// </summary>
public interface IClientboundPacket
{
    /// <summary>Gets the <see cref="ConnectionState"/> in which this packet is valid.</summary>
    ConnectionState ValidState { get; }
}
