using Microsoft.Extensions.Logging;

namespace OverHorizon.Networking;

internal static partial class NetworkingLog
{
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Network server listening on {EndPoint} (protocol {ProtocolVersion}).")]
    public static partial void ServerListening(ILogger logger, string endPoint, int protocolVersion);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Debug,
        Message = "Session {SessionId} disconnected ({Reason}).")]
    public static partial void SessionClosed(ILogger logger, long sessionId, string reason);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Warning,
        Message = "Session {SessionId} protocol violation: {Detail}")]
    public static partial void ProtocolViolation(ILogger logger, long sessionId, string detail);
}
