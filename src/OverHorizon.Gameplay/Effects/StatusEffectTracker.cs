using System.Numerics;

namespace OverHorizon.Gameplay.Effects;

/// <summary>
/// Active player status / potion effect archetypes.
/// </summary>
public enum StatusEffectKind : byte
{
    /// <summary>Increases horizontal movement speed (+35%).</summary>
    Speed = 0,

    /// <summary>Restores health smoothly over time.</summary>
    Regeneration = 1,

    /// <summary>Provides temporary golden absorption shield HP.</summary>
    Absorption = 2,

    /// <summary>Brightens dark caves and night environments.</summary>
    NightVision = 3,

    /// <summary>Grants complete immunity to Fire burn damage.</summary>
    FireResistance = 4,

    /// <summary>Engulfed in flames (quenched immediately by Water or Fire Resistance).</summary>
    Burning = 5,
}

/// <summary>
/// Tracks active player status effects, countdown durations, absorption shield HP, and HUD badge descriptors.
/// </summary>
public sealed class StatusEffectTracker
{
    private readonly float[] _remainingSeconds = new float[6];

    /// <summary>Gets or sets remaining golden Absorption shield HP (<c>0..8</c>).</summary>
    public float AbsorptionHealth { get; set; }

    /// <summary>Gets whether the player has active Speed boost.</summary>
    public bool HasSpeed => _remainingSeconds[(int)StatusEffectKind.Speed] > 0.01f;

    /// <summary>Gets whether the player has active Night Vision.</summary>
    public bool HasNightVision => _remainingSeconds[(int)StatusEffectKind.NightVision] > 0.01f;

    /// <summary>Gets whether the player has active Fire Resistance.</summary>
    public bool HasFireResistance => _remainingSeconds[(int)StatusEffectKind.FireResistance] > 0.01f;

    /// <summary>Gets whether the player is currently burning.</summary>
    public bool IsBurning => _remainingSeconds[(int)StatusEffectKind.Burning] > 0.01f && !HasFireResistance;

    /// <summary>Gets the movement speed multiplier from active effects (<c>1.0</c> or <c>1.35</c>).</summary>
    public float SpeedMultiplier => HasSpeed ? 1.35f : 1.0f;

    /// <summary>Clears all status effects (e.g. on death/respawn).</summary>
    public void Clear()
    {
        Array.Clear(_remainingSeconds);
        AbsorptionHealth = 0.0f;
    }

    /// <summary>Applies or extends <paramref name="effect"/> for at least <paramref name="durationSeconds"/>.</summary>
    /// <param name="effect">Status effect archetype.</param>
    /// <param name="durationSeconds">Duration in seconds.</param>
    public void ApplyEffect(StatusEffectKind effect, float durationSeconds)
    {
        var idx = (int)effect;
        if ((uint)idx >= (uint)_remainingSeconds.Length) return;
        _remainingSeconds[idx] = MathF.Max(_remainingSeconds[idx], durationSeconds);
        if (effect == StatusEffectKind.Absorption && AbsorptionHealth < 4.0f)
        {
            AbsorptionHealth = 4.0f;
        }
    }

    /// <summary>Extinguishes active fire burning immediately.</summary>
    public void ExtinguishFire() => _remainingSeconds[(int)StatusEffectKind.Burning] = 0.0f;

    /// <summary> Gets remaining duration in seconds for <paramref name="effect"/>.</summary>
    public float GetRemainingSeconds(StatusEffectKind effect) => _remainingSeconds[(int)effect];

    /// <summary>
    /// Advances effect timers and returns `(RegenHeal, BurnDamage)` accumulated this tick.
    /// </summary>
    public (float RegenHeal, float BurnDamage) Step(float dt, bool inWater)
    {
        if (inWater && _remainingSeconds[(int)StatusEffectKind.Burning] > 0.0f)
        {
            _remainingSeconds[(int)StatusEffectKind.Burning] = 0.0f;
        }

        var prevBurn = _remainingSeconds[(int)StatusEffectKind.Burning];
        var prevRegen = _remainingSeconds[(int)StatusEffectKind.Regeneration];

        for (var i = 0; i < _remainingSeconds.Length; i++)
        {
            if (_remainingSeconds[i] > 0.0f)
            {
                _remainingSeconds[i] = MathF.Max(0.0f, _remainingSeconds[i] - dt);
            }
        }

        if (_remainingSeconds[(int)StatusEffectKind.Absorption] <= 0.0f)
        {
            AbsorptionHealth = 0.0f;
        }

        var heal = prevRegen > 0.01f ? dt * 1.6f : 0.0f;
        var burnDmg = 0.0f;
        if (prevBurn > 0.01f && !HasFireResistance && !inWater)
        {
            // Trigger 1.0f burn damage each time Burning crosses an integer 0.8s boundary
            if ((int)(prevBurn / 0.8f) != (int)(_remainingSeconds[(int)StatusEffectKind.Burning] / 0.8f))
            {
                burnDmg = 1.0f;
            }
        }

        return (heal, burnDmg);
    }

    /// <summary>Collects active HUD badge labels and RGB colors for rendering.</summary>
    public void BuildHudBadges(List<(string Label, Vector3 Color)> destination)
    {
        destination.Clear();
        for (var i = 0; i < _remainingSeconds.Length; i++)
        {
            var rem = _remainingSeconds[i];
            if (rem <= 0.05f) continue;
            var kind = (StatusEffectKind)i;
            if (kind == StatusEffectKind.Burning && HasFireResistance) continue;

            var totalSec = Math.Max(1, (int)MathF.Ceiling(rem));
            var mins = totalSec / 60;
            var secs = totalSec % 60;
            var (name, color) = kind switch
            {
                StatusEffectKind.Speed => ("Speed", new Vector3(0.45f, 0.82f, 0.98f)),
                StatusEffectKind.Regeneration => ("Regeneration", new Vector3(0.96f, 0.42f, 0.72f)),
                StatusEffectKind.Absorption => ($"Absorption (+{(int)MathF.Ceiling(AbsorptionHealth)})", new Vector3(0.98f, 0.84f, 0.24f)),
                StatusEffectKind.NightVision => ("Night Vision", new Vector3(0.38f, 0.48f, 0.98f)),
                StatusEffectKind.FireResistance => ("Fire Resist", new Vector3(0.98f, 0.62f, 0.20f)),
                _ => ("On Fire!", new Vector3(0.98f, 0.34f, 0.16f)),
            };
            destination.Add(($"{name} {mins}:{secs:D2}", color));
        }
    }
}
