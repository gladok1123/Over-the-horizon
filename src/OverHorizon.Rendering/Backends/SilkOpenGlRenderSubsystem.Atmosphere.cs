using System.Numerics;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Hud;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Sky, void, and cave atmosphere colouring plus the Nether fog override for <see cref="SilkOpenGlRenderSubsystem"/>
/// (split out so the render subsystem itself stays under the 500-line architecture budget).
/// </summary>
public sealed partial class SilkOpenGlRenderSubsystem
{
    private static Vector3 ComputeSkyAndFogColor(
        FrameOverlayState? overlay,
        FirstPersonCamera camera,
        float clampedSkyBrightness,
        ref float chunkFogDistance,
        out bool isNether) =>
        FrameAtmosphere.ComputeSkyAndFogColor(overlay, camera, clampedSkyBrightness, ref chunkFogDistance, out isNether);
}
