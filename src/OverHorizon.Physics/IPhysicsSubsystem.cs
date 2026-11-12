using OverHorizon.Core;
using OverHorizon.Core.Math;

namespace OverHorizon.Physics;

/// <summary>
/// Executes AABB swept collision resolution, gravity/buoyancy kinematics, and cellular fluid simulation.
/// </summary>
public interface IPhysicsSubsystem : ITickSubsystem
{
    /// <summary>
    /// Integrates one entity's kinematics and voxel collisions over <paramref name="deltaSeconds"/>.
    /// </summary>
    /// <param name="world">Read-only collision and fluid reader.</param>
    /// <param name="feetPosition">Starting bottom-center position.</param>
    /// <param name="velocity">Starting velocity (blocks/s).</param>
    /// <param name="width">Horizontal width.</param>
    /// <param name="height">Vertical height.</param>
    /// <param name="deltaSeconds">Step duration in seconds.</param>
    /// <returns>Resolved <see cref="PhysicsStepResult"/>.</returns>
    PhysicsStepResult StepEntity(
        IVoxelCollisionReader world,
        Vec3d feetPosition,
        Vec3d velocity,
        double width,
        double height,
        double deltaSeconds);

    /// <summary>
    /// Evaluates one deterministic cellular fluid propagation step.
    /// </summary>
    /// <param name="world">Read-only collision and fluid reader.</param>
    /// <param name="activePositions">Candidate fluid blocks to evaluate.</param>
    /// <returns>Sorted list of <see cref="FluidChange"/> mutations.</returns>
    IReadOnlyList<FluidChange> StepFluids(
        IVoxelCollisionReader world,
        ReadOnlySpan<BlockPos> activePositions);
}
