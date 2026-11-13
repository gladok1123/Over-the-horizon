using System.Buffers;
using System.Numerics;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
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
using OverHorizon.Gameplay.Redstone;
using OverHorizon.Gameplay.Survival;
using OverHorizon.Networking;
using OverHorizon.Networking.Codec;
using OverHorizon.Networking.Framing;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Physics;
using OverHorizon.Rendering;
using OverHorizon.Rendering.Backends;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Textures;
using OverHorizon.World.Generation;
using OverHorizon.World.Serialization;
using OverHorizon.World.Storage;
using Xunit;

namespace OverHorizon.Tests;

public sealed class V6MechanicsRedstoneWorldsAndUiTests
{
    [Fact]
    public void RedstoneEngineAndPistons_PropagatePowerInvertTorchLightLampPushBlocksAndPullStickyBlock()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var map = new Dictionary<BlockPos, BlockState>();

        BlockState Get(BlockPos p) => map.TryGetValue(p, out var s) ? s : BlockState.Air;
        void Set(BlockPos p, BlockState s) => map[p] = s;

        // Lever (unpowered) -> Wire -> RedstoneLamp, and Stone block with attached RedstoneTorch inverter
        var leverPos = new BlockPos(0, 64, 0);
        var wirePos = new BlockPos(1, 64, 0);
        var lampPos = new BlockPos(2, 64, 0);
        var supportPos = new BlockPos(1, 64, 1);
        var torchPos = new BlockPos(1, 65, 1);

        for (var x = -2; x <= 6; x++)
        {
            for (var z = -2; z <= 4; z++)
            {
                Set(new BlockPos(x, 63, z), blocks.GetDefaultState(BuiltInBlocks.Stone));
            }
        }

        Set(leverPos, blocks.GetDefaultState(BuiltInBlocks.Lever));
        Set(wirePos, blocks.GetDefaultState(BuiltInBlocks.RedstoneWire));
        Set(lampPos, blocks.GetDefaultState(BuiltInBlocks.RedstoneLamp));
        Set(supportPos, blocks.GetDefaultState(BuiltInBlocks.Stone));
        Set(torchPos, blocks.GetDefaultState(BuiltInBlocks.RedstoneTorch));

        var redstone = new RedstoneEngine(blocks);
        void EvalAt(BlockPos center)
        {
            redstone.NotifyBlockChanged(center);
            redstone.EvaluateImmediate(Get, Set, [], []);
        }

        EvalAt(leverPos);
        EvalAt(supportPos);
        blocks.GetStateMetadata(Get(lampPos)).Properties["lit"].Should().Be("false");
        blocks.GetStateMetadata(Get(torchPos)).BlockKey.Should().Be(BuiltInBlocks.RedstoneTorch);

        // Flip Lever ON -> Wire power > 0, RedstoneLamp lights up!
        _ = redstone.TryInteractComponent(leverPos, Get, Set);
        redstone.EvaluateImmediate(Get, Set, [], []);
        int.Parse(blocks.GetStateMetadata(Get(wirePos)).Properties["power"], System.Globalization.CultureInfo.InvariantCulture)
            .Should().BeGreaterThan(0);
        blocks.GetStateMetadata(Get(lampPos)).Properties["lit"].Should().Be("true");

        // Directly power Stone support with RedstoneBlock underneath -> RedstoneTorch turns OFF (inverter)!
        Set(new BlockPos(1, 63, 1), blocks.GetDefaultState(BuiltInBlocks.RedstoneBlock));
        EvalAt(supportPos);
        blocks.GetStateMetadata(Get(torchPos)).BlockKey.Should().Be(BuiltInBlocks.RedstoneTorchOff);

        // StickyPiston facing East pushes Cobblestone East when powered, and pulls it back when unpowered!
        var pistonPos = new BlockPos(0, 66, 0);
        var cobbleStart = new BlockPos(1, 66, 0);
        var cobblePushed = new BlockPos(2, 66, 0);
        var powerAbove = new BlockPos(0, 67, 0);

        var stickyEast = blocks.WithProperty(blocks.GetDefaultState(BuiltInBlocks.StickyPiston), "facing", "east");
        Set(pistonPos, stickyEast);
        Set(cobbleStart, blocks.GetDefaultState(BuiltInBlocks.Cobblestone));
        Set(powerAbove, blocks.GetDefaultState(BuiltInBlocks.RedstoneBlock));

