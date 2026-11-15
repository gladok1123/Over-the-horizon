using System.Numerics;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Backend-agnostic dynamic sky atmosphere: the night/day/sunset colour ramp every renderer uses for both
/// its clear colour and its distance-fog colour.
/// </summary>
internal static class SkyAtmosphere
{
    private static readonly Vector3 DaySkyColor = new(0.47f, 0.68f, 0.99f);
    private static readonly Vector3 SunsetSkyColor = new(0.92f, 0.49f, 0.28f);
    private static readonly Vector3 NightSkyColor = new(0.035f, 0.048f, 0.105f);

    /// <summary>
    /// Interpolates the sky colour for the supplied time of day and sky-light level.
    /// </summary>
    /// <param name="skyBrightness">Global sky light multiplier in <c>[0.05, 1.0]</c>.</param>
    /// <param name="worldTimeTicks">Absolute world time in ticks.</param>
    /// <param name="underwater">Whether the camera eye is submerged.</param>
    /// <returns>Linear RGB sky (and fog) colour.</returns>
    public static Vector3 ComputeColor(float skyBrightness, long worldTimeTicks, bool underwater)
    {
        if (underwater)
        {
            return new Vector3(0.03f, 0.21f, 0.44f) * skyBrightness;
        }

        var tod = (int)(((worldTimeTicks % 24000L) + 24000L) % 24000L);
        var sunsetFactor = 0.0f;
        if (tod is >= 11600 and <= 13800)
        {
            sunsetFactor = 1.0f - (MathF.Abs(tod - 12700.0f) / 1100.0f);
        }
        else if (tod is >= 22200 or <= 400)
        {
            var wrapped = tod >= 22200 ? tod - 24000 : tod;
            sunsetFactor = 1.0f - (MathF.Abs(wrapped + 700.0f) / 1100.0f);
        }

        sunsetFactor = Math.Clamp(sunsetFactor, 0.0f, 1.0f);
        var dayMix = Math.Clamp((skyBrightness - 0.14f) / 0.86f, 0.0f, 1.0f);
        var baseCol = Vector3.Lerp(NightSkyColor, DaySkyColor, dayMix);
        if (sunsetFactor > 0.01f)
        {
            baseCol = Vector3.Lerp(baseCol, SunsetSkyColor, sunsetFactor * 0.58f);
        }

        return baseCol;
    }
}
