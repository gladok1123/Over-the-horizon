using OverHorizon.Networking.Packets;

namespace OverHorizon.Networking.Sessions;

internal readonly record struct InboundSessionEvent(
    long SessionId,
    long SequenceNumber,
    InboundEventKind Kind,
    string? Username,
    Guid PlayerUuid,
    IServerboundPacket? Packet) : IComparable<InboundSessionEvent>
{
    public int CompareTo(InboundSessionEvent other)
    {
        var sessionCmp = SessionId.CompareTo(other.SessionId);
        return sessionCmp != 0 ? sessionCmp : SequenceNumber.CompareTo(other.SequenceNumber);
    }
}
