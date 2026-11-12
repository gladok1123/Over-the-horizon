namespace OverHorizon.Networking.Packets.Clientbound;

/// <summary>
/// Completes <see cref="ConnectionState.Login"/> and transitions the session into <see cref="ConnectionState.Play"/>.
/// </summary>
/// <param name="PlayerUuid">Assigned player UUID.</param>
/// <param name="Username">Confirmed player username.</param>
/// <param name="AssignedEntityId">Authoritative ECS entity ID assigned to the player.</param>
public sealed record LoginSuccessPacket(
    Guid PlayerUuid,
    string Username,
    long AssignedEntityId) : IClientboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Login;
}
