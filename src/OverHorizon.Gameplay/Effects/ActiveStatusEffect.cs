namespace OverHorizon.Gameplay.Effects;

/// <summary>
/// Immutable snapshot of one active status effect on a player.
/// </summary>
public readonly record struct ActiveStatusEffect(
    StatusEffectType Type,
    float RemainingSeconds);
