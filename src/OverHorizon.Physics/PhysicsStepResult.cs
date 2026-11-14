using OverHorizon.Core.Math;

namespace OverHorizon.Physics;

/// <summary>
/// Immutable outcome of a single entity physics and swept-AABB collision step.
/// </summary>
/// <param name="Position">Resolved bottom-center world position.</param>
/// <param name="Velocity">Post-collision, post-drag linear velocity (blocks/s).</param>
/// <param name="OnGround">Whether the entity rested or landed on a solid downward surface.</param>
/// <param name="CollidedHorizontally">Whether X or Z motion was blocked by a solid wall.</param>
/// <param name="InFluid">Whether the entity bounding box overlaps any fluid block.</param>
public readonly record struct PhysicsStepResult(
    Vec3d Position,
    Vec3d Velocity,
    bool OnGround,
    bool CollidedHorizontally,
    bool InFluid);
