using OverHorizon.Core.Math;

namespace OverHorizon.Networking.Packets.Clientbound;

/// <summary>
/// Authoritative server player position and orientation sync (initial spawn or anti-cheat rubber-band).
/// </summary>
/// <param name="Position">Authoritative world-space feet position.</param>
/// <param name="YawDegrees">Horizontal yaw in degrees.</param>
/// <param name="PitchDegrees">Vertical pitch in degrees.</param>
/// <param name="TeleportId">Monotonic teleport correlation identifier.</param>
public sealed record SynchronizePlayerPositionPacket(
    Vec3d Position,
    float YawDegrees,
    float PitchDegrees,
    int TeleportId) : IClientboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Play;
}
