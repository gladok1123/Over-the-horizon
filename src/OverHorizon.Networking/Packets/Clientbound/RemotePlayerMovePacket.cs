using OverHorizon.Core.Math;

namespace OverHorizon.Networking.Packets.Clientbound;

/// <summary>
/// Streams a remote player's updated feet position and look angles to peer clients in <see cref="ConnectionState.Play"/>.
/// </summary>
/// <param name="EntityId">Authoritative server ECS entity ID of the remote player.</param>
/// <param name="Position">Updated world-space feet position.</param>
/// <param name="YawDegrees">Updated horizontal yaw in degrees.</param>
/// <param name="PitchDegrees">Updated vertical pitch in degrees.</param>
public sealed record RemotePlayerMovePacket(
    long EntityId,
    Vec3d Position,
    float YawDegrees,
    float PitchDegrees) : IClientboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Play;
}
