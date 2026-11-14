using System.Globalization;
using System.Text;

namespace OverHorizon.Rendering.Hud;

/// <summary>
/// Formats an <see cref="F3DebugSnapshot"/> into human-readable F3 diagnostic overlay lines.
/// </summary>
public static class F3OverlayFormatter
{
    /// <summary>
    /// Formats <paramref name="snapshot"/> into a multi-line F3 telemetry string using invariant culture.
    /// </summary>
    /// <param name="snapshot">Telemetry snapshot.</param>
    /// <returns>Formatted multi-line F3 text.</returns>
    public static string Format(in F3DebugSnapshot snapshot)
    {
        var blockPos = snapshot.PlayerPosition.ToBlockPos();
        var chunkPos = blockPos.ToChunkPos();
        var secPos = blockPos.ToSectionPos();
        var memMiB = snapshot.ManagedMemoryBytes / (1024.0 * 1024.0);

        var sb = new StringBuilder(384);
        sb.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"OverHorizon 0.1 ({snapshot.BackendName}) | {snapshot.FramesPerSecond:F1} FPS ({snapshot.FrameTimeMilliseconds:F2} ms)"));
        sb.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"XYZ: {snapshot.PlayerPosition.X:F3} / {snapshot.PlayerPosition.Y:F3} / {snapshot.PlayerPosition.Z:F3}"));
        sb.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Block: {blockPos.X} {blockPos.Y} {blockPos.Z} | Chunk: [{chunkPos.X}, {chunkPos.Z}] | Section: ({secPos.X}, {secPos.Y}, {secPos.Z})"));
        sb.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Facing: {snapshot.Facing} (Yaw {snapshot.YawDegrees:F1}, Pitch {snapshot.PitchDegrees:F1}) | Biome: {snapshot.Biome}"));
        sb.Append(string.Create(
            CultureInfo.InvariantCulture,
            $"Sections: {snapshot.VisibleSections}/{snapshot.TotalMeshedSections} | Vertices: {snapshot.DrawnVertices} | Mem: {memMiB:F1} MiB"));

        return sb.ToString();
    }
}
