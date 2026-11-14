namespace OverHorizon.Networking.Packets.Clientbound;

/// <summary>
/// Notifies peer clients that a remote player disconnected and their 3D avatar should be removed.
/// </summary>
/// <param name="EntityId">Authoritative server ECS entity ID of the departed player.</param>
public sealed record RemotePlayerDespawnPacket(long EntityId) : IClientboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Play;
}
