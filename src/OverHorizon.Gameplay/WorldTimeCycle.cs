using System.Globalization;
using System.Numerics;

namespace OverHorizon.Gameplay;

/// <summary>
/// Authoritative Minecraft-compatible <c>0..24000</c>-tick day/night celestial cycle computing sun/moon elevation,
/// <c>0.18..1.0</c> sky brightness, horizon sky color, sleep window, and undead sunlight burning threshold.
/// </summary>
public sealed class WorldTimeCycle
{
    /// <summary>Number of world ticks in one full day-night cycle (<c>20</c> real minutes at <c>20</c> Hz).</summary>
    public const long TicksPerDay = 24_000L;

    /// <summary>Morning tick (<c>/time set day</c>).</summary>
    public const long DayTicks = 1_000L;

    /// <summary>High noon tick (<c>/time set noon</c>).</summary>
    public const long NoonTicks = 6_000L;

    /// <summary>Golden sunset tick (<c>/time set sunset</c>).</summary>
    public const long SunsetTicks = 12_500L;

    /// <summary>Nightfall tick (<c>/time set night</c>).</summary>
    public const long NightTicks = 13_000L;

    /// <summary>Midnight tick (<c>/time set midnight</c>).</summary>
    public const long MidnightTicks = 18_000L;

    /// <summary>Sunrise tick (<c>/time set sunrise</c>).</summary>
    public const long SunriseTicks = 23_000L;

    private double _fractionalTicks;

    /// <summary>Initializes a new instance of the <see cref="WorldTimeCycle"/> class starting at morning (<c>1000</c>).</summary>
    public WorldTimeCycle(long initialTicks = DayTicks)
    {
        TotalTicks = Math.Max(0L, initialTicks);
    }

    /// <summary>Gets total elapsed world ticks.</summary>
    public long TotalTicks { get; private set; }

    /// <summary>Gets or sets whether automatic daylight progression is enabled.</summary>
    public bool DoDaylightCycle { get; set; } = true;

    /// <summary>Gets current time-of-day in <c>[0, 23999]</c>.</summary>
    public int TimeOfDay => (int)(((TotalTicks % TicksPerDay) + TicksPerDay) % TicksPerDay);

    /// <summary>Gets current day number (<c>0, 1, 2, ...</c>).</summary>
    public long DayNumber => TotalTicks / TicksPerDay;

    /// <summary>Gets celestial sun angle in radians (<c>0</c> = sunrise horizon, <c>pi/2</c> = noon zenith, <c>3pi/2</c> = midnight).</summary>
    public float SunAngleRadians => (TimeOfDay / (float)TicksPerDay) * MathF.Tau;

    /// <summary>Gets global sky brightness multiplier in <c>[0.18, 1.00]</c>.</summary>
    public float SkyBrightness
    {
        get
        {
            var elev = MathF.Sin(SunAngleRadians);
            var normalized = Math.Clamp((elev + 0.22f) / 0.72f, 0.0f, 1.0f);
            return 0.18f + (normalized * 0.82f);
        }
    }

    /// <summary>Gets RGB sky &amp; horizon fog color smoothly transitioning across dawn, day, sunset, and starry night.</summary>
    public Vector3 SkyColor
    {
        get
        {
            var elev = MathF.Sin(SunAngleRadians);
            var dayColor = new Vector3(0.47f, 0.69f, 0.99f);
            var sunsetColor = new Vector3(0.88f, 0.48f, 0.28f);
            var nightColor = new Vector3(0.035f, 0.050f, 0.115f);

            if (elev >= 0.24f)
            {
                return dayColor;
            }

            if (elev >= -0.08f)
            {
                var t = (elev + 0.08f) / 0.32f;
                return Vector3.Lerp(sunsetColor, dayColor, t);
            }

            if (elev >= -0.32f)
            {
                var t = (elev + 0.32f) / 0.24f;
                return Vector3.Lerp(nightColor, sunsetColor, t);
            }

            return nightColor;
        }
    }

    /// <summary>Gets whether players are allowed to sleep in a Bed (<c>12542..23459</c>).</summary>
    public bool IsNightForSleeping => TimeOfDay is >= 12_542 and <= 23_459;

    /// <summary>Gets whether sunlight is intense enough to ignite exposed Zombies and Skeletons.</summary>
    public bool IsDaylightForUndeadBurn => SkyBrightness >= 0.78f;

    /// <summary>Advances the world clock by <paramref name="deltaSeconds"/> (<c>20</c> ticks/s).</summary>
    public void AdvanceSeconds(double deltaSeconds)
    {
        if (!DoDaylightCycle || deltaSeconds <= 0.0)
        {
            return;
        }

        _fractionalTicks += Math.Min(deltaSeconds, 0.25) * 20.0;
        var whole = (long)_fractionalTicks;
        if (whole > 0)
        {
            _fractionalTicks -= whole;
            TotalTicks += whole;
        }
    }

    /// <summary>Sets the world time directly.</summary>
    public void SetTime(long ticks)
    {
        TotalTicks = Math.Max(0L, ticks);
        _fractionalTicks = 0.0;
    }

    /// <summary>Adds <paramref name="deltaTicks"/> to the world time.</summary>
    public void AddTime(long deltaTicks) =>
        SetTime(Math.Max(0L, TotalTicks + deltaTicks));

    /// <summary>Skips the current night forward to morning (<c>1000</c> ticks on the next day).</summary>
    public void SkipNightToMorning()
    {
        var nextDayStart = (DayNumber + 1L) * TicksPerDay;
        SetTime(nextDayStart + DayTicks);
    }

    /// <summary>Parses a <c>/time set</c> argument (<c>day</c>, <c>noon</c>, <c>sunset</c>, <c>night</c>, <c>midnight</c>, <c>sunrise</c>, or integer).</summary>
    public static bool TryParseTimeSpec(string token, out long ticks)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            ticks = 0;
            return false;
        }

        switch (token.Trim().ToUpperInvariant())
        {
            case "DAY":
            case "MORNING":
                ticks = DayTicks;
                return true;
            case "NOON":
            case "MIDDAY":
                ticks = NoonTicks;
                return true;
            case "SUNSET":
            case "DUSK":
                ticks = SunsetTicks;
                return true;
            case "NIGHT":
                ticks = NightTicks;
                return true;
            case "MIDNIGHT":
                ticks = MidnightTicks;
                return true;
            case "SUNRISE":
            case "DAWN":
                ticks = SunriseTicks;
                return true;
            default:
                return long.TryParse(token.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks) && ticks >= 0;
        }
    }
}
