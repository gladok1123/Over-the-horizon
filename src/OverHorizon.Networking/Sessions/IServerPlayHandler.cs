using OverHorizon.Networking.Packets;

namespace OverHorizon.Networking.Sessions;

/// <summary>
/// Main-thread gameplay bridge invoked deterministically during Phase 0 (<c>NetworkServer.TickAsync</c>).
/// </summary>
public interface IServerPlayHandler
{
    /// <summary>
    /// Invoked on the main simulation thread when a player completes login.
    /// Implementations spawn the player entity, register chunk tickets, and enqueue initial world sync packets.
    /// </summary>
    /// <param name="session">Authenticated session info (<c>AssignedEntityId == 0</c>).</param>
    /// <param name="tickNumber">Current server tick.</param>
    /// <param name="initialPlayPackets">List to populate with initial <see cref="ConnectionState.Play"/> packets sent right after <c>LoginSuccess</c>.</param>
    /// <param name="cancellationToken">Tick cancellation token.</param>
    /// <returns>Assigned authoritative 64-bit ECS entity identifier.</returns>
    ValueTask<long> OnPlayerLoginAsync(
        PlayerSessionInfo session,
        long tickNumber,
        IList<IClientboundPacket> initialPlayPackets,
        CancellationToken cancellationToken);

    /// <summary>
    /// Invoked on the main simulation thread for each gameplay packet received from an active player.
    /// </summary>
    /// <param name="session">Player session metadata.</param>
    /// <param name="packet">Decoded serverbound gameplay packet.</param>
    /// <param name="tickNumber">Current server tick.</param>
    /// <param name="broadcaster">Packet sender/broadcaster.</param>
    /// <param name="cancellationToken">Tick cancellation token.</param>
    /// <returns>Asynchronous completion task.</returns>
    ValueTask OnPlayPacketAsync(
        PlayerSessionInfo session,
        IServerboundPacket packet,
        long tickNumber,
        INetworkBroadcaster broadcaster,
        CancellationToken cancellationToken);

    /// <summary>
    /// Invoked on the main simulation thread when a previously logged-in player disconnects.
    /// </summary>
    /// <param name="session">Disconnected player session metadata.</param>
    void OnPlayerDisconnected(PlayerSessionInfo session);
}
