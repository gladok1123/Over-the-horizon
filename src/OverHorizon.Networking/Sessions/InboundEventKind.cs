namespace OverHorizon.Networking.Sessions;

internal enum InboundEventKind : byte
{
    Login = 0,
    PlayPacket = 1,
    Disconnect = 2,
}
