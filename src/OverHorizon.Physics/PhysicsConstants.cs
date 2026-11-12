namespace OverHorizon.Physics;

/// <summary>
/// Clean-room kinematic, drag, and fluid constants for deterministic 20 TPS simulation.
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
    public const double GroundSlipperiness = 0.60;

    /// <summary>Per-tick velocity retention when submerged in fluid.</summary>
    public const double FluidDragFactor = 0.80;

    /// <summary>Upward buoyant acceleration when submerged in fluid (blocks/s^2).</summary>
    public const double FluidBuoyancyAcceleration = 22.0;

    /// <summary>Horizontal acceleration exerted by flowing fluid currents (blocks/s^2).</summary>
    public const double FluidCurrentAcceleration = 6.0;

    /// <summary>Maximum displacement per collision sub-step in blocks (prevents high-speed tunneling).</summary>
    public const double MaxSubstepDisplacement = 0.45;

    /// <summary>Numerical epsilon used when clamping swept AABB faces against voxel planes.</summary>
    public const double CollisionEpsilon = 1e-7;
}
