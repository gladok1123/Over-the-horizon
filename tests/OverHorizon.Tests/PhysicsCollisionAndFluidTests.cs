using FluentAssertions;
using OverHorizon.Core.Math;
using OverHorizon.Physics;
using Xunit;

namespace OverHorizon.Tests;

public sealed class PhysicsCollisionAndFluidTests
{
    [Fact]
    public void VoxelSweptCollider_LandsCleanlyOnSolidFloor()
    {
        var world = new TestCollisionWorld();
        world.FillBox(new BlockPos(-4, 63, -4), new BlockPos(4, 63, 4), isSolid: true);

        var startPos = new Vec3d(0.5, 64.25, 0.5);
        var startBox = BoundingBox.FromEntityFeet(startPos, width: 0.6, height: 1.8);

        var (resolvedBox, actualMove, onGround, collidedHoriz) = VoxelSweptCollider.MoveAndCollide(
            world,
            startBox,
            new Vec3d(0.0, -0.5, 0.0));

        resolvedBox.MinY.Should().BeApproximately(64.0, 1e-5);
        actualMove.Y.Should().BeApproximately(-0.25, 1e-5);
        onGround.Should().BeTrue();
        collidedHoriz.Should().BeFalse();
        VoxelSweptCollider.IntersectsAnySolid(world, resolvedBox).Should().BeFalse();
    }

    [Fact]
    public void KinematicsIntegrator_PreventsHighSpeedTunnelingThroughOneBlockWall()
    {
        var world = new TestCollisionWorld();
        world.FillBox(new BlockPos(-10, 63, -10), new BlockPos(10, 63, 10), isSolid: true);

        // Build a 1-block-thick vertical wall at X = 5.
        for (var y = 64; y <= 70; y++)
        {
            for (var z = -3; z <= 3; z++)
            {
                world.SetSolid(new BlockPos(5, y, z), true);
            }
        }

        // 100 blocks/s * 0.05 s = 5.0 blocks in a single tick — much thicker than the 1-block wall.
        var startPos = new Vec3d(2.0, 64.0, 0.5);
        var velocity = new Vec3d(100.0, 0.0, 4.0);

        var result = KinematicsIntegrator.StepEntity(
            world,
            startPos,
            velocity,
            width: 0.6,
            height: 1.8,
            deltaSeconds: 0.05);

        // Half-width is 0.3, so entity right face stops at X = 5.0 -> center X <= 4.7.
        result.Position.X.Should().BeLessThanOrEqualTo(4.7001);
        result.Position.X.Should().BeGreaterThan(4.69);
        result.Position.Z.Should().BeApproximately(0.7, 1e-5);
        result.CollidedHorizontally.Should().BeTrue();
        result.Velocity.X.Should().BeApproximately(0.0, 1e-9);
    }

