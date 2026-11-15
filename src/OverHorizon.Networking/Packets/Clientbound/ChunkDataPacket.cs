using OverHorizon.Core.Math;

namespace OverHorizon.Networking.Packets.Clientbound;

/// <summary>
/// Streams a full 16x384x16 chunk column's compact section and light payload to a client in <see cref="ConnectionState.Play"/>.
/// </summary>
/// <param name="Position">Chunk column coordinates.</param>
/// <param name="Payload">Serialized chunk column section + heightmap + light payload.</param>
public sealed record ChunkDataPacket(ChunkPos Position, ReadOnlyMemory<byte> Payload) : IClientboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Play;
}
