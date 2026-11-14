namespace OverHorizon.Physics;

/// <summary>
/// Clean-room kinematic, drag, Creative flight, and authentic water constants for deterministic 20 TPS simulation.
/// </summary>
public static class PhysicsConstants
{
    /// <summary>Vertical gravitational acceleration in blocks per second squared.</summary>
    public const double GravityAcceleration = -32.0;

    /// <summary>Maximum downward falling speed in blocks per second.</summary>
    public const double TerminalVelocity = -78.4;

    /// <summary>Per-tick airborne velocity retention factor.</summary>
    public const double AirDragFactor = 0.98;

    /// <summary>Per-tick horizontal velocity retention when sliding on solid ground.</summary>
    public const double GroundSlipperiness = 0.72;

    /// <summary>Per-tick velocity retention when hovering/flying in Creative mode.</summary>
    public const double FlightDragFactor = 0.82;

    /// <summary>Per-tick velocity retention when submerged in fluid.</summary>
    public const double FluidDragFactor = 0.80;

    /// <summary>Downward gravitational sink acceleration in water when not swimming up (blocks/s^2).</summary>
    public const double WaterGravityAcceleration = -3.2;

    /// <summary>Upward swim acceleration in water when holding Jump (blocks/s^2).</summary>
    public const double WaterSwimUpAcceleration = 16.0;

    /// <summary>Maximum upward vertical swim speed while submerged in water (blocks/s).</summary>
    public const double MaxWaterSwimUpSpeed = 3.6;

    /// <summary>Maximum passive downward sinking speed in still water (blocks/s).</summary>
    public const double MaxWaterSinkSpeed = -2.4;

    /// <summary>Horizontal acceleration exerted by flowing fluid currents (blocks/s^2).</summary>
    public const double FluidCurrentAcceleration = 6.0;

    /// <summary>
    /// Vanilla auto step-up height (<c>0.6</c> blocks) letting players and mobs walk straight onto
    /// slabs, <c>15/16</c> Dirt Paths, Farmland, and <c>1..2</c> pixel ledges without jumping.
    /// </summary>
    public const double StepHeight = 0.6;

    /// <summary>Maximum displacement per collision sub-step in blocks (prevents high-speed tunneling).</summary>
    public const double MaxSubstepDisplacement = 0.45;

    /// <summary>Numerical epsilon used when clamping swept AABB faces against voxel planes.</summary>
    public const double CollisionEpsilon = 1e-7;
}
