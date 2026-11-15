using OverHorizon.Networking.Packets;

namespace OverHorizon.Networking.Sessions;

/// <summary>
/// Allows main-thread gameplay handlers to enqueue packets to individual sessions or all active players.
/// </summary>
public interface INetworkBroadcaster
{
    /// <summary>
    /// Enqueues a <see cref="IClientboundPacket"/> for delivery to <paramref name="sessionId"/>.
    /// </summary>
    /// <param name="sessionId">Target session ID.</param>
    /// <param name="packet">Outgoing packet (valid for <see cref="ConnectionState.Play"/>).</param>
    /// <returns><see langword="true"/> if the session is active and accepted the packet.</returns>
    bool SendToSession(long sessionId, IClientboundPacket packet);

    /// <summary>
    /// Enqueues a <see cref="IClientboundPacket"/> to all sessions currently in <see cref="ConnectionState.Play"/>.
    /// </summary>
    /// <param name="packet">Outgoing broadcast packet.</param>
    void BroadcastToPlaySessions(IClientboundPacket packet);

    /// <summary>
    /// Sends a <c>DisconnectPacket</c> and closes <paramref name="sessionId"/>.
    /// </summary>
    /// <param name="sessionId">Target session ID.</param>
    /// <param name="reason">Human-readable disconnect message.</param>
    void DisconnectSession(long sessionId, string reason);
}
