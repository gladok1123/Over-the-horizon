using OverHorizon.Core.Math;

namespace OverHorizon.Physics;

/// <summary>
/// Concrete stateless/thread-safe <see cref="IPhysicsSubsystem"/> implementation.
/// </summary>
public sealed class PhysicsSubsystem : IPhysicsSubsystem
{
    /// <inheritdoc />
    public string Name => "Physics";

    /// <inheritdoc />
    public int PhaseOrder => 20;

    /// <inheritdoc />
    public PhysicsStepResult StepEntity(
        IVoxelCollisionReader world,
        Vec3d feetPosition,
        Vec3d velocity,
        double width,
        double height,
        double deltaSeconds) =>
        KinematicsIntegrator.StepEntity(world, feetPosition, velocity, width, height, deltaSeconds);

    /// <inheritdoc />
    public IReadOnlyList<FluidChange> StepFluids(
        IVoxelCollisionReader world,
        ReadOnlySpan<BlockPos> activePositions) =>
        FluidSimulator.EvaluateStep(world, activePositions);

    /// <inheritdoc />
    public ValueTask TickAsync(long tickNumber, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = tickNumber;
        return ValueTask.CompletedTask;
    }
}
