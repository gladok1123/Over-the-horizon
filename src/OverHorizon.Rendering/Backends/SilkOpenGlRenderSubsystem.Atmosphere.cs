using System.Numerics;
using OverHorizon.Core;
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
        out bool isNether)
    {
        var underwater = overlay?.IsUnderwater == true;
        var skyColor = SilkOpenGlChunkPassDrawer.ComputeSkyAtmosphereColor(clampedSkyBrightness, overlay?.WorldTimeTicks ?? 1000L, underwater);
        if (!underwater && overlay is not null)
        {
            if (camera.Position.Y < EngineConstants.WorldMinY + 2.0)
            {
                var voidFactor = (float)Math.Clamp((EngineConstants.WorldMinY + 2.0 - camera.Position.Y) / 8.0, 0.35, 1.0);
                skyColor = Vector3.Lerp(skyColor, new Vector3(0.010f, 0.010f, 0.016f), voidFactor);
            }
            else if (camera.Position.Y < 54.0 && overlay.CameraSkyLightNormalized < 0.15f)
            {
                var caveDark = (1.0f - (overlay.CameraSkyLightNormalized / 0.15f)) * (float)Math.Clamp((54.0 - camera.Position.Y) / 14.0, 0.0, 1.0);
                skyColor = Vector3.Lerp(skyColor, new Vector3(0.018f, 0.018f, 0.024f), caveDark * 0.95f);
            }
        }

        isNether = overlay?.IsNetherDimension == true;
        if (isNether)
        {
            // The Nether has no sky: a flat sulfurous haze with heavy volcanic fog.
            skyColor = new Vector3(0.145f, 0.052f, 0.048f);
            chunkFogDistance = Math.Min(chunkFogDistance, 34.0f);
        }

        return skyColor;
    }
}
