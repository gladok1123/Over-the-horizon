using Microsoft.Extensions.Logging;

namespace OverHorizon.Audio;

internal static partial class AudioLog
{
    [LoggerMessage(
        EventId = 8101,
        Level = LogLevel.Information,
        Message = "Silk.NET OpenAL 3D audio initialized with {BufferCount} procedural sound buffers and {SourceCount} voice channels.")]
    public static partial void OpenAlInitialized(ILogger logger, int bufferCount, int sourceCount);

    [LoggerMessage(
        EventId = 8102,
        Level = LogLevel.Information,
        Message = "OpenAL device unavailable ({Reason}); continuing in silent audio fallback mode.")]
    public static partial void OpenAlFallback(ILogger logger, string reason);
}
