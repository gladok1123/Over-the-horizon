namespace OverHorizon.Gameplay.Effects;

/// <summary>
/// Active player status / potion effect archetypes.
/// </summary>
public enum StatusEffectType : byte
{
    /// <summary>Increases horizontal movement speed (+35%).</summary>
    Swiftness = 0,

    /// <summary>Restores health smoothly over time.</summary>
    Regeneration = 1,

    /// <summary>Increases melee attack damage (+3.0).</summary>
    Strength = 2,

    /// <summary>Brightens dark caves and night environments.</summary>
    NightVision = 3,

    /// <summary>Provides temporary golden absorption shield HP (+4 HP).</summary>
    Absorption = 4,

    /// <summary>Grants complete immunity to Fire burn damage.</summary>
    FireResistance = 5,
}
