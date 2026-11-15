using OverHorizon.Core.Math;

namespace OverHorizon.Networking.Packets.Clientbound;

/// <summary>
/// Notifies clients in <see cref="ConnectionState.Play"/> that a single block state changed.
/// </summary>
/// <param name="Position">World block coordinate.</param>
/// <param name="BlockStateId">New interned <c>BlockState.Id</c>.</param>
public sealed record BlockUpdatePacket(BlockPos Position, ushort BlockStateId) : IClientboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Play;
}
