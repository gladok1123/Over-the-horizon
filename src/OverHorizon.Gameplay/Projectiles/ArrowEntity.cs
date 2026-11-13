using OverHorizon.Core.Math;

namespace OverHorizon.Gameplay.Projectiles;

/// <summary>
/// Live 3D ballistic arrow projectile fired by a player <c>Bow</c> or hostile <c>Skeleton</c>.
/// </summary>
public sealed class ArrowEntity
{
    /// <summary>Initializes a new instance of the <see cref="ArrowEntity"/> class.</summary>
    public ArrowEntity(
        int id,
        Vec3d position,
        Vec3d velocity,
        float baseDamage,
        bool isCritical,
        bool firedByPlayer,
        bool canBePickedUp)
    {
        Id = id;
        Position = position;
        Velocity = velocity;
        BaseDamage = baseDamage;
        IsCritical = isCritical;
        FiredByPlayer = firedByPlayer;
        CanBePickedUp = canBePickedUp;
        SyncAnglesFromVelocity();
    }

    /// <summary>Gets the unique runtime identifier of this arrow.</summary>
    public int Id { get; }

    /// <summary>Gets or sets the world-space tip/center position of the arrow.</summary>
    public Vec3d Position { get; set; }

    /// <summary>Gets or sets linear velocity in blocks/second.</summary>
    public Vec3d Velocity { get; set; }

    /// <summary>Gets base hit damage scaled by bow charge.</summary>
    public float BaseDamage { get; }

    /// <summary>Gets whether this arrow was released at full bow tension (<c>&gt;= 98%</c>).</summary>
    public bool IsCritical { get; }

    /// <summary>Gets whether this arrow was fired by the player (<see langword="false"/> if fired by a Skeleton).</summary>
    public bool FiredByPlayer { get; }

    /// <summary>Gets whether the player can recover this arrow into Survival inventory after it lands.</summary>
    public bool CanBePickedUp { get; }

    /// <summary>Gets or sets heading yaw in degrees.</summary>
    public float YawDegrees { get; set; }

    /// <summary>Gets or sets elevation pitch in degrees.</summary>
    public float PitchDegrees { get; set; }

    /// <summary>Gets or sets whether this arrow is embedded in a solid voxel block.</summary>
    public bool IsStuckInBlock { get; set; }

    /// <summary>Gets or sets the block coordinate in which this arrow is embedded.</summary>
    public BlockPos StuckBlockPos { get; set; }

    /// <summary>Gets or sets remaining impact twang/shake timer in seconds.</summary>
    public float ShakeTimer { get; set; }

    /// <summary>Gets or sets lifetime age in seconds (despawns at 60s).</summary>
    public float AgeSeconds { get; set; }

    /// <summary>Updates <see cref="YawDegrees"/> and <see cref="PitchDegrees"/> from <see cref="Velocity"/>.</summary>
    public void SyncAnglesFromVelocity()
    {
        var h = Math.Sqrt((Velocity.X * Velocity.X) + (Velocity.Z * Velocity.Z));
        if (h > 1e-4 || Math.Abs(Velocity.Y) > 1e-4)
        {
            YawDegrees = (float)(Math.Atan2(Velocity.X, Velocity.Z) * (180.0 / Math.PI));
            PitchDegrees = (float)(Math.Atan2(Velocity.Y, h) * (180.0 / Math.PI));
        }
    }
}
