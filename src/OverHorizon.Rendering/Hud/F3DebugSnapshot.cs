using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;

namespace OverHorizon.Rendering.Hud;

/// <summary>
/// Telemetry snapshot backing the F3 diagnostic overlay.
/// </summary>
/// <param name="BackendName">Active graphics backend name.</param>
/// <param name="FramesPerSecond">Smoothed frame rate.</param>
/// <param name="FrameTimeMilliseconds">Most recent frame duration in milliseconds.</param>
/// <param name="PlayerPosition">World-space eye or feet position.</param>
/// <param name="Facing">Cardinal facing direction.</param>
/// <param name="YawDegrees">Camera yaw in degrees.</param>
/// <param name="PitchDegrees">Camera pitch in degrees.</param>
/// <param name="Biome">Current biome key.</param>
/// <param name="VisibleSections">Number of frustum-visible non-empty sections drawn this frame.</param>
/// <param name="TotalMeshedSections">Total meshed chunk sections resident on client.</param>
/// <param name="DrawnVertices">Total vertices submitted in the current frame.</param>
/// <param name="ManagedMemoryBytes">Approximate managed heap allocation in bytes.</param>
public readonly record struct F3DebugSnapshot(
    string BackendName,
    double FramesPerSecond,
    double FrameTimeMilliseconds,
    Vec3d PlayerPosition,
    Direction Facing,
    float YawDegrees,
    float PitchDegrees,
    ResourceKey Biome,
    int VisibleSections,
    int TotalMeshedSections,
    long DrawnVertices,
    long ManagedMemoryBytes);
