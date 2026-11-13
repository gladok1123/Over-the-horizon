using OverHorizon.Core.Math;

namespace OverHorizon.Gameplay.Mobs;

/// <summary>
/// Live world mob entity with 3D kinematics, limb animation phase, health, hurt flash, and AI timers.
/// </summary>
public sealed class ActiveMob
{
    /// <summary>Initializes a new instance of the <see cref="ActiveMob"/> class.</summary>
    public ActiveMob(int id, MobType type, Vec3d feetPosition, float initialYawDegrees = 0.0f)
    {
        Id = id;
        Type = type;
        FeetPosition = feetPosition;
        YawDegrees = initialYawDegrees;
        (Width, Height, MaxHealth, AttackDamage) = GetSpeciesStats(type);
        Health = MaxHealth;
    }

    /// <summary>Gets the unique runtime identifier of this mob.</summary>
    public int Id { get; }

    /// <summary>Gets the peaceful or hostile species of this mob.</summary>
    public MobType Type { get; }

    /// <summary>Gets whether this mob belongs to a hostile species.</summary>
    public bool IsHostile => Type is MobType.Zombie or MobType.Skeleton or MobType.Spider or MobType.Creeper;

    /// <summary>Gets horizontal hitbox width in blocks.</summary>
    public double Width { get; }

    /// <summary>Gets vertical hitbox height in blocks.</summary>
    public double Height { get; }

    /// <summary>Gets maximum health points.</summary>
    public float MaxHealth { get; }

    /// <summary>Gets melee or explosion base damage.</summary>
    public float AttackDamage { get; }

    /// <summary>Gets or sets world-space feet position.</summary>
    public Vec3d FeetPosition { get; set; }

    /// <summary>Gets or sets linear velocity in blocks/second.</summary>
    public Vec3d Velocity { get; set; }

    /// <summary>Gets or sets whether the mob is resting on solid ground.</summary>
    public bool OnGround { get; set; }

    /// <summary>Gets or sets body facing yaw in degrees.</summary>
    public float YawDegrees { get; set; }

    /// <summary>Gets or sets accumulated limb-swing phase in radians.</summary>
    public float WalkPhase { get; set; }

    /// <summary>Gets or sets current health points.</summary>
    public float Health { get; set; }

    /// <summary>Gets or sets red damage-flash intensity (<c>0..1</c>).</summary>
    public float HurtFlashTimer { get; set; }

    /// <summary>Gets or sets post-hit invulnerability timer in seconds.</summary>
    public float InvulnerabilityTimer { get; set; }

    /// <summary>Gets or sets cooldown timer before next melee attack.</summary>
    public float AttackCooldownTimer { get; set; }

    /// <summary>Gets or sets flee panic timer for peaceful animals after being hit.</summary>
    public float PanicTimer { get; set; }

    /// <summary>Gets or sets idle wander direction change timer.</summary>
    public float WanderChangeTimer { get; set; }

    /// <summary>Gets or sets desired horizontal movement speed in blocks/second.</summary>
    public float DesiredSpeed { get; set; }

    /// <summary>Gets or sets normalized Creeper ignition fuse progress (<c>0..1</c>).</summary>
    public float CreeperFuseTimer { get; set; }

    /// <summary>Gets or sets whether this hostile mob is actively pursuing a Survival player.</summary>
    public bool IsTargetingPlayer { get; set; }

    /// <summary>Gets or sets whether this Sheep has been sheared.</summary>
    public bool IsSheared { get; set; }

    /// <summary>Gets or sets remaining sunlight/fire burn timer in seconds (<c>&gt; 0</c> when burning).</summary>
    public float BurnTimer { get; set; }

    /// <summary>Gets or sets accumulated burn damage tick timer in seconds.</summary>
    public float BurnDamageTickTimer { get; set; }

    /// <summary>Gets whether this mob is currently engulfed in flames.</summary>
    public bool IsOnFire => BurnTimer > 0.01f;

    /// <summary>Gets whether this mob has zero health remaining.</summary>
    public bool IsDead => Health <= 0.0f;

    /// <summary>Gets the world-space 3D hitbox of this mob.</summary>
    public BoundingBox Hitbox => BoundingBox.FromEntityFeet(FeetPosition, Width, Height);

    /// <summary>Returns canonical hitbox dimensions, health, and damage for <paramref name="type"/>.</summary>
    public static (double Width, double Height, float MaxHealth, float Damage) GetSpeciesStats(MobType type) =>
        type switch
        {
            MobType.Pig => (0.85, 0.90, 10.0f, 0.0f),
            MobType.Cow => (0.90, 1.35, 10.0f, 0.0f),
            MobType.Sheep => (0.85, 1.25, 8.0f, 0.0f),
            MobType.Chicken => (0.45, 0.68, 4.0f, 0.0f),
            MobType.Zombie => (0.62, 1.90, 20.0f, 3.0f),
            MobType.Skeleton => (0.60, 1.92, 20.0f, 3.0f),
            MobType.Spider => (1.25, 0.85, 16.0f, 2.5f),
            MobType.Creeper => (0.62, 1.70, 20.0f, 10.0f),
            _ => (0.60, 1.80, 10.0f, 2.0f),
        };
}
