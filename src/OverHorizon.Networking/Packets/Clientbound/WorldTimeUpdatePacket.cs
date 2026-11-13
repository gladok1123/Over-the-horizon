namespace OverHorizon.Networking.Packets.Clientbound;

/// <summary>
/// Synchronizes the authoritative server day/night <c>WorldTimeTicks</c> (<c>0..24000+</c>) to connected clients.
/// </summary>
public sealed record WorldTimeUpdatePacket(long WorldTimeTicks) : IClientboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Play;
}
