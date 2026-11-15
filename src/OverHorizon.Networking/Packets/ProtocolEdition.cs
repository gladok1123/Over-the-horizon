namespace OverHorizon.Networking.Packets;

/// <summary>
/// Identifies the wire protocol family negotiated for a session (ADR 0004: Java in MVP, Bedrock in Beta).
/// </summary>
public enum ProtocolEdition : byte
{
    /// <summary>Java-like TCP + VarInt + ZLib stream framing.</summary>
    Java = 0,

    /// <summary>Reserved for Phase 2 Bedrock-like UDP/datagram adapter.</summary>
    Bedrock = 1,
}
