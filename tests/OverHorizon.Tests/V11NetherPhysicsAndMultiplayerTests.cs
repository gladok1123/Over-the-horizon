using System.Buffers;
using FluentAssertions;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay.Mobs;
using OverHorizon.Gameplay.Survival;
using OverHorizon.Networking;
using OverHorizon.Networking.Codec;
using OverHorizon.Networking.Framing;
using OverHorizon.Networking.Packets;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Physics;
using OverHorizon.World.Generation;
using OverHorizon.World.Storage;
using Xunit;

namespace OverHorizon.Tests;

/// <summary>
/// Covers the <c>v0.11.0-alpha</c> Nether dimension (portal rules, terrain, mobs, collision shapes),
/// the collision sub-stepping polish, the pooled fluid stepper, and the multiplayer dimension packet.
/// </summary>
public sealed class V11NetherPhysicsAndMultiplayerTests
{
    [Fact]
    public void DimensionRules_ScalesCoordinatesAndClampsNetherBuildHeight()
    {
        DimensionRules.GetTravelScale(DimensionId.Overworld, DimensionId.Nether).Should().BeApproximately(1.0 / 8.0, 1e-9);
        DimensionRules.GetTravelScale(DimensionId.Nether, DimensionId.Overworld).Should().BeApproximately(8.0, 1e-9);
        DimensionRules.GetTravelScale(DimensionId.Overworld, DimensionId.Overworld).Should().BeApproximately(1.0, 1e-9);

        DimensionRules.Opposite(DimensionId.Nether).Should().Be(DimensionId.Overworld);
        DimensionRules.Opposite(DimensionId.Overworld).Should().Be(DimensionId.Nether);

        DimensionRules.HasSkyLight(DimensionId.Overworld).Should().BeTrue();
        DimensionRules.HasSkyLight(DimensionId.Nether).Should().BeFalse();
        DimensionRules.GetAmbientLightFloor(DimensionId.Nether).Should().BeGreaterThan(DimensionRules.GetAmbientLightFloor(DimensionId.Overworld));

        DimensionRules.ClampWorldY(DimensionId.Nether, 500).Should().Be(DimensionRules.NetherCeilingY - 3);
        DimensionRules.ClampWorldY(DimensionId.Nether, -500).Should().Be(DimensionRules.NetherFloorY + 2);
    }

    [Fact]
    public void NetherPortalRules_ScalesTravelAndRoundTripsThroughTheNether()
    {
        var registries = CoreRegistries.CreateDefault();
        var origin = new BlockPos(800, 70, -1600);

        var inNether = NetherPortalRules.ScaleTravel(origin, DimensionId.Overworld, DimensionId.Nether);
        inNether.X.Should().Be(100);
        inNether.Z.Should().Be(-200);
        inNether.Y.Should().Be(70);

        var backHome = NetherPortalRules.ScaleTravel(inNether, DimensionId.Nether, DimensionId.Overworld);
        backHome.X.Should().Be(origin.X);
        backHome.Z.Should().Be(origin.Z);

        var portalState = NetherPortalRules.WithAxis(registries.Blocks, "x");
        NetherPortalRules.IsPortal(registries.Blocks, portalState).Should().BeTrue();
        NetherPortalRules.IsPortal(registries.Blocks, BlockState.Air).Should().BeFalse();
        NetherPortalRules.IsPortal(registries.Blocks, registries.Blocks.GetDefaultState(BuiltInBlocks.Netherrack)).Should().BeFalse();
    }

    [Fact]
    public void NetherPortalRules_LightsAValidObsidianFrameAndFillsItsInterior()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var world = new TestPortalWorld();

        // 4x5 obsidian ring (x = 0..3, y = 0..4) with an empty 2x3 interior at x = 1..2, y = 1..3.
        for (var y = 0; y <= 4; y++)
        {
            for (var x = 0; x <= 3; x++)
            {
                var isFrame = y == 0 || y == 4 || x == 0 || x == 3;
                world.Set(new BlockPos(x, y, 0), isFrame ? blocks.GetDefaultState(BuiltInBlocks.Obsidian) : BlockState.Air);
            }
        }

