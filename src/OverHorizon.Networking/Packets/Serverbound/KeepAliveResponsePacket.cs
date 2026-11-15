namespace OverHorizon.Networking.Packets.Serverbound;

/// <summary>
/// Client acknowledgement of a server <c>KeepAliveRequestPacket</c> during <see cref="ConnectionState.Play"/>.
/// </summary>
/// <param name="KeepAliveId">Matching 64-bit challenge identifier.</param>
public sealed record KeepAliveResponsePacket(long KeepAliveId) : IServerboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Play;
}
