using OverHorizon.Core.Math;

namespace OverHorizon.Networking.Packets.Serverbound;

/// <summary>
/// Client movement and look orientation update during <see cref="ConnectionState.Play"/>.
/// </summary>
/// <param name="Position">Proposed world-space feet position.</param>
/// <param name="YawDegrees">Horizontal yaw rotation in degrees.</param>
/// <param name="PitchDegrees">Vertical pitch rotation in degrees.</param>
/// <param name="OnGround">Client-reported grounded flag.</param>
public sealed record MovePlayerPacket(
    Vec3d Position,
    float YawDegrees,
    float PitchDegrees,
    bool OnGround) : IServerboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Play;
}
