using OverHorizon.Core.Math;

namespace OverHorizon.Gameplay.Vehicles;

/// <summary>
/// Live rideable 3D Minecart vehicle entity with rail-snap kinematics, slope pitch, corner yaw, and passenger state.
/// </summary>
public sealed class MinecartEntity
{
    /// <summary>Horizontal bounding-box width of a Minecart in blocks.</summary>
    public const double Width = 0.98;

    /// <summary>Vertical bounding-box height of a Minecart in blocks.</summary>
    public const double Height = 0.70;

    /// <summary>Initializes a new instance of the <see cref="MinecartEntity"/> class.</summary>
    public MinecartEntity(int id, Vec3d feetPosition, float yawDegrees = 0.0f)
    {
        Id = id;
        FeetPosition = feetPosition;
        YawDegrees = yawDegrees;
    }

    /// <summary>Gets the unique runtime identifier of this Minecart.</summary>
    public int Id { get; }

    /// <summary>Gets or sets the world-space bottom-center position of this Minecart.</summary>
    public Vec3d FeetPosition { get; set; }

    /// <summary>Gets or sets the linear velocity in blocks/second.</summary>
    public Vec3d Velocity { get; set; }

    /// <summary>Gets or sets visual heading yaw in degrees.</summary>
    public float YawDegrees { get; set; }

    /// <summary>Gets or sets visual slope pitch in degrees (<c>-45..+45</c> on ascending rails).</summary>
    public float PitchDegrees { get; set; }

    /// <summary>Gets or sets whether this Minecart is currently resting on a rail track.</summary>
    public bool IsOnRail { get; set; }

    /// <summary>Gets or sets whether the local player is currently riding inside this Minecart.</summary>
    public bool HasPlayerPassenger { get; set; }

    /// <summary>Gets or sets damage wobble timer when hit.</summary>
    public float HurtWobbleTimer { get; set; }

    /// <summary>Gets the world-space 3D hitbox of this Minecart.</summary>
    public BoundingBox Hitbox => BoundingBox.FromEntityFeet(FeetPosition, Width, Height);
}
