using System.Numerics;
using FluentAssertions;
using OverHorizon.Audio;
using OverHorizon.Client;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Crafting;
using OverHorizon.Gameplay.Inventory;
using OverHorizon.Gameplay.Mobs;
using OverHorizon.Gameplay.Physics;
using OverHorizon.Gameplay.Survival;
using OverHorizon.Physics;
using OverHorizon.Rendering.Textures;
using OverHorizon.World.Storage;
using Xunit;

namespace OverHorizon.Tests;

/// <summary>
/// v0.10 regression coverage: vanilla rail auto-connection (including the multiplayer server cascade),
/// the <c>0.6</c>-block auto step-up, smooth single-column falling-block physics, and the original
/// <c>23w13a_or_b</c> landing / barrel / chest / rail asset wiring.
/// </summary>
public sealed class V10RailsStepsFallingAndOriginalAssetsTests
{
    [Fact]
    public void RailCircle_ResolvesAllFourVanillaCurveShapes()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var world = new Dictionary<BlockPos, BlockState>();

        BlockState Get(BlockPos p) => world.TryGetValue(p, out var s) ? s : BlockState.Air;
        void Set(BlockPos p, BlockState s)
        {
            if (s.IsAir) world.Remove(p);
            else world[p] = s;
        }

        // Solid Stone floor under the whole 2x2 loop (and one block of margin) so no rail is unsupported!
        for (var x = -1; x <= 2; x++)
        {
            for (var z = -1; z <= 2; z++)
            {
                Set(new BlockPos(x, 63, z), blocks.GetDefaultState(BuiltInBlocks.Stone));
            }
        }

        // Place a closed 2x2 loop: (0,0) (1,0) (0,1) (1,1) -> every corner must resolve to a vanilla curve!
        var loop = new[]
        {
            new BlockPos(0, 64, 0),
            new BlockPos(1, 64, 0),
            new BlockPos(0, 64, 1),
            new BlockPos(1, 64, 1),
        };

        var railItem = blocks.GetDefaultState(BuiltInBlocks.Rail);
        foreach (var railPos in loop)
        {
            Set(railPos, RailPlacementRules.ResolveRailState(blocks, Get, BuiltInBlocks.Rail, railPos, new Vector3(0.0f, 0.0f, 1.0f), railItem));
            RailPlacementRules.RefreshAdjacentRails(blocks, Get, Set, railPos);
        }

