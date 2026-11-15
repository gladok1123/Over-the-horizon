namespace OverHorizon.Modding.Events;

/// <summary>
/// Cancellable and mutable event fired before broadcasting a player chat message.
/// </summary>
/// <param name="SessionId">Sender session ID.</param>
/// <param name="PlayerName">Sender username.</param>
/// <param name="Message">Chat message body (modifiable by subscribers).</param>
/// <param name="IsCancelled">Whether broadcast is vetoed.</param>
public record struct PlayerChatEvent(
    long SessionId,
    string PlayerName,
    string Message,
    bool IsCancelled = false) : ICancellableEvent
{
    /// <inheritdoc />
    public readonly string EventName => "overhorizon:player_chat";
}