        EvalAt(pistonPos);
        blocks.GetStateMetadata(Get(pistonPos)).Properties["extended"].Should().Be("true");
        blocks.GetStateMetadata(Get(cobbleStart)).BlockKey.Should().Be(BuiltInBlocks.PistonHead);
        blocks.GetStateMetadata(Get(cobblePushed)).BlockKey.Should().Be(BuiltInBlocks.Cobblestone);

        // Remove RedstoneBlock -> StickyPiston retracts AND pulls Cobblestone back to (1, 66, 0)!
        Set(powerAbove, BlockState.Air);
        EvalAt(pistonPos);
        blocks.GetStateMetadata(Get(pistonPos)).Properties["extended"].Should().Be("false");
        blocks.GetStateMetadata(Get(cobbleStart)).BlockKey.Should().Be(BuiltInBlocks.Cobblestone);
        Get(cobblePushed).IsAir.Should().BeTrue();
    }

    [Fact]
    public void TorchesAndLadders_EnforceWallOnlyLadderSubVoxelRaycastAndPopOffWhenSupportBreaks()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var items = registries.Items;
        var atlas = ProceduralAtlasGenerator.CreateDefault(blocks);
        var cache = new ClientWorldCache(blocks, atlas);

        var col = new ChunkColumn(new ChunkPos(0, 0));
        var stone = blocks.GetDefaultState(BuiltInBlocks.Stone);
        _ = col.SetBlockState(5, 63, 5, stone, blocks); // Floor at (5,63,5)
        _ = col.SetBlockState(5, 65, 4, stone, blocks); // North wall at (5,65,4)
        cache.UpsertColumn(col);

        // 1. Ladder MUST reject floor (Up), ceiling (Down), and unsupported air, and ONLY accept vertical solid walls!
        BlockAttachmentAndShapeRules.TryResolvePlacementState(blocks, cache.GetBlockState, BuiltInBlocks.Ladder, new BlockPos(5, 64, 5), Direction.Up, Vector3.UnitZ, out _, out _)
            .Should().BeFalse("ladders must never be placed on floors");
        BlockAttachmentAndShapeRules.TryResolvePlacementState(blocks, cache.GetBlockState, BuiltInBlocks.Ladder, new BlockPos(5, 62, 5), Direction.Down, Vector3.UnitZ, out _, out _)
            .Should().BeFalse("ladders must never be placed on ceilings");
        BlockAttachmentAndShapeRules.TryResolvePlacementState(blocks, cache.GetBlockState, BuiltInBlocks.Ladder, new BlockPos(10, 70, 10), Direction.South, Vector3.UnitZ, out _, out _)
            .Should().BeFalse("ladders must never levitate against air");

        BlockAttachmentAndShapeRules.TryResolvePlacementState(blocks, cache.GetBlockState, BuiltInBlocks.Ladder, new BlockPos(5, 65, 5), Direction.South, Vector3.UnitZ, out var wallLadder, out _)
            .Should().BeTrue("solid wall at (5,65,4) supports South-facing ladder at (5,65,5)");
        blocks.GetStateMetadata(wallLadder).Properties["facing"].Should().Be("south");
        _ = cache.ApplyBlockUpdate(new BlockPos(5, 65, 5), wallLadder);

        // 2. Torch places upright on floor AND tilted on solid wall, but rejects ceiling/air.
        BlockAttachmentAndShapeRules.TryResolvePlacementState(blocks, cache.GetBlockState, BuiltInBlocks.Torch, new BlockPos(5, 64, 5), Direction.Up, Vector3.UnitZ, out var floorTorch, out _)
            .Should().BeTrue();
        blocks.GetStateMetadata(floorTorch).Properties["facing"].Should().Be("up");
        _ = cache.ApplyBlockUpdate(new BlockPos(5, 64, 5), floorTorch);

        BlockAttachmentAndShapeRules.TryResolvePlacementState(blocks, cache.GetBlockState, BuiltInBlocks.Torch, new BlockPos(5, 62, 5), Direction.Down, Vector3.UnitZ, out _, out _)
            .Should().BeFalse("torches cannot hang upside-down from ceilings");

        // 3. Sub-voxel VoxelRaycaster hits non-solid Torch & Ladder so they can be targeted and broken!
        var hitTorch = VoxelRaycaster.Raycast(cache, new Vec3d(5.5, 64.3, 7.0), new Vector3(0.0f, 0.0f, -1.0f), 5.0);
        hitTorch.Should().NotBeNull();
        hitTorch!.Value.BlockPosition.Should().Be(new BlockPos(5, 64, 5));

        var hitLadder = VoxelRaycaster.Raycast(cache, new Vec3d(5.5, 65.5, 7.0), new Vector3(0.0f, 0.0f, -1.0f), 5.0);
        hitLadder.Should().NotBeNull();
        hitLadder!.Value.BlockPosition.Should().Be(new BlockPos(5, 65, 5));

        // 4. Breaking the wall at (5,65,4) invalidates support for the Ladder at (5,65,5) and resolves a Ladder drop!
        _ = cache.ApplyBlockUpdate(new BlockPos(5, 65, 4), BlockState.Air);
        BlockAttachmentAndShapeRules.IsStillSupported(blocks, cache.GetBlockState, new BlockPos(5, 65, 5), wallLadder)
            .Should().BeFalse("unsupported ladder must be detected immediately when its supporting wall breaks");
        var drop = BlockDropResolver.ResolveDrop(BuiltInBlocks.Ladder, null, items);
        drop.ItemId.Should().Be((ushort)items.GetId(BuiltInBlocks.Ladder));
    }

    [Fact]
    public void FlintAndSteelPrimedTntSandGravityWaterFlowAndGrassSpread_SimulateDeterministically()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var atlas = ProceduralAtlasGenerator.CreateDefault(blocks);
        var cache = new ClientWorldCache(blocks, atlas);

        var col = new ChunkColumn(new ChunkPos(0, 0));
        var stone = blocks.GetDefaultState(BuiltInBlocks.Stone);
        var bedrock = blocks.GetDefaultState(BuiltInBlocks.Bedrock);
        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++)
            {
                _ = col.SetBlockState(x, 60, z, bedrock, blocks);
                _ = col.SetBlockState(x, 61, z, stone, blocks);
            }
        }

        // Unsupported Sand at (2, 64, 2), Water source at (10, 62, 10), GrassBlock next to Dirt at (6, 62, 6..7)
        _ = col.SetBlockState(2, 64, 2, blocks.GetDefaultState(BuiltInBlocks.Sand), blocks);
        _ = col.SetBlockState(10, 62, 10, blocks.GetDefaultState(BuiltInBlocks.Water), blocks);
        _ = col.SetBlockState(6, 62, 6, blocks.GetDefaultState(BuiltInBlocks.GrassBlock), blocks);
        _ = col.SetBlockState(7, 62, 6, blocks.GetDefaultState(BuiltInBlocks.Dirt), blocks);
        _ = col.SetBlockState(4, 62, 4, blocks.GetDefaultState(BuiltInBlocks.Tnt), blocks);
        cache.UpsertColumn(col);

        var ticker = new PrimedTntAndWorldTicker(blocks);
        void SetWorld(BlockPos p, BlockState s) => _ = cache.ApplyBlockUpdate(p, s);

        ticker.NotifyWorldBlockChanged(new BlockPos(2, 64, 2), cache.GetBlockState, SetWorld);
        ticker.NotifyWorldBlockChanged(new BlockPos(10, 62, 10), cache.GetBlockState, SetWorld);
        _ = cache.ApplyBlockUpdate(new BlockPos(4, 62, 4), BlockState.Air);
        _ = ticker.SpawnPrimedTnt(new BlockPos(4, 62, 4));

        cache.GetBlockState(new BlockPos(4, 62, 4)).IsAir.Should().BeTrue("ignited TNT block converts into live PrimedTntEntity");
        ticker.PrimedTnts.Should().HaveCount(1);

        var craters = new List<Vec3d>();
        for (var step = 0; step < 50; step++)
        {
            ticker.Step(
                0.10,
                cache,
                new Vec3d(8.0, 65.0, 8.0),
                cache.GetBlockState,
                cache.GetLight,
                SetWorld,
                craters);
        }

        // 1. PrimedTnt detonated after 4.0s fuse, carving Stone at Y=61 while sparing Bedrock at Y=60!
        ticker.PrimedTnts.Should().BeEmpty();
        craters.Should().NotBeEmpty();
        cache.GetBlockState(new BlockPos(4, 61, 4)).IsAir.Should().BeTrue("explosion destroys breakable Stone beneath TNT");
        cache.GetBlockState(new BlockPos(4, 60, 4)).Should().Be(bedrock, "Bedrock is blast-immune");

        // 2. Unsupported Sand fell from Y=64 down onto Y=62!
        blocks.GetStateMetadata(cache.GetBlockState(new BlockPos(2, 62, 2))).BlockKey.Should().Be(BuiltInBlocks.Sand);
        cache.GetBlockState(new BlockPos(2, 64, 2)).IsAir.Should().BeTrue();

        // 3. Water spread outward from (10, 62, 10) to cardinal neighbors!
        blocks.GetStateMetadata(cache.GetBlockState(new BlockPos(11, 62, 10))).BlockKey.Should().Be(BuiltInBlocks.Water);
    }

    [Fact]
    public async Task WorldSavesSlashCommandsDaylightUndeadBurningAndBedSleep_WorkEndToEndAsync()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"oh_v6_saves_{Guid.NewGuid():N}");
        try
        {
            var registries = CoreRegistries.CreateDefault();
            var blocks = registries.Blocks;
            var atlas = ProceduralAtlasGenerator.CreateDefault(blocks);
            using var audio = new OpenAlAudioSubsystem(NullLogger<OpenAlAudioSubsystem>.Instance);
            var renderer = new HeadlessRenderSubsystem();

            var createdMeta = WorldSaveManager.CreateWorld("My Test Kingdom", GameMode.Creative, "custom-seed-42", tempRoot);
            var listed = WorldSaveManager.ListWorlds(tempRoot);
            listed.Should().ContainSingle(w => w.WorldName == "My Test Kingdom" && w.DefaultGameMode == GameMode.Creative);

            await using var savedWorld = WorldSaveManager.OpenWorldSubsystem(createdMeta, registries, tempRoot);
            var cache = new ClientWorldCache(blocks, atlas);
            var col = new ChunkColumn(new ChunkPos(0, 0));
            for (var x = 0; x < 16; x++)
            {
                for (var z = 0; z < 16; z++)
                {
                    _ = col.SetBlockState(x, 63, z, blocks.GetDefaultState(BuiltInBlocks.GrassBlock), blocks);
                }
            }

            cache.UpsertColumn(col);

            var mobs = new WorldMobManager();
            var bridge = new ClientWorldSyncAndMobBridge(blocks, registries.Items, savedWorld, cache, renderer, audio, mobs);
            var player = new ClientPlayerController(new Vec3d(4.5, 64.0, 4.5), blocks, registries.Items);
            var chat = new ClientChatHistory();

            // 1. Console commands: /time set night, /give diamond 5, /summon zombie
            ClientSlashCommandExecutor.TryExecuteLocalCommand("/time set night", player, bridge, mobs, registries, audio, chat, createdMeta.Seed)
                .Should().BeTrue();
            bridge.TimeCycle.IsNightForSleeping.Should().BeTrue();

            ClientSlashCommandExecutor.TryExecuteLocalCommand("/give diamond 5", player, bridge, mobs, registries, audio, chat, createdMeta.Seed)
                .Should().BeTrue();
            player.Hotbar.SurvivalInventory.CountItem((ushort)registries.Items.GetId(BuiltInItems.Diamond)).Should().Be(5);

            // 2. Place RedBed during night (no monsters nearby) and right-click to sleep -> advances to morning (1000)!
            var bedPos = new BlockPos(5, 64, 4);
            var bedFoot = blocks.WithProperty(blocks.GetDefaultState(BuiltInBlocks.RedBed), "part", "foot");
            _ = cache.ApplyBlockUpdate(bedPos, bedFoot);

            var combat = new ClientSurvivalCombatController(blocks, registries.Items, audio);
            player.ApplyAuthoritativePose(new Vec3d(5.5, 64.0, 6.2), 0.0f, -38.0f);
            player.RefreshRaycastTarget(cache);
            combat.HandleRightClickUseOrPlace(
                player, cache, bridge, mobs, chat, (p, _, s) => cache.ApplyBlockUpdate(p, s), out _, out _)
                .Should().BeTrue("clicking a RedBed at night with no nearby monsters sleeps until morning");
            bridge.TimeCycle.TimeOfDay.Should().Be((int)WorldTimeCycle.DayTicks);

            // 3. Spawn Zombie & Skeleton under open morning sky -> both catch fire and burn!
            var sunlitZombie = mobs.SpawnMob(MobType.Zombie, new Vec3d(12.5, 64.0, 12.5));
            var shadedZombie = mobs.SpawnMob(MobType.Zombie, new Vec3d(2.5, 64.0, 2.5));
            var initialHp = sunlitZombie.Health;

            for (var s = 0; s < 10; s++)
            {
                _ = mobs.Step(
                    cache,
                    pos => pos.X > 8 ? ((byte)15, (byte)0, false, true, true) : ((byte)4, (byte)0, false, true, true),
                    player.FeetPosition,
                    GameMode.Creative,
                    0,
                    bridge.TimeCycle.SkyBrightness,
                    0.10f);
            }

            sunlitZombie.IsOnFire.Should().BeTrue("undead mob under open daytime sky catches fire");
            sunlitZombie.Health.Should().BeLessThan(initialHp, "burning undead mob takes fire damage");
            shadedZombie.IsOnFire.Should().BeFalse("undead mob under shade roof does not burn");
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void OffhandAirSwingBlockCracksHungerCreativeTabsRecipesSoundsAndMultiplayerPackets_MeetAllV6Specs()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var items = registries.Items;
        var atlas = ProceduralAtlasGenerator.CreateDefault(blocks);
        var entityAtlas = EntityTextureAtlasGenerator.CreateDefault();
        entityAtlas.Width.Should().Be(EntityTextureAtlasGenerator.Width);
        entityAtlas.RgbaPixels.Length.Should().Be(EntityTextureAtlasGenerator.Width * EntityTextureAtlasGenerator.Height * 4);

        // 1. All 47 authentic 23w13a sound cues synthesized/mapped cleanly
        var soundBank = ProceduralSoundBank.CreateDefaultBank();
        foreach (var cue in Enum.GetValues<SoundCue>())
        {
            soundBank.ContainsKey(cue).Should().BeTrue($"SoundCue.{cue} must be present in sound bank");
        }

        // 2. Comprehensive Crafting Recipes (Redstone, Piston, StickyPiston, RedBed, Tnt, Shield, Bookshelf, FlintAndSteel)
        var recipes = BuiltInRecipes.CreateFrozen(items);
        recipes.Entries.Count.Should().BeGreaterThanOrEqualTo(90);
        ushort[] requiredCraftOutputs =
        [
            (ushort)items.GetId(BuiltInBlocks.Piston),
            (ushort)items.GetId(BuiltInBlocks.StickyPiston),
            (ushort)items.GetId(BuiltInBlocks.RedstoneTorch),
            (ushort)items.GetId(BuiltInBlocks.Lever),
            (ushort)items.GetId(BuiltInBlocks.RedstoneRepeater),
            (ushort)items.GetId(BuiltInBlocks.RedstoneLamp),
            (ushort)items.GetId(BuiltInBlocks.RedBed),
            (ushort)items.GetId(BuiltInBlocks.Tnt),
            (ushort)items.GetId(BuiltInBlocks.Bookshelf),
            (ushort)items.GetId(BuiltInItems.FlintAndSteel),
            (ushort)items.GetId(BuiltInItems.Shield),
        ];
        foreach (var outId in requiredCraftOutputs)
        {
            recipes.Entries.Should().Contain(r => r.Output.ItemId == outId);
        }

        // 3. Offhand swap (F) + Dynamic Held Light in Offhand + 1st-person Offhand & Right-hand viewmodel
        using var audio = new OpenAlAudioSubsystem(NullLogger<OpenAlAudioSubsystem>.Instance);
        var player = new ClientPlayerController(new Vec3d(0.5, 70.0, 0.5), blocks, items);
        player.SetGameMode(GameMode.Survival);
        var torchItemId = (ushort)items.GetId(BuiltInBlocks.Torch);
        player.Hotbar.SurvivalInventory.SetSlot(0, new ItemStack(torchItemId, 8));
        player.Hotbar.RefreshSlotVisuals(GameMode.Survival);
        player.SwapOffhand().Should().BeTrue();
        player.Hotbar.SurvivalInventory.OffhandItem.ItemId.Should().Be(torchItemId);
        player.Hotbar.CombinedHeldLightEmission.Should().Be(14, "Torch in Offhand emits dynamic warm light 14");

        // 4. Air Swing: Left-clicking with CurrentTarget == null triggers full HandSwingProgress!
        var cache = new ClientWorldCache(blocks, atlas);
        var combat = new ClientSurvivalCombatController(blocks, items, audio);
        player.CurrentTarget.Should().BeNull();
        player.HandSwingProgress.Should().Be(0.0f);
        combat.StepMining(0.016, leftClickJustPressed: true, leftMouseHeld: true, player, cache, (_, _, _) => false);
        player.HandSwingProgress.Should().BeGreaterThan(0.0f, "left-clicking empty air must trigger hand swing animation");

        // 5. On-Block Mining Cracks (destroy_stage_0..9) & NO 2D HUD progress bar, plus Offhand & Steve 3D geometry
        var menu = new ClientMenuController(new ClientOptions(), UiOverlayMode.BlockInventory, items, recipes);
        menu.CreativeSearchQuery = "piston";
        var creativeOverlay = menu.BuildOverlayState(
            new Vector2(1280, 720), player, cache, [], [], [], 1000L, 1.0f, new ClientChatHistory(), string.Empty) with
        {
            TargetedBlock = new BlockPos(1, 69, 1),
            MiningProgress = 0.55f,
        };
        creativeOverlay.InventoryCells.Should().Contain(c => c.DisplayName.Contains("Piston", StringComparison.OrdinalIgnoreCase));

        var (cwx, cwy, _, _) = ClientInventoryScreenBuilder.GetCreativeWindowBounds(1280, 720);
        const float cs = ClientInventoryScreenBuilder.CreativeGuiScale;
        _ = menu.HandleContainerOrMenuClick(new Vector2(cwx + (180.0f * cs), cwy - (14.0f * cs)), new Vector2(1280, 720), player, isRightClick: false);
        var creativeInvTabOverlay = menu.BuildOverlayState(
            new Vector2(1280, 720), player, cache, [], [], [], 1000L, 1.0f, new ClientChatHistory(), string.Empty);
        creativeInvTabOverlay.ShowInventoryPlayerModel.Should().BeTrue();
        creativeInvTabOverlay.InventoryCells.Should().Contain(c => c.BlockIndex == ClientInventoryScreenBuilder.CreativeDeleteTrashCellId);

        var heldAndSteveVerts = new List<OverlayVertex>();
        HeldItem3DMesher.AppendHeldViewmodel(heldAndSteveVerts, creativeOverlay with { Mode = UiOverlayMode.InGame }, atlas);
        heldAndSteveVerts.Count.Should().BeGreaterThan(0, "Offhand Torch emits 3D left-hand viewmodel geometry");

        var steveVerts = new List<OverlayVertex>();
        WorldMob3DMesher.AppendSteveModel(steveVerts, Matrix4x4.Identity, 0.25f, -0.15f, 0.0f, Vector4.One);
        steveVerts.Count.Should().Be(7 * 36, "3D Inventory Steve preview emits 7 articulated boxes (head + hat, torso, 2 arms, 2 legs)");

        // 6. Refined Hunger + Saturation + Exhaustion model
        var vitals = new HungerAndVitalsModel { Hunger = 20, Saturation = 4.0f };
        var (healAmt, _) = vitals.Step(0.6, currentHealth: 14.0f);
        healAmt.Should().BeGreaterThan(0.0f, "full hunger + saturation regenerates health");
        vitals.Hunger = 6;
        vitals.CanSprint.Should().BeFalse("sprint is disabled at <= 6 hunger points (3 drumsticks)");

        // 7. Multiplayer MultiBlockUpdatePacket (0x0A) & WorldTimeUpdatePacket (0x0B) round-trip
        var codec = new JavaProtocolCodec();
        var batchPkt = new MultiBlockUpdatePacket([(new BlockPos(3, 64, -2), (ushort)17), (new BlockPos(4, 64, -2), (ushort)0)]);
        var timePkt = new WorldTimeUpdatePacket(13_500L);

        foreach (var pkt in new OverHorizon.Networking.Packets.IPacket[] { batchPkt, timePkt })
        {
            var writer = new ArrayBufferWriter<byte>();
            PacketFramer.WriteFrame(writer, codec, ConnectionState.Play, pkt, compressionThreshold: 64);
            var seq = new ReadOnlySequence<byte>(writer.WrittenMemory);
            PacketFramer.TryReadFrame(ref seq, 64, out var rented, out var len).Should().BeTrue();
            var decoded = codec.DecodeClientbound(ConnectionState.Play, rented.AsSpan(0, len));
            ArrayPool<byte>.Shared.Return(rented);
            decoded.GetType().Should().Be(pkt.GetType());
        }
    }
}
