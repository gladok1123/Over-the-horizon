namespace OverHorizon.Rendering;

/// <summary>
/// Per-frame visibility, draw-call, and geometry counters captured by an <see cref="IRenderSubsystem"/>.
/// </summary>
/// <param name="VisibleSections">Number of uploaded chunk sections inside the camera frustum.</param>
/// <param name="CulledSections">Number of uploaded chunk sections rejected by frustum culling.</param>
/// <param name="DrawCalls">Number of opaque + translucent section draw calls submitted.</param>
/// <param name="TrianglesSubmitted">Total triangle count submitted across all visible passes.</param>
/// <param name="FrameCount">Monotonic frame counter since subsystem creation.</param>
public readonly record struct RenderFrameMetrics(
    int VisibleSections,
    int CulledSections,
    int DrawCalls,
    int TrianglesSubmitted,
    long FrameCount);
