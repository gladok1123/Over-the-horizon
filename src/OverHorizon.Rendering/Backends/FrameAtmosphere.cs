using System.Numerics;
using OverHorizon.Core;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Hud;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Backend-agnostic per-frame atmosphere: the clear/fog colour plus the void, cave and Nether overrides that
/// every renderer applies before it draws the chunk passes.
/// </summary>
internal static class FrameAtmosphere
{
    /// <summary>
    /// Resolves the frame clear and fog colour, applying the void abyss, deep-cave and Nether overrides.
    /// </summary>
    /// <param name="overlay">Optional per-frame overlay state.</param>
    /// <param name="camera">Active first-person camera.</param>
    /// <param name="clampedSkyBrightness">Sky light multiplier clamped to <c>[0.08, 1.0]</c>.</param>
    /// <param name="chunkFogDistance">
    /// Chunk fog distance in blocks; shortened inside the Nether (passed by reference and rewritten).
    /// </param>
    /// <param name="isNether">Receives whether the active dimension is the Nether.</param>
    /// <returns>Linear RGB sky/fog colour.</returns>
    public static Vector3 ComputeSkyAndFogColor(
        FrameOverlayState? overlay,
        FirstPersonCamera camera,
        float clampedSkyBrightness,
        ref float chunkFogDistance,
        out bool isNether)
    {
        var underwater = overlay?.IsUnderwater == true;
        var skyColor = SkyAtmosphere.ComputeColor(clampedSkyBrightness, overlay?.WorldTimeTicks ?? 1000L, underwater);
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
