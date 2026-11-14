using OverHorizon.Core.Math;

namespace OverHorizon.Physics;

/// <summary>
/// Deterministic single-entity kinematic integrator combining gravity, Creative flight, fluid buoyancy/currents, swept collisions, and drag.
/// </summary>
public static class KinematicsIntegrator
{
    /// <summary>
    /// Advances an entity by <paramref name="deltaSeconds"/> against <paramref name="world"/>.
    /// Safe to execute concurrently from multiple worker threads over read-only chunk snapshots.
    /// </summary>
    /// <param name="world">Read-only collision and fluid reader.</param>
    /// <param name="feetPosition">Starting bottom-center position.</param>
    /// <param name="velocity">Starting linear velocity (blocks/s).</param>
    /// <param name="width">Horizontal entity width.</param>
    /// <param name="height">Vertical entity height.</param>
    /// <param name="deltaSeconds">Simulation step duration in seconds (typically <c>0.05</c>).</param>
    /// <param name="swimUp">When <see langword="true"/> in fluid, applies upward swimming propulsion instead of gentle sinking.</param>
    /// <param name="isFlying">When <see langword="true"/>, bypasses gravity and applies smooth Creative flight damping.</param>
    /// <returns>Resolved <see cref="PhysicsStepResult"/>.</returns>
    public static PhysicsStepResult StepEntity(
        IVoxelCollisionReader world,
        Vec3d feetPosition,
        Vec3d velocity,
        double width,
        double height,
        double deltaSeconds,
        bool swimUp = false,
        bool isFlying = false)
    {
        ArgumentNullException.ThrowIfNull(world);

        var footBlock = feetPosition.ToBlockPos();
        if (!world.IsColumnLoaded(footBlock))
        {
            return new PhysicsStepResult(feetPosition, Vec3d.Zero, OnGround: false, CollidedHorizontally: false, InFluid: false);
        }

        var box = BoundingBox.FromEntityFeet(feetPosition, width, height);
        var (inFluid, submergedDepth, currentPush) = SampleFluidContact(world, box);

        var vx = velocity.X;
        var vy = velocity.Y;
        var vz = velocity.Z;

        if (!isFlying)
        {
            if (inFluid)
            {
                if (swimUp)
                {
                    var surfaceScale = Math.Clamp(submergedDepth / 0.42, 0.35, 1.0);
                    vy = Math.Min(PhysicsConstants.MaxWaterSwimUpSpeed * surfaceScale, vy + (PhysicsConstants.WaterSwimUpAcceleration * surfaceScale * deltaSeconds));
                }
                else
                {
                    vy = Math.Max(PhysicsConstants.MaxWaterSinkSpeed, vy + (PhysicsConstants.WaterGravityAcceleration * deltaSeconds));
                }

                vx += currentPush.X * PhysicsConstants.FluidCurrentAcceleration * deltaSeconds;
                vz += currentPush.Z * PhysicsConstants.FluidCurrentAcceleration * deltaSeconds;
            }
            else
            {
                vy = Math.Max(
                    PhysicsConstants.TerminalVelocity,
                    vy + (PhysicsConstants.GravityAcceleration * deltaSeconds));
            }
        }

        var requestedMove = new Vec3d(vx * deltaSeconds, vy * deltaSeconds, vz * deltaSeconds);
        var (resolvedBox, actualMove, onGround, hitHorizontal) = VoxelSweptCollider.MoveAndCollide(world, box, requestedMove);

        // Vanilla 0.6-block auto step-up: walk straight onto slabs, 15/16 Dirt Paths, Farmland, and 1..2 pixel ledges!
        if (hitHorizontal && onGround && !isFlying && (requestedMove.X != 0.0 || requestedMove.Z != 0.0))
        {
            var raisedBox = box.Move(new Vec3d(0.0, PhysicsConstants.StepHeight, 0.0));
            var (steppedBox, steppedMove, _, blockedWhileRaised) = VoxelSweptCollider.MoveAndCollide(
                world, raisedBox, new Vec3d(requestedMove.X, 0.0, requestedMove.Z));

            var advance = Math.Abs(steppedMove.X) + Math.Abs(steppedMove.Z);
            if (!blockedWhileRaised || advance > 1e-6)
            {
                var (landedBox, downMove, landedOnGround, _) = VoxelSweptCollider.MoveAndCollide(
                    world, steppedBox, new Vec3d(0.0, -PhysicsConstants.StepHeight, 0.0));

                // Only accept the step when the entity actually settles on a ledge above its previous feet height!
                if (landedBox.MinY > box.MinY + 1e-6 && (landedOnGround || Math.Abs(downMove.Y) < PhysicsConstants.StepHeight - 1e-6))
                {
                    resolvedBox = landedBox;
                    actualMove = new Vec3d(steppedMove.X, landedBox.MinY - box.MinY, steppedMove.Z);
                    onGround = true;
                    hitHorizontal = blockedWhileRaised;
                    vy = 0.0;
                }
            }
        }

        if (Math.Abs(actualMove.Y - requestedMove.Y) > 1e-7)
        {
            vy = 0.0;
        }

        // Shoreline climb-out: when swimming up against a 1-block riverbank ledge, give a smooth hop onto shore!
        if (inFluid && swimUp && hitHorizontal && vy >= 0.0 && vy < 4.4)
        {
            vy = 4.4;
        }

        if (Math.Abs(actualMove.X - requestedMove.X) > 1e-7)
        {
            vx = 0.0;
        }

        if (Math.Abs(actualMove.Z - requestedMove.Z) > 1e-7)
        {
            vz = 0.0;
        }

        var tickScale = deltaSeconds * 20.0;
        var exactSingleTick = Math.Abs(tickScale - 1.0) <= 1e-6;
        if (isFlying)
        {
            var flightRetention = exactSingleTick
                ? PhysicsConstants.FlightDragFactor
                : Math.Pow(PhysicsConstants.FlightDragFactor, tickScale);
            vx *= flightRetention;
            vy *= flightRetention;
            vz *= flightRetention;
        }
        else if (inFluid)
        {
            var fluidRetention = exactSingleTick
                ? PhysicsConstants.FluidDragFactor
                : Math.Pow(PhysicsConstants.FluidDragFactor, tickScale);
            vx *= fluidRetention;
            vy *= fluidRetention;
            vz *= fluidRetention;
        }
        else
        {
            var baseHoriz = onGround
                ? PhysicsConstants.GroundSlipperiness * PhysicsConstants.AirDragFactor
                : PhysicsConstants.AirDragFactor;
            var horizRetention = exactSingleTick ? baseHoriz : Math.Pow(baseHoriz, tickScale);
            var vertRetention = exactSingleTick ? PhysicsConstants.AirDragFactor : Math.Pow(PhysicsConstants.AirDragFactor, tickScale);
            vx *= horizRetention;
            vy *= vertRetention;
            vz *= horizRetention;
        }

        var resolvedFeet = new Vec3d(
            (resolvedBox.MinX + resolvedBox.MaxX) * 0.5,
            resolvedBox.MinY,
            (resolvedBox.MinZ + resolvedBox.MaxZ) * 0.5);

        return new PhysicsStepResult(
            resolvedFeet,
            new Vec3d(vx, vy, vz),
            onGround,
            hitHorizontal,
            inFluid);
    }

