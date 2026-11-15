namespace OverHorizon.Networking.Packets.Serverbound;

/// <summary>
/// Requests the server's JSON status summary during <see cref="ConnectionState.Status"/>.
/// </summary>
public sealed record StatusRequestPacket : IServerboundPacket
{
    /// <summary>Singleton stateless instance.</summary>
    public static readonly StatusRequestPacket Instance = new();

    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Status;
}
