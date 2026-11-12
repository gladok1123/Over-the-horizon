using OverHorizon.Networking.Packets;

namespace OverHorizon.Networking.Sessions;

/// <summary>
/// Immutable descriptor of an authenticated player session exposed to gameplay handlers.
/// </summary>
/// <param name="SessionId">Unique monotonic 64-bit network session identifier.</param>
/// <param name="Edition">Negotiated protocol edition.</param>
/// <param name="Username">Player username.</param>
/// <param name="PlayerUuid">Player 128-bit UUID.</param>
/// <param name="AssignedEntityId">ECS entity ID (<c>0</c> prior to spawn).</param>
public readonly record struct PlayerSessionInfo(
    long SessionId,
    ProtocolEdition Edition,
    string Username,
    Guid PlayerUuid,
    long AssignedEntityId);
