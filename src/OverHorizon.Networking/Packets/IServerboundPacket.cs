namespace OverHorizon.Networking.Packets;

/// <summary>
/// Represents a decoded packet sent from a client to the server.
/// </summary>
public interface IServerboundPacket
{
    /// <summary>Gets the <see cref="ConnectionState"/> in which this packet is valid.</summary>
    ConnectionState ValidState { get; }
}
