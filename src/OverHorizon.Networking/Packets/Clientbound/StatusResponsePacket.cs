namespace OverHorizon.Networking.Packets.Clientbound;

/// <summary>
/// Carries the UTF-8 JSON server status payload during <see cref="ConnectionState.Status"/>.
/// </summary>
/// <param name="JsonPayload">Serialized status JSON document.</param>
public sealed record StatusResponsePacket(string JsonPayload) : IClientboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Status;
}
