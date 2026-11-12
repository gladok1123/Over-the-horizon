using Microsoft.Extensions.Logging;

namespace OverHorizon.Rendering;

internal static partial class RenderingLog
{
    [LoggerMessage(
        EventId = 8001,
        Level = LogLevel.Information,
        Message = "Silk.NET OpenGL 3.3 context initialized ({Width}x{Height}, VSync={VSync}).")]
    public static partial void ContextInitialized(ILogger logger, int width, int height, bool vSync);

    [LoggerMessage(
        EventId = 8002,
        Level = LogLevel.Information,
        Message = "Uploaded procedural block texture atlas ({Width}x{Height}, {TileSize}px tiles).")]
    public static partial void AtlasUploaded(ILogger logger, int width, int height, int tileSize);
}
