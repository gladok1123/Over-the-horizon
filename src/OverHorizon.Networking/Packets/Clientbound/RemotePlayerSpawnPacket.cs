using OverHorizon.Core.Math;

namespace OverHorizon.Networking.Packets.Clientbound;

/// <summary>
/// Notifies connected clients that another player is present in the world so their 3D avatar can be rendered.
/// </summary>
/// <param name="EntityId">Authoritative server ECS entity ID of the remote player.</param>
/// <param name="Username">Remote player display username.</param>
/// <param name="Position">World-space feet position.</param>
/// <param name="YawDegrees">Horizontal yaw in degrees.</param>
/// <param name="PitchDegrees">Vertical pitch in degrees.</param>
public sealed record RemotePlayerSpawnPacket(
    long EntityId,
    string Username,
    Vec3d Position,
    float YawDegrees,
    float PitchDegrees) : IClientboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Play;
}