        var lit = NetherPortalRules.TryActivateFrame(
            blocks,
            world.Get,
            world.Set,
            new BlockPos(1, 2, 0),
            out var info);

        lit.Should().BeTrue("a 2x3 obsidian ring is a valid Nether Portal frame");
        info.MinInterior.Should().Be(new BlockPos(1, 1, 0));
        info.MaxInterior.Should().Be(new BlockPos(2, 3, 0));

        for (var y = 1; y <= 3; y++)
        {
            for (var x = 1; x <= 2; x++)
            {
                NetherPortalRules.IsPortal(blocks, world.Get(new BlockPos(x, y, 0)))
                    .Should().BeTrue($"interior cell ({x}, {y}) must be filled with lit portal blocks");
            }
        }
    }

    [Fact]
    public void NetherPortalRules_RejectsAnUnframedOpening()
    {
        var registries = CoreRegistries.CreateDefault();
        var world = new TestPortalWorld();

        var lit = NetherPortalRules.TryActivateFrame(
            registries.Blocks,
            world.Get,
            world.Set,
            new BlockPos(0, 64, 0),
            out _);

        lit.Should().BeFalse("a completely empty region has no obsidian frame to light");
    }

    [Fact]
    public void NetherPortalRules_ResolvesASafeArrivalSiteOverLava()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var world = new TestPortalWorld
        {
            IsLoaded = _ => true,
        };

        // A solid shelf at y=80 surrounded by air; every scan target below it is empty (unloaded void).
        for (var x = -12; x <= 12; x++)
        {
            for (var z = -12; z <= 12; z++)
            {
                world.Set(new BlockPos(x, 80, z), blocks.GetDefaultState(BuiltInBlocks.Netherrack));
            }
        }

        var landing = NetherPortalRules.ResolveArrivalSite(
            blocks,
            world.Get,
            world.Set,
            _ => true,
            new BlockPos(0, 84, 0),
            DimensionId.Nether);

        blocks.GetStateMetadata(world.Get(new BlockPos(landing.X, landing.Y - 1, landing.Z))).IsSolid
            .Should().BeTrue("the traveller must land on solid ground");
        blocks.GetStateMetadata(world.Get(landing)).BlockKey.Should().Be(BuiltInBlocks.Air);
        blocks.GetStateMetadata(world.Get(new BlockPos(landing.X, landing.Y + 1, landing.Z))).BlockKey.Should().Be(BuiltInBlocks.Air);
    }

    [Fact]
    public void NetherGenerator_CarvesBedrockFloorAndCeilingWithALavaSea()
    {
        var registries = CoreRegistries.CreateDefault();
        var generator = new NetherGenerator(12345L, registries);
        var chunk = new ChunkColumn(new ChunkPos(0, 0));
        generator.GenerateChunk(chunk);

        registries.Blocks.GetStateMetadata(chunk.GetBlockState(0, 0, 0)).BlockKey
            .Should().Be(BuiltInBlocks.Bedrock, "the Nether floor is bedrock");
        registries.Blocks.GetStateMetadata(chunk.GetBlockState(0, 127, 0)).BlockKey
            .Should().Be(BuiltInBlocks.Bedrock, "the Nether roof is bedrock");

        var sawLava = false;
        for (var x = 0; x < 16 && !sawLava; x++)
        {
            for (var z = 0; z < 16 && !sawLava; z++)
            {
                for (var y = 8; y <= 34; y++)
                {
                    var key = registries.Blocks.GetStateMetadata(chunk.GetBlockState(x, y, z)).BlockKey;
                    if (key == BuiltInBlocks.Lava)
                    {
                        sawLava = true;
                        break;
                    }
                }
            }
        }

        sawLava.Should().BeTrue("the generated Nether must contain its lava sea");
    }

    [Fact]
    public void NetherMobs_AreHostileFireImmuneAndHaveAuthenticHitboxes()
    {
        var piglin = new ActiveMob(1, MobType.ZombifiedPiglin, Vec3d.Zero);
        var cube = new ActiveMob(2, MobType.MagmaCube, Vec3d.Zero);
        var zombie = new ActiveMob(3, MobType.Zombie, Vec3d.Zero);

        piglin.IsHostile.Should().BeTrue();
        cube.IsHostile.Should().BeTrue();
        piglin.IsFireImmune.Should().BeTrue();
        cube.IsFireImmune.Should().BeTrue();
        cube.IsImmuneToFallDamage.Should().BeTrue();
        zombie.IsFireImmune.Should().BeFalse();
        zombie.IsImmuneToFallDamage.Should().BeFalse();

        piglin.Height.Should().BeGreaterThan(1.8);
        cube.Height.Should().BeLessThan(1.0);
        cube.Width.Should().BePositive();
        piglin.MaxHealth.Should().Be(20.0f);
    }

    [Fact]
    public void CollisionShapeTable_GivesSoulSandItsAuthenticSunkenHeight()
    {
        var registries = CoreRegistries.CreateDefault();
        var table = new BlockCollisionShapeTable(registries.Blocks);

        table.TryGetWorldBox(registries.Blocks.GetDefaultState(BuiltInBlocks.SoulSand), new BlockPos(0, 64, 0), out var soulSand)
            .Should().BeTrue();
        soulSand.MaxY.Should().BeApproximately(64.875, 1e-4);

        table.TryGetWorldBox(registries.Blocks.GetDefaultState(BuiltInBlocks.NetherPortal), new BlockPos(0, 64, 0), out _)
            .Should().BeFalse("portals are walk-through blocks with no collision box");

        table.TryGetWorldBox(registries.Blocks.GetDefaultState(BuiltInBlocks.Netherrack), new BlockPos(0, 64, 0), out var full)
            .Should().BeTrue();
        full.MaxY.Should().BeApproximately(65.0, 1e-4);
    }

    [Fact]
    public void VoxelSweptCollider_ReturnsTheBoxUnchangedForAZeroLengthSweep()
    {
        var world = new TestNetherCollisionWorld();
        world.FillBox(new BlockPos(-4, 63, -4), new BlockPos(4, 63, 4));

        var box = BoundingBox.FromEntityFeet(new Vec3d(0.5, 64.0, 0.5), width: 0.6, height: 1.8);
        var (resolved, move, onGround, hitHorizontal) = VoxelSweptCollider.MoveAndCollide(world, box, Vec3d.Zero);

        resolved.Should().Be(box);
        move.Should().Be(Vec3d.Zero);
        onGround.Should().BeFalse();
        hitHorizontal.Should().BeFalse();
    }

    [Fact]
    public void KinematicsIntegrator_SubstepsHighSpeedMovesWithoutLosingTheWall()
    {
        var world = new TestNetherCollisionWorld();
        world.FillBox(new BlockPos(-12, 63, -12), new BlockPos(12, 63, 12));

        // 1-block-thick wall at X = 5.
        for (var y = 64; y <= 72; y++)
        {
            for (var z = -4; z <= 4; z++)
            {
                world.SetSolid(new BlockPos(5, y, z));
            }
        }

        var result = KinematicsIntegrator.StepEntity(
            world,
            new Vec3d(0.0, 64.0, 0.5),
            new Vec3d(60.0, 0.0, 12.0),
            width: 0.6,
            height: 1.8,
            deltaSeconds: 0.05);

        result.Position.X.Should().BeLessThanOrEqualTo(4.7001, "the entity must never end up inside or past the wall");
        result.CollidedHorizontally.Should().BeTrue();
        result.Velocity.X.Should().BeApproximately(0.0, 1e-9);
    }

    [Fact]
    public void FluidSimulator_EvaluateStepInto_ReusesTheCallerBufferAndStaysDeterministic()
    {
        var world = new TestNetherCollisionWorld();
        world.FillBox(new BlockPos(-4, 60, -4), new BlockPos(4, 60, 4));
        world.SetFluid(new BlockPos(0, 62, 0), FluidSimulator.SourceLevel);

        var buffer = new List<FluidChange>();
        var first = FluidSimulator.EvaluateStepInto(world, [new BlockPos(0, 62, 0)], buffer);
        var firstSnapshot = buffer.ToArray();

        buffer.Clear();
        var second = FluidSimulator.EvaluateStepInto(world, [new BlockPos(0, 62, 0)], new List<FluidChange>());

        buffer.Capacity.Should().BeGreaterThan(0);
        first.Should().BeGreaterThan(0);
        second.Should().Be(firstSnapshot.Length);
    }

    [Fact]
    public void PlayerDimensionPacket_RoundTripsThroughThePlayCodec()
    {
        var codec = new JavaProtocolCodec();
        var packet = new PlayerDimensionPacket(DimensionId.Nether, new Vec3d(12.5, 40.0, -8.25), YawDegrees: 91.5f, PitchDegrees: -12.25f);

        var payload = new byte[256];
        var writer = new ArrayBufferWriter<byte>();
        codec.EncodeClientbound(ConnectionState.Play, packet, writer);
        var decoded = codec.DecodeClientbound(ConnectionState.Play, writer.WrittenSpan.ToArray());

        decoded.Should().BeOfType<PlayerDimensionPacket>();
        var round = (PlayerDimensionPacket)decoded;
        round.Dimension.Should().Be(DimensionId.Nether);
        round.Position.X.Should().BeApproximately(12.5, 1e-6);
        round.Position.Y.Should().BeApproximately(40.0, 1e-6);
        round.Position.Z.Should().BeApproximately(-8.25, 1e-6);
        round.YawDegrees.Should().BeApproximately(91.5f, 1e-4f);
        round.PitchDegrees.Should().BeApproximately(-12.25f, 1e-4f);
        _ = payload;
    }

    private sealed class TestPortalWorld
    {
        private readonly Dictionary<BlockPos, BlockState> _blocks = [];

        public Func<BlockPos, bool> IsLoaded { get; set; } = _ => true;

        public BlockState Get(BlockPos pos) => _blocks.GetValueOrDefault(pos, BlockState.Air);

        public void Set(BlockPos pos, BlockState state) => _blocks[pos] = state;
    }

    private sealed class TestNetherCollisionWorld : IVoxelCollisionReader
    {
        private readonly HashSet<BlockPos> _solids = [];
        private readonly Dictionary<BlockPos, int> _fluids = [];

        public void SetSolid(BlockPos pos) => _ = _solids.Add(pos);

        public void FillBox(BlockPos min, BlockPos max)
        {
            for (var x = min.X; x <= max.X; x++)
            {
                for (var y = min.Y; y <= max.Y; y++)
                {
                    for (var z = min.Z; z <= max.Z; z++)
                    {
                        SetSolid(new BlockPos(x, y, z));
                    }
                }
            }
        }

        public void SetFluid(BlockPos pos, int level) => _fluids[pos] = level;

        public bool IsColumnLoaded(BlockPos pos) => true;

        public bool IsSolidBlock(BlockPos pos) => _solids.Contains(pos);

        public int GetFluidLevelOrNegative(BlockPos pos) => _fluids.GetValueOrDefault(pos, -1);

        public bool TryGetCollisionBox(BlockPos pos, out BoundingBox box)
        {
            if (_solids.Contains(pos))
            {
                box = new BoundingBox(pos.X, pos.Y, pos.Z, pos.X + 1.0, pos.Y + 1.0, pos.Z + 1.0);
                return true;
            }

            box = default;
            return false;
        }
    }
}