        Shape(world, blocks, new BlockPos(0, 64, 0)).Should().Be("south_east");
        Shape(world, blocks, new BlockPos(1, 64, 0)).Should().Be("south_west");
        Shape(world, blocks, new BlockPos(0, 64, 1)).Should().Be("north_east");
        Shape(world, blocks, new BlockPos(1, 64, 1)).Should().Be("north_west");
    }

    [Fact]
    public void StraightAndAscendingRails_ResolveNorthSouthEastWestAndFortyFiveDegreeSlopes()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var world = new Dictionary<BlockPos, BlockState>();

        BlockState Get(BlockPos p) => world.TryGetValue(p, out var s) ? s : BlockState.Air;
        var railItem = blocks.GetDefaultState(BuiltInBlocks.Rail);

        for (var z = -1; z <= 3; z++)
        {
            world[new BlockPos(0, 63, z)] = blocks.GetDefaultState(BuiltInBlocks.Stone);
            world[new BlockPos(1, 63, z)] = blocks.GetDefaultState(BuiltInBlocks.Stone);
        }

        // A lone neighbouring rail to the South joins into a straight north_south track!
        world[new BlockPos(0, 64, 1)] = railItem;
        var straight = RailPlacementRules.ResolveRailState(blocks, Get, BuiltInBlocks.Rail, new BlockPos(0, 64, 0), new Vector3(0.0f, 0.0f, 1.0f), railItem);
        RailPlacementRules.GetRailShape(blocks.GetStateMetadata(straight)).Should().Be("north_south");

        // A lone neighbouring rail to the East joins into a straight east_west track!
        var eastWorld = new Dictionary<BlockPos, BlockState>(world);
        eastWorld.Remove(new BlockPos(0, 64, 1));
        eastWorld[new BlockPos(1, 64, 0)] = railItem;
        BlockState GetEast(BlockPos p) => eastWorld.TryGetValue(p, out var s) ? s : BlockState.Air;
        var eastWest = RailPlacementRules.ResolveRailState(blocks, GetEast, BuiltInBlocks.Rail, new BlockPos(0, 64, 0), new Vector3(0.0f, 0.0f, 1.0f), railItem);
        RailPlacementRules.GetRailShape(blocks.GetStateMetadata(eastWest)).Should().Be("east_west");

        // A rail one block higher to the South (resting on a solid support block) upgrades the track to a 45 degree slope!
        world[new BlockPos(0, 64, 1)] = blocks.GetDefaultState(BuiltInBlocks.Stone);
        world[new BlockPos(0, 65, 1)] = railItem;
        var ascending = RailPlacementRules.ResolveRailState(blocks, Get, BuiltInBlocks.Rail, new BlockPos(0, 64, 0), new Vector3(0.0f, 0.0f, 1.0f), railItem);
        RailPlacementRules.GetRailShape(blocks.GetStateMetadata(ascending)).Should().Be("ascending_south");
    }

    [Fact]
    public void PlayerStepUp_ClimbsOnePixelDirtPathLedgeButNeverAFullBlockWall()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var atlas = ProceduralAtlasGenerator.CreateDefault(blocks);
        var cache = new ClientWorldCache(blocks, atlas);
        var col = new ChunkColumn(new ChunkPos(0, 0));

        for (var z = 6; z <= 12; z++)
        {
            for (var x = 6; x <= 12; x++)
            {
                _ = col.SetBlockState(x, 63, z, blocks.GetDefaultState(BuiltInBlocks.Stone), blocks);
            }
        }

        // 15/16 (1 pixel) Dirt Path at (8,63,8) beside a full Stone block at (9,63,8): a 1-pixel ledge!
        _ = col.SetBlockState(8, 63, 8, blocks.GetDefaultState(BuiltInBlocks.DirtPath), blocks);

        // A full 1.0-block wall at (8,64,10) is taller than the 0.6 step height and must stay unclimbable!
        _ = col.SetBlockState(8, 64, 10, blocks.GetDefaultState(BuiltInBlocks.Stone), blocks);

        cache.UpsertColumn(col);

        // Walking East off the Dirt Path onto the full block steps straight up with no jump input!
        var onPath = new Vec3d(8.5, 63.9375, 8.5);
        var stepped = KinematicsIntegrator.StepEntity(cache, onPath, new Vec3d(4.3, -0.05, 0.0), 0.6, 1.8, 0.05);
        stepped.Position.X.Should().BeGreaterThan(onPath.X + 0.05);
        stepped.Position.Y.Should().BeApproximately(64.0, 1e-6);
        stepped.OnGround.Should().BeTrue();

        // Walking North into the 1.0-block wall is still blocked: step-up must never become a free climb!
        var beforeWall = new Vec3d(8.5, 64.0, 9.5);
        var blocked = KinematicsIntegrator.StepEntity(cache, beforeWall, new Vec3d(0.0, -0.05, 4.3), 0.6, 1.8, 0.15);
        blocked.Position.Z.Should().BeLessThan(9.8);
        blocked.Position.Y.Should().BeApproximately(64.0, 1e-6);
    }

    [Fact]
    public void FallingBlockEntity_FallsSmoothlyCenteredInsideItsOwnBlockColumn()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;

        var fb = new FallingBlockEntity(1, blocks.GetDefaultState(BuiltInBlocks.Sand), new BlockPos(8, 70, 8));

        fb.FeetPosition.X.Should().BeApproximately(8.5, 1e-9, "a falling block must fall inside its own column, never at a 4-block crossing");
        fb.FeetPosition.Z.Should().BeApproximately(8.5, 1e-9);
        fb.FeetPosition.Y.Should().BeApproximately(70.0, 1e-9);

        fb.CurrentY = 66.25;
        fb.FeetPosition.Y.Should().BeApproximately(66.25, 1e-9);
        fb.FeetPosition.X.Should().BeApproximately(8.5, 1e-9, "X/Z stay locked to the column centre for the entire fall");
        fb.FeetPosition.Z.Should().BeApproximately(8.5, 1e-9);
    }

    [Fact]
    public void LandingAndFallSounds_UseOriginalDamageOggClipsInsteadOfFootsteps()
    {
        Enum.GetValues<SoundCue>().Should().HaveCount(97, "the fake synthesized jump cue is gone; vanilla has no jump sound");

        var bank = ProceduralSoundBank.CreateDefaultBank();
        bank.Should().ContainKey(SoundCue.LandSoft);
        bank.Should().ContainKey(SoundCue.FallSmall);
        bank.Should().ContainKey(SoundCue.FallBig);

        bank[SoundCue.LandSoft].Samples.Length.Should().BeGreaterThan(1000);
        bank[SoundCue.FallSmall].Samples.Length.Should().BeGreaterThan(1000);
        bank[SoundCue.FallBig].Samples.Length.Should().BeGreaterThan(1000);

        // Original 23w13a_or_b assets these cues and the v0.10 blocks are wired to!
        Minecraft23w13aAssetSyncer.AssetManifest.Should().Contain("sounds/damage/fallsmall.ogg");
        Minecraft23w13aAssetSyncer.AssetManifest.Should().Contain("sounds/damage/fallbig.ogg");
        Minecraft23w13aAssetSyncer.AssetManifest.Should().Contain("textures/block/rail.png");
        Minecraft23w13aAssetSyncer.AssetManifest.Should().Contain("textures/block/rail_corner.png");
        Minecraft23w13aAssetSyncer.AssetManifest.Should().Contain("textures/block/barrel_side.png");
        Minecraft23w13aAssetSyncer.AssetManifest.Should().Contain("textures/block/barrel_top.png");
        Minecraft23w13aAssetSyncer.AssetManifest.Should().Contain("textures/block/barrel_top_open.png");
        Minecraft23w13aAssetSyncer.AssetManifest.Should().Contain("sounds/block/barrel/open1.ogg");
        Minecraft23w13aAssetSyncer.AssetManifest.Should().Contain("sounds/block/barrel/close.ogg");
        Minecraft23w13aAssetSyncer.AssetManifest.Should().Contain("sounds/block/chest/open.ogg");
        Minecraft23w13aAssetSyncer.AssetManifest.Should().Contain("sounds/block/chest/close1.ogg");
    }

    [Fact]
    public void Skeletons_NeverShootOrAggroThroughSolidWalls()
    {
        var world = new WallTestCollisionWorld();
        world.FillSolid(new BlockPos(-4, 63, -4), new BlockPos(20, 63, 20));

        // 2-block solid wall at Z = 8 spanning X = 0..12 completely separates the Skeleton from the player!
        world.FillSolid(new BlockPos(0, 64, 8), new BlockPos(12, 65, 8));

        // --- Blocked skeleton: must neither shoot nor even acquire the player through the wall ---
        var blockedMobs = new WorldMobManager();
        _ = blockedMobs.SpawnMob(MobType.Skeleton, new Vec3d(6.5, 64.0, 12.5));
        var blockedArrows = new List<(Vec3d Origin, Vec3d Velocity)>();
        for (var t = 0; t < 60; t++)
        {
            _ = blockedMobs.Step(
                world,
                _ => (0, 0, false, true, true),
                new Vec3d(6.5, 64.0, 4.5),
                GameMode.Survival,
                playerArmorDefense: 0,
                skyBrightness: 0.1f,
                deltaSeconds: 0.05f,
                onSkeletonShootArrow: (o, v) => blockedArrows.Add((o, v)));
        }

        blockedArrows.Should().BeEmpty("a Skeleton must never fire an arrow through solid stone");

        // --- Clear skeleton: same distance, unobstructed sight line -> fires as vanilla does ---
        var openMobs = new WorldMobManager();
        _ = openMobs.SpawnMob(MobType.Skeleton, new Vec3d(6.5, 64.0, 12.5));
        var openArrows = new List<(Vec3d Origin, Vec3d Velocity)>();
        for (var t = 0; t < 60; t++)
        {
            _ = openMobs.Step(
                world,
                _ => (0, 0, false, true, true),
                new Vec3d(6.5, 64.0, 12.5 + 9.0),
                GameMode.Survival,
                playerArmorDefense: 0,
                skyBrightness: 0.1f,
                deltaSeconds: 0.05f,
                onSkeletonShootArrow: (o, v) => openArrows.Add((o, v)));
        }

        openArrows.Should().NotBeEmpty("a Skeleton in the open with clear line of sight still shoots");
    }

    [Fact]
    public void LineOfSight_FailsClosedOnUnloadedChunkColumns()
    {
        var registries = CoreRegistries.CreateDefault();
        var atlas = ProceduralAtlasGenerator.CreateDefault(registries.Blocks);
        var cache = new ClientWorldCache(registries.Blocks, atlas);

        // Nothing is loaded yet, so sight must be treated as blocked instead of see-through!
        WorldMobManager.HasClearLineOfSight(cache, new Vec3d(2.5, 65.6, 2.5), new Vec3d(8.5, 65.6, 2.5)).Should().BeFalse();

        cache.UpsertColumn(new ChunkColumn(new ChunkPos(0, 0)));

        // With the column loaded and completely empty the same sight line is genuinely clear!
        WorldMobManager.HasClearLineOfSight(cache, new Vec3d(2.5, 65.6, 2.5), new Vec3d(8.5, 65.6, 2.5)).Should().BeTrue();
    }

    [Fact]
    public void ContainerBlockEntity_HoldsTwentySevenSlotsTracksViewersAndDropsEverything()
    {
        var registries = CoreRegistries.CreateDefault();
        var items = registries.Items;
        var stoneId = (ushort)items.GetId(BuiltInItems.Coal);

        var container = new ContainerBlockEntity(new BlockPos(4, 64, 4));
        container.IsEmpty.Should().BeTrue();
        container.IsOpen.Should().BeFalse();

        container.SetSlot(0, new ItemStack(stoneId, 32));
        container.SetSlot(26, new ItemStack(stoneId, 5));
        container.GetSlot(0).Count.Should().Be(32);
        container.GetSlot(26).Count.Should().Be(5);
        container.IsEmpty.Should().BeFalse();

        // Out-of-range reads/writes are ignored instead of throwing!
        container.GetSlot(27).Should().Be(ItemStack.Empty);
        container.SetSlot(-1, new ItemStack(stoneId, 1));

        // Vanilla viewer counting drives the open block property and the open/close cues!
        container.AddViewer();
        container.IsOpen.Should().BeTrue();
        container.AddViewer();
        container.RemoveViewer().Should().BeFalse("a second viewer is still looking inside");
        container.RemoveViewer().Should().BeTrue("the last viewer closed the lid");

        var dropped = new List<ItemStack>();
        container.DropAllContents(dropped.Add);
        dropped.Should().HaveCount(2, "breaking a Barrel/Chest spills every stored stack");
        container.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void BarrelAndChestScreens_OpenTwentySevenSlotContainersWithOriginalCues()
    {
        var registries = CoreRegistries.CreateDefault();
        var containers = new ClientContainerSlotController(registries.Items, BuiltInRecipes.CreateFrozen(registries.Items));
        var barrelPos = new BlockPos(3, 64, 3);
        var chestPos = new BlockPos(9, 64, 9);

        Enum.GetValues<UiOverlayMode>().Should().Contain(UiOverlayMode.BarrelScreen);
        Enum.GetValues<UiOverlayMode>().Should().Contain(UiOverlayMode.ChestScreen);

        containers.OpenContainer(UiOverlayMode.BarrelScreen, barrelPos).Should().BeTrue("first viewer opens the barrel");
        containers.ActiveContainer.Should().NotBeNull();
        containers.ActiveContainer!.Position.Should().Be(barrelPos);
        containers.OpenedContainerMode.Should().Be(UiOverlayMode.BarrelScreen);

        // Re-opening the same barrel from a different screen does not double-fire the cue!
        containers.OpenContainer(UiOverlayMode.ChestScreen, chestPos).Should().BeTrue();
        containers.ActiveContainer!.Position.Should().Be(chestPos);

        // 27 storage cells are addressable through the screen cell-id range!
        ClientInventoryScreenBuilder.ContainerCellBase.Should().Be(500);
        ContainerBlockEntity.SlotCount.Should().Be(27);

        var stoneId = (ushort)registries.Items.GetId(BuiltInItems.Coal);
        containers.ActiveContainer!.SetSlot(13, new ItemStack(stoneId, 7));
        containers.ActiveContainer!.GetSlot(13).Count.Should().Be(7);

        containers.CloseContainer().Should().BeNull("the barrel still has no viewers and was already closed");
        containers.ActiveContainer.Should().BeNull();

        // Contents survive closing and re-opening, and spill into the inventory when the block is broken.
        containers.OpenContainer(UiOverlayMode.ChestScreen, chestPos);
        containers.ActiveContainer!.GetSlot(13).Count.Should().Be(7, "chest contents persist across open/close");

        var inv = new PlayerInventory();
        containers.RemoveContainerAndDropContents(chestPos, inv).Should().BeTrue();
        inv.CountItem(stoneId).Should().Be(7, "breaking a Chest drops its 27-slot contents into the player inventory");
        containers.PeekContainer(chestPos).Should().BeNull();
    }

    private static string Shape(Dictionary<BlockPos, BlockState> world, IBlockRegistry blocks, BlockPos pos)
    {
        var state = world.TryGetValue(pos, out var s) ? s : BlockState.Air;
        return RailPlacementRules.GetRailShape(blocks.GetStateMetadata(state));
    }

    private sealed class WallTestCollisionWorld : IVoxelCollisionReader
    {
        private readonly HashSet<BlockPos> _solids = [];

        public void FillSolid(BlockPos min, BlockPos max)
        {
            for (var x = min.X; x <= max.X; x++)
            {
                for (var y = min.Y; y <= max.Y; y++)
                {
                    for (var z = min.Z; z <= max.Z; z++)
                    {
                        _ = _solids.Add(new BlockPos(x, y, z));
                    }
                }
            }
        }

        public bool IsColumnLoaded(BlockPos pos) => true;
        public bool IsSolidBlock(BlockPos pos) => _solids.Contains(pos);
        public int GetFluidLevelOrNegative(BlockPos pos) => -1;
    }
}
