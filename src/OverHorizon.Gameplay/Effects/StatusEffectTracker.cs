using System.Numerics;

namespace OverHorizon.Gameplay.Effects;

/// <summary>
/// Tracks active player potion/food status effects, countdown durations, golden Absorption HP, melee/speed modifiers, and HUD badge descriptors.
/// </summary>
public sealed class StatusEffectTracker
{
    private readonly float[] _remainingSeconds = new float[6];
    private readonly List<ActiveStatusEffect> _activeScratch = new(6);

    /// <summary>Gets or sets remaining golden Absorption shield HP (<c>0..8</c>).</summary>
    public float AbsorptionHealth { get; set; }

    /// <summary>Gets snapshot list of currently active status effects.</summary>
    public IReadOnlyList<ActiveStatusEffect> ActiveEffects
    {
        get
        {
            _activeScratch.Clear();
            for (var i = 0; i < _remainingSeconds.Length; i++)
            {
                if (_remainingSeconds[i] > 0.05f)
                {
                    _activeScratch.Add(new ActiveStatusEffect((StatusEffectType)i, _remainingSeconds[i]));
                }
            }

            return _activeScratch;
        }
    }

    /// <summary>Clears all status effects (e.g. on death/respawn or <c>/effect clear</c>).</summary>
    public void Clear()
    {
        Array.Clear(_remainingSeconds);
        AbsorptionHealth = 0.0f;
    }

    /// <summary>Applies or extends <paramref name="effect"/> for at least <paramref name="durationSeconds"/>.</summary>
    public void ApplyEffect(StatusEffectType effect, float durationSeconds)
    {
        var idx = (int)effect;
        if ((uint)idx >= (uint)_remainingSeconds.Length) return;
        _remainingSeconds[idx] = MathF.Max(_remainingSeconds[idx], durationSeconds);
        if (effect == StatusEffectType.Absorption && AbsorptionHealth < 4.0f)
        {
            AbsorptionHealth = 4.0f;
        }
    }

    /// <summary>Returns <see langword="true"/> when <paramref name="effect"/> is currently active.</summary>
    public bool HasEffect(StatusEffectType effect)
    {
        var idx = (int)effect;
        return (uint)idx < (uint)_remainingSeconds.Length && _remainingSeconds[idx] > 0.01f;
    }

    /// <summary>Gets movement speed multiplier (<c>1.35</c> when <see cref="StatusEffectType.Swiftness"/> is active, else <c>1.0</c>).</summary>
    public double GetSpeedMultiplier() => HasEffect(StatusEffectType.Swiftness) ? 1.35 : 1.0;

    /// <summary>Gets bonus melee weapon damage (<c>+3.0f</c> when <see cref="StatusEffectType.Strength"/> is active, else <c>0.0f</c>).</summary>
    public float GetBonusMeleeDamage() => HasEffect(StatusEffectType.Strength) ? 3.0f : 0.0f;

    /// <summary>Absorbs incoming damage using golden <see cref="AbsorptionHealth"/> first and returns unabsorbed remainder.</summary>
    public float AbsorbDamage(float incomingDamage)
    {
        if (AbsorptionHealth <= 0.0f || incomingDamage <= 0.0f)
        {
            return incomingDamage;
        }

        var absorbed = MathF.Min(AbsorptionHealth, incomingDamage);
        AbsorptionHealth -= absorbed;
        if (AbsorptionHealth <= 0.01f)
        {
            AbsorptionHealth = 0.0f;
            _remainingSeconds[(int)StatusEffectType.Absorption] = 0.0f;
        }

        return incomingDamage - absorbed;
    }

    /// <summary>Advances active effect timers and returns HP regenerated this step.</summary>
    public float Step(float dt)
    {
        var clampedDt = Math.Clamp(dt, 0.0f, 0.25f);
        var hadRegen = HasEffect(StatusEffectType.Regeneration);

        for (var i = 0; i < _remainingSeconds.Length; i++)
        {
            if (_remainingSeconds[i] > 0.0f)
            {
                _remainingSeconds[i] = MathF.Max(0.0f, _remainingSeconds[i] - clampedDt);
            }
        }

        if (_remainingSeconds[(int)StatusEffectType.Absorption] <= 0.0f)
        {
            AbsorptionHealth = 0.0f;
        }

        return hadRegen ? clampedDt * 1.6f : 0.0f;
    }

    /// <summary>Gets human-readable HUD badge label for <paramref name="type"/>.</summary>
    public static string GetDisplayName(StatusEffectType type) => type switch
    {
        StatusEffectType.Swiftness => "Speed",
        StatusEffectType.Regeneration => "Regeneration",
        StatusEffectType.Strength => "Strength",
        StatusEffectType.NightVision => "Night Vision",
        StatusEffectType.Absorption => "Absorption",
        StatusEffectType.FireResistance => "Fire Resist",
        _ => "Effect",
    };

    /// <summary>Gets RGB accent color for <paramref name="type"/>.</summary>
    public static Vector3 GetIconColor(StatusEffectType type) => type switch
    {
        StatusEffectType.Swiftness => new Vector3(0.45f, 0.82f, 0.98f),
        StatusEffectType.Regeneration => new Vector3(0.96f, 0.42f, 0.72f),
        StatusEffectType.Strength => new Vector3(0.92f, 0.26f, 0.22f),
        StatusEffectType.NightVision => new Vector3(0.38f, 0.48f, 0.98f),
        StatusEffectType.Absorption => new Vector3(0.98f, 0.84f, 0.24f),
        _ => new Vector3(0.98f, 0.62f, 0.20f),
    };
}
