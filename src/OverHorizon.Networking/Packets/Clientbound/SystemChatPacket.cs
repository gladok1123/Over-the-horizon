namespace OverHorizon.Networking.Packets.Clientbound;

/// <summary>
/// Delivers a chat or system broadcast message to a client in <see cref="ConnectionState.Play"/>.
/// </summary>
/// <param name="Message">Formatted message text.</param>
/// <param name="IsOverlay">Whether to display above the hotbar instead of the chat log.</param>
public sealed record SystemChatPacket(string Message, bool IsOverlay = false) : IClientboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Play;
}
