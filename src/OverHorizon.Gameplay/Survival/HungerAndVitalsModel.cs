namespace OverHorizon.Gameplay.Survival;

/// <summary>
/// Vanilla-styled Survival <c>Hunger</c> (<c>0..20</c>), <c>Saturation</c> (<c>0..Hunger</c>), and <c>Exhaustion</c> (<c>4.0</c> threshold)
/// state machine driving sprint eligibility, saturation-boosted regeneration, and zero-hunger starvation.
/// </summary>
public sealed class HungerAndVitalsModel
{
    /// <summary>Exhaustion threshold at which 1 point of Saturation (or Hunger) is consumed.</summary>
    public const float ExhaustionThreshold = 4.0f;

    /// <summary>Exhaustion added per melee attack (<c>0.1</c>).</summary>
    public const float ExhaustionPerAttack = 0.1f;

    /// <summary>Exhaustion added per Survival block broken (<c>0.005</c>).</summary>
    public const float ExhaustionPerBlockMined = 0.005f;

    private double _regenTimer;
    private double _starvationTimer;

    /// <summary>Gets or sets current food level in <c>[0, 20]</c>.</summary>
    public int Hunger { get; set; } = 20;

    /// <summary>Gets or sets current saturation reserve in <c>[0.0, Hunger]</c>.</summary>
    public float Saturation { get; set; } = 5.0f;

    /// <summary>Gets or sets accumulated activity exhaustion.</summary>
    public float Exhaustion { get; set; }

    /// <summary>Gets whether the player has enough food (<c>&gt; 6</c>, i.e. more than 3 drumsticks) to sprint.</summary>
    public bool CanSprint => Hunger > 6;

    /// <summary>Resets food, saturation, exhaustion, and timers to full spawn defaults.</summary>
    public void Reset()
    {
        Hunger = 20;
        Saturation = 5.0f;
        Exhaustion = 0.0f;
        _regenTimer = 0.0;
        _starvationTimer = 0.0;
    }

    /// <summary>Adds activity exhaustion and immediately drains Saturation (then Hunger) for each <c>4.0</c> increment.</summary>
    public void AddExhaustion(float amount)
    {
        if (amount <= 0.0f)
        {
            return;
        }

        Exhaustion += amount;
        DrainExhaustionOverflow();
    }

    /// <summary>Checks whether the player can eat food right now.</summary>
    public bool CanEat(bool alwaysEdible = false) => alwaysEdible || Hunger < 20;

    /// <summary>Consumes food, increasing <see cref="Hunger"/> and clamping <see cref="Saturation"/> to <see cref="Hunger"/>.</summary>
    public void Eat(int nutrition, float saturationGain)
    {
        Hunger = Math.Clamp(Hunger + Math.Max(0, nutrition), 0, 20);
        Saturation = Math.Clamp(Saturation + MathF.Max(0.0f, saturationGain), 0.0f, Hunger);
    }

    /// <summary>
    /// Advances natural health regeneration and zero-hunger starvation for <paramref name="dt"/> seconds.
    /// Returns <c>(healAmount, starvationDamage)</c>.
    /// </summary>
    public (float HealAmount, float StarvationDamage) Step(double dt, float currentHealth, float maxHealth = 20.0f)
    {
        DrainExhaustionOverflow();

        if (currentHealth <= 0.0f)
        {
            _regenTimer = 0.0;
            _starvationTimer = 0.0;
            return (0.0f, 0.0f);
        }

        // Fast saturation-boosted healing at full food bar (20/20 + Saturation > 0)
        if (Hunger >= 20 && Saturation > 0.0f && currentHealth < maxHealth)
        {
            _starvationTimer = 0.0;
            _regenTimer += dt;
            if (_regenTimer >= 0.5)
            {
                _regenTimer -= 0.5;
                var heal = MathF.Min(1.0f, maxHealth - currentHealth);
                AddExhaustion(6.0f * heal);
                return (heal, 0.0f);
            }

            return (0.0f, 0.0f);
        }

        // Standard regeneration at >= 18 food
        if (Hunger >= 18 && currentHealth < maxHealth)
        {
            _starvationTimer = 0.0;
            _regenTimer += dt;
            if (_regenTimer >= 4.0)
            {
                _regenTimer -= 4.0;
                var heal = MathF.Min(1.0f, maxHealth - currentHealth);
                AddExhaustion(6.0f);
                return (heal, 0.0f);
            }

            return (0.0f, 0.0f);
        }

        _regenTimer = 0.0;

        // Starvation damage at 0 food
        if (Hunger <= 0)
        {
            _starvationTimer += dt;
            if (_starvationTimer >= 4.0)
            {
                _starvationTimer -= 4.0;
                return (0.0f, 1.0f);
            }
        }
        else
        {
            _starvationTimer = 0.0;
        }

        return (0.0f, 0.0f);
    }

    private void DrainExhaustionOverflow()
    {
        while (Exhaustion >= ExhaustionThreshold)
        {
            Exhaustion -= ExhaustionThreshold;
            if (Saturation > 0.0f)
            {
                Saturation = MathF.Max(0.0f, Saturation - 1.0f);
            }
            else if (Hunger > 0)
            {
                Hunger--;
            }
        }
    }
}
