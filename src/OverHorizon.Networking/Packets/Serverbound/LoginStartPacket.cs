namespace OverHorizon.Networking.Packets.Serverbound;

/// <summary>
/// Initiates login with a player username and optional client-proposed UUID.
/// </summary>
/// <param name="Username">Player username (<c>1..16</c> characters).</param>
/// <param name="ClientUuid">Client UUID (or <see cref="Guid.Empty"/> for offline deterministic UUID).</param>
public sealed record LoginStartPacket(string Username, Guid ClientUuid) : IServerboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Login;
}
