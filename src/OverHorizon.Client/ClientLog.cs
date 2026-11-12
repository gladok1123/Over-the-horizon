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

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Information,
        Message = "[Chat] {Message}")]
    public static partial void SystemChatReceived(ILogger logger, string message);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Information,
        Message = "[Multiplayer] {Username} appeared in the world (EntityId={EntityId}).")]
    public static partial void RemotePlayerJoined(ILogger logger, string username, long entityId);

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Information,
        Message = "[Multiplayer] Player EntityId={EntityId} left the world.")]
    public static partial void RemotePlayerLeft(ILogger logger, long entityId);
}
