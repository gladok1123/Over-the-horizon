using FluentAssertions;
using OverHorizon.Audio;
using OverHorizon.Client;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay.Survival;
using OverHorizon.World.ChunkManagement;
using OverHorizon.World.Generation;
using OverHorizon.World.Storage;
using Xunit;

namespace OverHorizon.Tests;

/// <summary>
/// v0.13 regression coverage: Nether Fortress visibility, the much roomier Nether caverns, translucent-pass
/// draw order, real Nether ambience clips, portal plane orientation, and 1:8 portal travel scaling.
/// </summary>
public sealed class V13NetherSpaceTravelAndEffectsTests
{
    [Fact]
    public void NetherGenerator_OpensAGreatCavernAboveTheLavaSea()
    {
        var registries = CoreRegistries.CreateDefault();
        var generator = new NetherGenerator(20261112L, registries);

        var airInCavern = 0;
        var solidInCavern = 0;
        var solidInShore = 0;
        var cellsInShore = 0;

        for (var cx = -2; cx <= 2; cx++)
        {
            var chunk = new ChunkColumn(new ChunkPos(cx, 0));
            generator.GenerateChunk(chunk);

            for (var lz = 0; lz < 16; lz += 2)
            {
                for (var lx = 0; lx < 16; lx += 2)
                {
                    for (var y = NetherGenerator.ShoreRockTopY + 1; y <= NetherGenerator.GreatCavernTopY; y++)
                    {
                        if (chunk.GetBlockState(lx, y, lz).IsAir)
                        {
                            airInCavern++;
                        }
                        else
                        {
                            solidInCavern++;
                        }
                    }

                    for (var y = NetherGenerator.LavaSeaY + 1; y <= NetherGenerator.ShoreRockTopY; y++)
                    {
                        cellsInShore++;
                        if (!chunk.GetBlockState(lx, y, lz).IsAir)
                        {
                            solidInShore++;
                        }
                    }
                }
            }
        }

        var openFraction = (double)airInCavern / (airInCavern + solidInCavern);
        openFraction.Should().BeGreaterThan(0.62, "the great cavern must be mostly open space, not solid netherrack");

        var shoreFraction = (double)solidInShore / cellsInShore;
        shoreFraction.Should().BeGreaterThan(0.45, "the shore rock keeps a walkable floor above the lava sea");

        NetherGenerator.OpennessBias(NetherGenerator.GreatCavernTopY).Should().BeGreaterThan(0.0);
        NetherGenerator.OpennessBias(NetherGenerator.CeilingRockY + 4).Should().BeLessThan(0.0);
    }

    [Fact]
    public void NetherFortressGenerator_HangsItsKeepInsideTheGreatCavernWithOpenAirAroundIt()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var fortress = new NetherFortressGenerator(20261112L, blocks);
        fortress.GetFortressOrigin(0, 0, out var hubX, out var hubZ, out var baseY);

        baseY.Should().BeGreaterThan(NetherGenerator.ShoreRockTopY, "the keep hangs in the great cavern");
        (baseY + 12).Should().BeLessThan(NetherGenerator.GreatCavernTopY, "the whole keep fits under the cavern roof");

        var generator = new NetherGenerator(20261112L, registries);
        var chunk = new ChunkColumn(new ChunkPos(hubX >> 4, hubZ >> 4));
        generator.GenerateChunk(chunk);

        var lx = hubX - chunk.Position.MinBlockX;
        var lz = hubZ - chunk.Position.MinBlockZ;
        (uint)lx.Should().BeLessThan(16u);
        (uint)lz.Should().BeLessThan(16u);

        // The keep crown sits in open air, so the fortress is visible instead of entombed in netherrack.
        var openAbove = 0;
        for (var y = baseY + 12; y <= NetherGenerator.GreatCavernTopY; y++)
        {
            if (chunk.GetBlockState(lx, y, lz).IsAir)
            {
                openAbove++;
            }
        }

        openAbove.Should().BeGreaterThan(2, "the fortress cavern is hollowed out around the keep");

        var bricks = blocks.GetDefaultState(BuiltInBlocks.NetherBricks);
        var sawBricks = false;
        for (var y = baseY; y <= baseY + 12; y++)
        {
            if (chunk.GetBlockState(lx, y, lz) == bricks)
            {
                sawBricks = true;
            }
        }

