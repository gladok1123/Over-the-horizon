using Microsoft.Extensions.Logging;

namespace OverHorizon.Server;

internal static partial class ServerLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "OverHorizon dedicated server starting at {TicksPerSecond} TPS with {SubsystemCount} subsystems.")]
    public static partial void ServerStarting(ILogger logger, int ticksPerSecond, int subsystemCount);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "OverHorizon dedicated server stopped cleanly after {CompletedTicks} ticks.")]
    public static partial void ServerStopped(ILogger logger, long completedTicks);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Tick {TickNumber} exceeded budget ({ElapsedMilliseconds:F2} ms > {BudgetMilliseconds:F2} ms).")]
    public static partial void TickOverrun(ILogger logger, long tickNumber, double elapsedMilliseconds, double budgetMilliseconds);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Multiplayer TCP listener READY on port {Port} (ViewDistance={ViewDistance} chunks, Seed={WorldSeed}, SaveDir={SaveDir}). Friends can connect via --connect <your-ip>:{Port}")]
    public static partial void ListeningReady(ILogger logger, int port, int viewDistance, long worldSeed, string saveDir);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "Saved {SavedChunkCount} modified chunk(s) to disk.")]
    public static partial void WorldSaved(ILogger logger, int savedChunkCount);
}
