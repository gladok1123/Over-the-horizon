using System.Numerics;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// One frustum-visible chunk section together with the camera-relative offset its draw call needs, produced by
/// the (optionally multi-threaded) culling stage and consumed by the single-threaded draw stage.
/// </summary>
/// <param name="Section">Resident GPU section buffer to draw.</param>
/// <param name="CameraRelativeOffset">Section origin relative to the camera, in blocks.</param>
/// <param name="DistanceSq">Squared distance from the camera to the section centre, used for back-to-front sorting.</param>
internal readonly record struct CulledSectionDraw(
    GpuSectionBuffer Section,
    Vector3 CameraRelativeOffset,
    float DistanceSq);
