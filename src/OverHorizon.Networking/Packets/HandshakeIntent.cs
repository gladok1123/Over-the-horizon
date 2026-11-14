namespace OverHorizon.Networking.Packets;

/// <summary>
/// Next protocol state requested by a client during the initial handshake.
/// </summary>
public enum HandshakeIntent : byte
{
    /// <summary>Transition to <see cref="ConnectionState.Status"/> for server list ping.</summary>
    Status = 1,

    /// <summary>Transition to <see cref="ConnectionState.Login"/> to join the world.</summary>
    Login = 2,
}
