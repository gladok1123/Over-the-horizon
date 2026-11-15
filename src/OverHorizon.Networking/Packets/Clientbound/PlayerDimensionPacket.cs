using OverHorizon.Core;
using OverHorizon.Core.Math;

namespace OverHorizon.Networking.Packets.Clientbound;

/// <summary>
/// Authoritatively moves a connected client into a different dimension (used by Nether Portal travel),
/// instructing it to drop cached chunks from the previous dimension before the new ones arrive.
/// </summary>
/// <param name="Dimension">Destination dimension identifier.</param>
/// <param name="Position">Authoritative feet position in the destination dimension.</param>
/// <param name="YawDegrees">Authoritative body yaw after arrival.</param>
/// <param name="PitchDegrees">Authoritative head pitch after arrival.</param>
public sealed record PlayerDimensionPacket(
    DimensionId Dimension,
    Vec3d Position,
    float YawDegrees,
    float PitchDegrees) : IClientboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Play;
}
