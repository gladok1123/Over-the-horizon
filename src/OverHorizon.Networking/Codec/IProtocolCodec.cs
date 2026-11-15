using System.Buffers;
using OverHorizon.Networking.Packets;

namespace OverHorizon.Networking.Codec;

/// <summary>
/// Bidirectional wire codec mapping between edition-specific byte payloads and domain packet records.
/// </summary>
public interface IProtocolCodec
{
    /// <summary>Gets the protocol edition handled by this codec.</summary>
    ProtocolEdition Edition { get; }

    /// <summary>
    /// Encodes a <see cref="IServerboundPacket"/> (<c>[PacketId: VarInt][Fields...]</c>) into <paramref name="destination"/>.
    /// </summary>
    /// <param name="state">Current connection state.</param>
    /// <param name="packet">Packet to encode.</param>
    /// <param name="destination">Target buffer writer.</param>
    void EncodeServerbound(ConnectionState state, IServerboundPacket packet, IBufferWriter<byte> destination);

    /// <summary>
    /// Decodes an uncompressed <c>[PacketId: VarInt][Fields...]</c> payload into a <see cref="IServerboundPacket"/>.
    /// </summary>
    /// <param name="state">Current connection state.</param>
    /// <param name="payload">Uncompressed packet body.</param>
    /// <returns>Decoded <see cref="IServerboundPacket"/>.</returns>
    IServerboundPacket DecodeServerbound(ConnectionState state, ReadOnlySpan<byte> payload);

    /// <summary>
    /// Encodes a <see cref="IClientboundPacket"/> (<c>[PacketId: VarInt][Fields...]</c>) into <paramref name="destination"/>.
    /// </summary>
    /// <param name="state">Current connection state.</param>
    /// <param name="packet">Packet to encode.</param>
    /// <param name="destination">Target buffer writer.</param>
    void EncodeClientbound(ConnectionState state, IClientboundPacket packet, IBufferWriter<byte> destination);

    /// <summary>
    /// Decodes an uncompressed <c>[PacketId: VarInt][Fields...]</c> payload into a <see cref="IClientboundPacket"/>.
    /// </summary>
    /// <param name="state">Current connection state.</param>
    /// <param name="payload">Uncompressed packet body.</param>
    /// <returns>Decoded <see cref="IClientboundPacket"/>.</returns>
    IClientboundPacket DecodeClientbound(ConnectionState state, ReadOnlySpan<byte> payload);
}
