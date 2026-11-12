using Microsoft.Extensions.Logging;

namespace OverHorizon.Client;

internal static partial class ClientLog
{
    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        Message = "OverHorizon client initialized (RenderBackend={RenderBackend}, AudioInitialized={AudioInitialized}, Mode={Mode}).")]
    public static partial void ClientInitialized(ILogger logger, string renderBackend, bool audioInitialized, string mode);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Connected to multiplayer server {Host}:{Port} as '{Username}' (EntityId={EntityId}).")]
    public static partial void MultiplayerConnected(ILogger logger, string host, ushort port, string username, long entityId);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "F3 Telemetry:\n{OverlayText}")]
    public static partial void DebugTelemetrySnapshot(ILogger logger, string overlayText);
}
