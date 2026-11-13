namespace OverHorizon.Gameplay;

/// <summary>
/// Active gameplay ruleset for a player session.
/// </summary>
public enum GameMode : byte
{
    /// <summary>Finite resources, progressive block mining, health, hunger, fall damage, drowning, and crafting.</summary>
    Survival = 0,

    /// <summary>Infinite blocks &amp; items, instant block breaking (including Bedrock), flight, and damage immunity.</summary>
    Creative = 1,

    /// <summary>Noclip free-flight observer mode passing seamlessly through blocks and caves with night vision and full immunity.</summary>
    Spectator = 2,
}
