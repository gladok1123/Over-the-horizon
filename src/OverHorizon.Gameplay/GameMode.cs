namespace OverHorizon.Gameplay;

/// <summary>
/// Active gameplay ruleset for a player session.
/// </summary>
public enum GameMode : byte
{
    /// <summary>Finite resources, progressive block mining, health, hunger, fall damage, drowning, and crafting.</summary>
    Survival = 0,

    /// <summary>Infinite blocks &amp; items, instant block breaking, and invulnerability.</summary>
    Creative = 1,
}
