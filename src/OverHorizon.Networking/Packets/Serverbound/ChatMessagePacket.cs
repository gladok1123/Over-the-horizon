namespace OverHorizon.Networking.Packets.Serverbound;

/// <summary>
/// Plain-text chat message sent by a player during <see cref="ConnectionState.Play"/>.
/// </summary>
/// <param name="Message">Chat message text (<c>1..256</c> characters).</param>
public sealed record ChatMessagePacket(string Message) : IServerboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Play;
}
