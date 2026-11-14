using OverHorizon.Core.Math;

namespace OverHorizon.Gameplay.Physics;

/// <summary>
/// Live physics-simulated primed TNT entity with gravity, swept voxel collision, <c>4.0</c>-second fuse, and white flash phase.
/// </summary>
public sealed class PrimedTntEntity
{
    /// <summary>Initializes a new instance of the <see cref="PrimedTntEntity"/> class.</summary>
    public PrimedTntEntity(int id, Vec3d feetPosition, Vec3d velocity, float fuseSeconds = 4.0f)
    {
        Id = id;
        FeetPosition = feetPosition;
        Velocity = velocity;
        FuseSeconds = fuseSeconds;
    }

    /// <summary>Gets unique entity identifier.</summary>
    public int Id { get; }

    /// <summary>Gets or sets world-space bottom-center coordinate.</summary>
    public Vec3d FeetPosition { get; set; }

    /// <summary>Gets or sets linear velocity in blocks/s.</summary>
    public Vec3d Velocity { get; set; }

    /// <summary>Gets or sets remaining fuse time in seconds before detonation.</summary>
    public float FuseSeconds { get; set; }

    /// <summary>Gets whether the TNT block is currently in its bright white flash half-cycle.</summary>
    public bool IsFlashingWhite => ((int)(FuseSeconds * 10.0f) & 1) == 0;
}
