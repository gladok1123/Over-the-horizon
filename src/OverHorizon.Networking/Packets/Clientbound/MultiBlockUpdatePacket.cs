using OverHorizon.Core.Math;

namespace OverHorizon.Networking.Packets.Clientbound;

/// <summary>
/// Batches multiple authoritative block state mutations (Redstone networks, Piston pushes, TNT craters, Bed parts,
/// or support detachments) into a single compact <see cref="ConnectionState.Play"/> packet.
/// </summary>
public sealed record MultiBlockUpdatePacket(IReadOnlyList<(BlockPos Position, ushort BlockStateId)> Updates) : IClientboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Play;
}