    [Fact]
    public void KinematicsIntegrator_AppliesFluidBuoyancyAndHorizontalCurrentPush()
    {
        var world = new TestCollisionWorld();
        world.FillBox(new BlockPos(-4, 60, -4), new BlockPos(4, 60, 4), isSolid: true);

        // Sloped water channel flowing along +X: level 1 -> level 2 -> level 3.
        world.SetFluid(new BlockPos(-1, 61, 0), 1);
        world.SetFluid(new BlockPos(0, 61, 0), 2);
        world.SetFluid(new BlockPos(1, 61, 0), 3);

        var step = KinematicsIntegrator.StepEntity(
            world,
            new Vec3d(0.5, 61.1, 0.5),
            Vec3d.Zero,
            width: 0.6,
            height: 1.8,
            deltaSeconds: 0.05);

        step.InFluid.Should().BeTrue();
        step.Velocity.Y.Should().BeGreaterThan(0.0);
        step.Velocity.X.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void KinematicsIntegrator_FreezesEntityInUnloadedColumn()
    {
        var world = new TestCollisionWorld { LoadedByDefault = false };
        var initialPos = new Vec3d(100.5, 80.0, -50.5);

        var step = KinematicsIntegrator.StepEntity(
            world,
            initialPos,
            new Vec3d(3.0, -12.0, 1.0),
            width: 0.6,
            height: 1.8,
            deltaSeconds: 0.05);

        step.Position.Should().Be(initialPos);
        step.Velocity.Should().Be(Vec3d.Zero);
    }

    [Fact]
    public void FluidSimulator_SpreadsHorizontallyAndFormsInfiniteWaterSource()
    {
        var world = new TestCollisionWorld();
        world.FillBox(new BlockPos(-10, 63, -10), new BlockPos(10, 63, 10), isSolid: true);

        // Two horizontal source blocks surrounding (0, 64, 0) over solid floor -> infinite source rule!
        world.SetFluid(new BlockPos(-1, 64, 0), 0);
        world.SetFluid(new BlockPos(1, 64, 0), 0);

        Span<BlockPos> active =
        [
            new BlockPos(-1, 64, 0),
            new BlockPos(0, 64, 0),
            new BlockPos(1, 64, 0),
        ];

        var changes = FluidSimulator.EvaluateStep(world, active);

        changes.Should().Contain(new FluidChange(new BlockPos(0, 64, 0), NewLevel: 0));
        changes.Should().Contain(new FluidChange(new BlockPos(2, 64, 0), NewLevel: 1));
    }

    [Fact]
    public void FluidSimulator_CreatesFallingStreamOverLedgeAndDecaysOrphanedFlow()
    {
        var world = new TestCollisionWorld();
        world.SetSolid(new BlockPos(0, 63, 0), true);
        world.SetFluid(new BlockPos(0, 64, 0), 0);

        // Cell (1, 64, 0) has air underneath -> spread to (1, 64, 0) at level 1, then next step falls to (1, 63, 0) at level 8.
        var step1 = FluidSimulator.EvaluateStep(world, [new BlockPos(0, 64, 0)]);
        step1.Should().Contain(new FluidChange(new BlockPos(1, 64, 0), NewLevel: 1));

        world.SetFluid(new BlockPos(1, 64, 0), 1);
        var step2 = FluidSimulator.EvaluateStep(world, [new BlockPos(1, 64, 0)]);
        step2.Should().Contain(new FluidChange(new BlockPos(1, 63, 0), NewLevel: FluidSimulator.FallingLevel));

        // Remove source -> orphaned level-1 cell dries up (RemoveFluidSentinel).
        world.SetFluid(new BlockPos(0, 64, 0), -1);
        var step3 = FluidSimulator.EvaluateStep(world, [new BlockPos(1, 64, 0)]);
        step3.Should().Contain(new FluidChange(new BlockPos(1, 64, 0), FluidChange.RemoveFluidSentinel));
    }

    private sealed class TestCollisionWorld : IVoxelCollisionReader
    {
        private readonly HashSet<BlockPos> _solids = [];
        private readonly Dictionary<BlockPos, int> _fluids = [];

        public bool LoadedByDefault { get; set; } = true;

        public void SetSolid(BlockPos pos, bool isSolid)
        {
            if (isSolid)
            {
                _ = _solids.Add(pos);
            }
            else
            {
                _ = _solids.Remove(pos);
            }
        }

        public void FillBox(BlockPos min, BlockPos max, bool isSolid)
        {
            for (var x = min.X; x <= max.X; x++)
            {
                for (var y = min.Y; y <= max.Y; y++)
                {
                    for (var z = min.Z; z <= max.Z; z++)
                    {
                        SetSolid(new BlockPos(x, y, z), isSolid);
                    }
                }
            }
        }

        public void SetFluid(BlockPos pos, int level)
        {
            if (level < 0)
            {
                _ = _fluids.Remove(pos);
            }
            else
            {
                _fluids[pos] = level;
            }
        }

        public bool IsColumnLoaded(BlockPos pos) => LoadedByDefault;

        public bool IsSolidBlock(BlockPos pos) => _solids.Contains(pos);

        public int GetFluidLevelOrNegative(BlockPos pos) => _fluids.GetValueOrDefault(pos, -1);
    }
}
