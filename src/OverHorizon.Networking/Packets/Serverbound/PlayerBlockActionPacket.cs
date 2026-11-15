using OverHorizon.Core.Math;

namespace OverHorizon.Networking.Packets.Serverbound;

/// <summary>
/// Client block break or block placement request during <see cref="ConnectionState.Play"/>.
/// </summary>
/// <param name="Action">Break or place action kind.</param>
/// <param name="TargetPos">Target world block coordinate.</param>
/// <param name="Face">Targeted block face.</param>
/// <param name="BlockStateId">Interned <c>BlockState.Id</c> when placing (<c>0</c> when breaking).</param>
public sealed record PlayerBlockActionPacket(
    PlayerBlockActionKind Action,
    BlockPos TargetPos,
    Direction Face,
    ushort BlockStateId) : IServerboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Play;
}
