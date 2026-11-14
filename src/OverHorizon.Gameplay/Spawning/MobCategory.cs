namespace OverHorizon.Gameplay.Spawning;

/// <summary>
/// Ecological categories controlling mob natural spawn rules.
/// </summary>
public enum MobCategory : byte
{
    /// <summary>Daylight surface grazing fauna.</summary>
    Passive = 0,

    /// <summary>Darkness-dwelling nocturnal or subterranean threats.</summary>
    Hostile = 1,
}
