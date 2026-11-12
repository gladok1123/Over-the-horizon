using OverHorizon.Networking.Packets;

namespace OverHorizon.Networking.Sessions;

internal readonly record struct OutboundEnvelope(
    ConnectionState EncodeState,
    IClientboundPacket Packet,
    int? NewCompressionThresholdAfterSend = null,
    bool CloseAfterSend = false);
