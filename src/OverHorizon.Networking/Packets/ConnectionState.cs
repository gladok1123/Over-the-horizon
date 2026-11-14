namespace OverHorizon.Networking.Packets;

/// <summary>
/// Connection lifecycle state machine for a network session.
/// </summary>
public enum ConnectionState : byte
{
    /// <summary>Initial state awaiting a handshake intention packet.</summary>
    Handshaking = 0,

    /// <summary>Server list ping and MOTD status query state.</summary>
    Status = 1,

    /// <summary>Authentication, compression negotiation, and player profile login state.</summary>
    Login = 2,

    /// <summary>Active in-world gameplay state.</summary>
    Play = 3,

    /// <summary>Session has been terminated.</summary>
    Closed = 4,
}