        sawBricks.Should().BeTrue("the fortress keep is still stamped into the terrain");
    }

    [Fact]
    public void NetherPortalRules_ScalesEveryNetherStepToEightOverworldSteps()
    {
        var first = new BlockPos(96, 48, 96);
        var second = new BlockPos(320, 48, -160);

        var firstOverworld = NetherPortalRules.ScaleTravel(first, DimensionId.Nether, DimensionId.Overworld);
        var secondOverworld = NetherPortalRules.ScaleTravel(second, DimensionId.Nether, DimensionId.Overworld);

        firstOverworld.X.Should().Be(first.X * DimensionRules.NetherCoordinateScale);
        firstOverworld.Z.Should().Be(first.Z * DimensionRules.NetherCoordinateScale);
        secondOverworld.X.Should().Be(second.X * DimensionRules.NetherCoordinateScale);
        secondOverworld.Z.Should().Be(second.Z * DimensionRules.NetherCoordinateScale);

        // Two exits 224 Nether blocks apart must land 1792 Overworld blocks apart, never in the same place.
        var dx = secondOverworld.X - firstOverworld.X;
        var dz = secondOverworld.Z - firstOverworld.Z;
        dx.Should().Be((second.X - first.X) * DimensionRules.NetherCoordinateScale);
        dz.Should().Be((second.Z - first.Z) * DimensionRules.NetherCoordinateScale);

        var roundTrip = NetherPortalRules.ScaleTravel(firstOverworld, DimensionId.Overworld, DimensionId.Nether);
        roundTrip.X.Should().Be(first.X);
        roundTrip.Z.Should().Be(first.Z);
    }

    [Fact]
    public void NetherPortalRules_CreateArrivalSiteFramesAWalkInPortalWithTheTravellerBesideIt()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var world = new TestNetherWorld();

        var site = NetherPortalRules.CreateArrivalSite(
            blocks,
            world.Get,
            world.Set,
            _ => true,
            new BlockPos(96, 48, 96),
            DimensionId.Nether);

        site.Dimension.Should().Be(DimensionId.Overworld);
        site.Desired.X.Should().Be(96 * DimensionRules.NetherCoordinateScale);
        site.Desired.Z.Should().Be(96 * DimensionRules.NetherCoordinateScale);

        // The traveller lands within the bounded arrival search radius of the scaled destination.
        System.Math.Abs(site.Landing.X - site.Desired.X).Should().BeLessThanOrEqualTo(8);
        System.Math.Abs(site.Landing.Z - site.Desired.Z).Should().BeLessThanOrEqualTo(8);

        NetherPortalRules.IsPortal(blocks, world.Get(site.Landing)).Should().BeTrue("the arrival portal is lit");
        NetherPortalRules.IsPortal(blocks, world.Get(site.SpawnPosition.ToBlockPos())).Should()
            .BeFalse("the traveller must not be standing inside the portal they just arrived at");

        // Both faces of the gateway are walkable: three blocks of headroom over a solid floor.
        for (var step = 1; step <= 2; step++)
        {
            var front = new BlockPos(site.Landing.X, site.Landing.Y, site.Landing.Z + step);
            world.Get(front).IsAir.Should().BeTrue($"the walkway {step} block(s) in front of the portal is open");
            world.Get(new BlockPos(front.X, front.Y - 1, front.Z)).IsAir.Should().BeFalse("the walkway has a floor");
        }

        var behind = new BlockPos(site.Landing.X, site.Landing.Y, site.Landing.Z - 1);
        world.Get(behind).IsAir.Should().BeTrue("the far face of the gateway is open too");
    }

    [Fact]
    public void NetherPortalRules_BuildFramedPortalNeverSealsTheOpeningInsideObsidian()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var world = new TestNetherWorld();

        NetherPortalRules.BuildFramedPortal(blocks, world.Set, new BlockPos(0, 70, 0), "z");

        var obsidian = blocks.GetDefaultState(BuiltInBlocks.Obsidian);
        blocks.GetStateMetadata(world.Get(new BlockPos(-1, 71, 0))).BlockKey.Should().Be(BuiltInBlocks.Obsidian);
        blocks.GetStateMetadata(world.Get(new BlockPos(2, 71, 0))).BlockKey.Should().Be(BuiltInBlocks.Obsidian);
        blocks.GetStateMetadata(world.Get(new BlockPos(0, 73, 0))).BlockKey.Should().Be(BuiltInBlocks.Obsidian);

        NetherPortalRules.IsPortal(blocks, world.Get(new BlockPos(0, 71, 0))).Should().BeTrue();
        NetherPortalRules.IsPortal(blocks, world.Get(new BlockPos(1, 71, 0))).Should().BeTrue();

        obsidian.IsAir.Should().BeFalse();
        world.Get(new BlockPos(0, 71, 1)).IsAir.Should().BeTrue("the portal can be walked into from the front");
        world.Get(new BlockPos(0, 71, -1)).IsAir.Should().BeTrue("the portal can be walked into from behind");
    }

    [Fact]
    public void ShapedBlockMesher_OrientsThePortalPlanePerpendicularToItsAxis()
    {
        var text = ReadSourceFile("src/OverHorizon.Rendering/Meshing/ShapedBlockMesher.RedstoneAndBed.cs");

        var start = text.IndexOf("private static void EmitPortalPlane(", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1, "the portal plane emitter must exist");
        var body = text[start..text.IndexOf("private static void EmitDoubleSidedQuad(", start, StringComparison.Ordinal)];

        // The axis property names the axis the plane is PERPENDICULAR to: "x" pins X and spans Z, "z" pins Z and spans X.
        var pinsX = body.IndexOf("string.Equals(axis, \"x\", StringComparison.Ordinal)", StringComparison.Ordinal);
        pinsX.Should().BeGreaterThan(-1, "the \"x\" axis branch must be selected first");
        pinsX.Should().BeLessThan(body.IndexOf("o.X + 0.5f", StringComparison.Ordinal), "the \"x\" branch pins the plane to X");
        body.IndexOf("o.X + 0.5f", StringComparison.Ordinal).Should()
            .BeLessThan(body.IndexOf("o.Z + 0.5f", StringComparison.Ordinal), "the fallback branch pins the plane to Z");
    }

    [Fact]
    public void SoundBank_ShipsRealNetherAmbienceClips()
    {
        var bank = ProceduralSoundBank.CreateDefaultBank();

        bank.Should().ContainKey(SoundCue.NetherAmbience);
        bank.Should().ContainKey(SoundCue.NetherMood);
        bank.Should().ContainKey(SoundCue.NetherWhisper);
        bank.Should().ContainKey(SoundCue.NetherGroan);
        bank.Should().ContainKey(SoundCue.NetherMusic);
        bank.Should().ContainKey(SoundCue.GhastMoan);
        bank.Should().ContainKey(SoundCue.GhastScream);
        bank.Should().ContainKey(SoundCue.BlazeBreathe);

        bank[SoundCue.NetherAmbience].Samples.Length.Should().BeGreaterThan(1000);
        bank[SoundCue.NetherMusic].Samples.Length.Should().BeGreaterThan(1000);

        var manifest = Minecraft23w13aAssetSyncer.AssetManifest;
        manifest.Should().Contain("sounds/ambient/nether/nether_wastes/ambience.ogg");
        manifest.Should().Contain("sounds/ambient/nether/soulsand_valley/whisper1.ogg");
        manifest.Should().Contain("sounds/music/game/nether/nether1.ogg");
        manifest.Should().Contain("sounds/mob/ghast/moan1.ogg");
        manifest.Should().Contain("sounds/mob/blaze/breathe1.ogg");
    }

    [Fact]
    public void RenderFrame_KeepsTheTranslucentPassBetweenTheWorldAndScreenOverlays()
    {
        var text = ReadSourceFile("src/OverHorizon.Rendering/Backends/SilkOpenGlRenderSubsystem.cs");
        var worldPass = text.IndexOf("RenderWorldOverlays(", StringComparison.Ordinal);
        var transPass = text.IndexOf("DrawTranslucentPass(", StringComparison.Ordinal);
        var screenPass = text.IndexOf("RenderScreenOverlays(", StringComparison.Ordinal);

        worldPass.Should().BeGreaterThan(-1);
        transPass.Should().BeGreaterThan(-1);
        screenPass.Should().BeGreaterThan(-1);

        worldPass.Should().BeLessThan(transPass, "clouds and mobs are drawn before water and portals");
        transPass.Should().BeLessThan(screenPass, "the held viewmodel and HUD are drawn after translucent terrain");
    }

    private static string ReadSourceFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OverHorizon.sln")))
            {
                var full = Path.Combine(dir.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
                File.Exists(full).Should().BeTrue($"source file '{relativePath}' must exist");
                return File.ReadAllText(full);
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate OverHorizon.sln above test base directory.");
    }

    private sealed class TestNetherWorld
    {
        private readonly Dictionary<long, BlockState> _blocks = new();

        public BlockState Get(BlockPos pos) =>
            _blocks.TryGetValue(Pack(pos), out var state) ? state : BlockState.Air;

        public void Set(BlockPos pos, BlockState state) => _blocks[Pack(pos)] = state;

        private static long Pack(BlockPos pos) =>
            (((long)(uint)pos.X & 0xFFFF) << 32) | (((long)(uint)pos.Y & 0xFF) << 16) | ((long)(uint)pos.Z & 0xFFFF);
    }
}