    private static (bool InFluid, double MaxSubmergedDepth, Vec3d FlowDirection) SampleFluidContact(IVoxelCollisionReader world, BoundingBox box)
    {
        var contracted = box.Inflate(-0.001, -0.001, -0.001);
        var minX = (int)Math.Floor(contracted.MinX);
        var maxX = (int)Math.Floor(contracted.MaxX);
        var minY = (int)Math.Floor(contracted.MinY);
        var maxY = (int)Math.Floor(contracted.MaxY);
        var minZ = (int)Math.Floor(contracted.MinZ);
        var maxZ = (int)Math.Floor(contracted.MaxZ);

        var anyFluid = false;
        var maxSubmerged = 0.0;
        var flow = Vec3d.Zero;

        for (var y = minY; y <= maxY; y++)
        {
            for (var z = minZ; z <= maxZ; z++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var pos = new BlockPos(x, y, z);
                    var level = world.GetFluidLevelOrNegative(pos);
                    if (level < 0)
                    {
                        continue;
                    }

                    var aboveLevel = world.GetFluidLevelOrNegative(pos.Offset(Direction.Up));
                    var surfaceY = y + (aboveLevel >= 0 ? 1.0 : FluidSimulator.GetSurfaceHeight(level));
                    if (contracted.MinY >= surfaceY)
                    {
                        continue;
                    }

                    anyFluid = true;
                    var depth = Math.Min(contracted.MaxY, surfaceY) - contracted.MinY;
                    if (depth > maxSubmerged)
                    {
                        maxSubmerged = depth;
                    }

                    flow += ComputeHorizontalGradient(world, pos, level);
                }
            }
        }

        return (anyFluid, maxSubmerged, anyFluid ? flow.Normalize() : Vec3d.Zero);
    }

    private static Vec3d ComputeHorizontalGradient(IVoxelCollisionReader world, BlockPos center, int centerLevel)
    {
        var effectiveCenter = centerLevel >= 8 ? 0 : centerLevel;
        var push = Vec3d.Zero;

        foreach (var dir in DirectionExtensions.Horizontal)
        {
            var neighborPos = center.Offset(dir);
            var neighborLevel = world.GetFluidLevelOrNegative(neighborPos);
            if (neighborLevel < 0)
            {
                continue;
            }

            var effectiveNeighbor = neighborLevel >= 8 ? 0 : neighborLevel;
            var diff = effectiveNeighbor - effectiveCenter;
            if (diff != 0)
            {
                var n = dir.Normal();
                push += new Vec3d(n.X * diff, 0.0, n.Z * diff);
            }
        }

        return push;
    }
}
