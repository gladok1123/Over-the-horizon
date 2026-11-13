using System.Globalization;
using System.Net;
using System.Numerics;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OverHorizon.Audio;
using OverHorizon.Client;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Crafting;
using OverHorizon.Gameplay.Effects;
using OverHorizon.Gameplay.Inventory;
using OverHorizon.Gameplay.Items;
using OverHorizon.Gameplay.Mobs;
using OverHorizon.Gameplay.Physics;
using OverHorizon.Gameplay.Redstone;
using OverHorizon.Modding.Commands;
using OverHorizon.Networking;
using OverHorizon.Networking.Client;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Networking.Packets.Serverbound;
using OverHorizon.Physics;
using OverHorizon.Rendering.Backends;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Meshing;
using OverHorizon.Rendering.Textures;
using OverHorizon.Server;
using OverHorizon.World.Generation;
using OverHorizon.World.Storage;
using Xunit;

namespace OverHorizon.Tests;

public sealed class V8MultiplayerWorldAndMechanicsTests
{
    [Fact]
    public void RedstoneRepeaterDelay_SideLocking_AndComparatorCompareSubtractModes_WorkAuthentically()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var items = registries.Items;
        var recipes = BuiltInRecipes.CreateFrozen(items);

        // Verify RedstoneComparator, OakTrapdoor, Composter, and BoneMeal crafting recipes exist
        recipes.Entries.Should().Contain(r => r.Output.ItemId == items.GetId(BuiltInBlocks.RedstoneComparator));
        recipes.Entries.Should().Contain(r => r.Output.ItemId == items.GetId(BuiltInBlocks.OakTrapdoor));
        recipes.Entries.Should().Contain(r => r.Output.ItemId == items.GetId(BuiltInBlocks.Composter));
        recipes.Entries.Should().Contain(r => r.Output.ItemId == items.GetId(BuiltInItems.BoneMeal));

        var map = new Dictionary<BlockPos, BlockState>();
        BlockState Get(BlockPos p) => map.GetValueOrDefault(p, BlockState.Air);
        void Set(BlockPos p, BlockState s) => map[p] = s;

        var stone = blocks.GetDefaultState(BuiltInBlocks.Stone);
        for (var x = -4; x <= 10; x++)
        {
            for (var z = -4; z <= 6; z++)
            {
                Set(new BlockPos(x, 63, z), stone);
            }
        }

        // 1. Repeater set to delay=3 (0.30s): facing="north" means rear input is South (+Z) and front output is North (-Z)!
        var repInputPos = new BlockPos(0, 64, 1);
        var repPos = new BlockPos(0, 64, 0);
        var repLampPos = new BlockPos(0, 64, -1);

        var repDelay3 = blocks.WithProperty(
            blocks.WithProperty(blocks.GetDefaultState(BuiltInBlocks.RedstoneRepeater), "facing", "north"),
            "delay",
            "3");
        Set(repInputPos, blocks.GetDefaultState(BuiltInBlocks.RedstoneBlock));
        Set(repPos, repDelay3);
        Set(repLampPos, blocks.GetDefaultState(BuiltInBlocks.RedstoneLamp));

        var redstone = new RedstoneEngine(blocks);
        redstone.NotifyBlockChanged(repPos);

        // First evaluation schedules the 0.30s diode transition -> Repeater MUST NOT turn on immediately!
        redstone.Step(0.08f, [], Get, Set, [], []);
        redstone.Step(0.10f, [], Get, Set, [], []);
        blocks.GetStateMetadata(Get(repPos)).Properties["powered"].Should().Be("false", "delay=3 repeater waits 0.30s before turning ON");
        blocks.GetStateMetadata(Get(repLampPos)).BlockKey.Should().Be(BuiltInBlocks.RedstoneLamp);

        // Advance past 0.30s total -> Repeater turns ON and lights the output RedstoneLamp!
        redstone.Step(0.12f, [], Get, Set, [], []);
        redstone.Step(0.12f, [], Get, Set, [], []);
        blocks.GetStateMetadata(Get(repPos)).Properties["powered"].Should().Be("true");
        blocks.GetStateMetadata(Get(repLampPos)).BlockKey.Should().Be(BuiltInBlocks.LitRedstoneLamp);

        // 2. RedstoneComparator: reads rear Composter fullness (level=7 -> rear signal > 5), toggles compare <-> subtract!
        var compRear = new BlockPos(5, 64, 1);
        var compPos = new BlockPos(5, 64, 0);
        var compSideWire = new BlockPos(4, 64, 0);
        var compOutWire = new BlockPos(5, 64, -1);

        Set(compRear, blocks.WithProperty(blocks.GetDefaultState(BuiltInBlocks.Composter), "level", "7"));
        Set(compPos, blocks.WithProperty(blocks.GetDefaultState(BuiltInBlocks.RedstoneComparator), "facing", "north"));
        Set(compOutWire, blocks.GetDefaultState(BuiltInBlocks.RedstoneWire));

        redstone.NotifyBlockChanged(compPos);
        redstone.Step(0.08f, [], Get, Set, [], []);
        redstone.Step(0.12f, [], Get, Set, [], []);
        var baseOutPower = int.Parse(blocks.GetStateMetadata(Get(compOutWire)).Properties["power"], CultureInfo.InvariantCulture);
        baseOutPower.Should().BeGreaterThan(5, "Comparator outputs rear Composter fullness signal");

        // Toggle Comparator to subtract mode via right-click interaction and apply a side wire signal
        redstone.TryInteractComponent(compPos, Get, Set).Should().BeTrue();
        blocks.GetStateMetadata(Get(compPos)).Properties["subtract"].Should().Be("true");

        Set(new BlockPos(3, 64, 0), blocks.GetDefaultState(BuiltInBlocks.Lever));
        _ = redstone.TryInteractComponent(new BlockPos(3, 64, 0), Get, Set);
        Set(compSideWire, blocks.GetDefaultState(BuiltInBlocks.RedstoneWire));
        redstone.NotifyBlockChanged(new BlockPos(3, 64, 0));
        redstone.NotifyBlockChanged(compPos);
        redstone.Step(0.08f, [], Get, Set, [], []);
        redstone.Step(0.12f, [], Get, Set, [], []);
        var subtractedPower = int.Parse(blocks.GetStateMetadata(Get(compOutWire)).Properties["power"], CultureInfo.InvariantCulture);
        subtractedPower.Should().BeLessThan(baseOutPower, "subtract mode reduces rear signal by side wire power");
    }

    [Fact]
    public void UnifiedInventory_PickBlock_BowArrowConsume_OffhandLeftSwing_ShieldBlock_SpectatorAndVoid_WorkCleanly()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var items = registries.Items;
        var atlas = ProceduralAtlasGenerator.CreateDefault(blocks);
        using var audio = new OpenAlAudioSubsystem(NullLogger<OpenAlAudioSubsystem>.Instance);

        var player = new ClientPlayerController(new Vec3d(0.5, 70.0, 0.5), blocks, items);
        player.Hotbar.BindAtlas(atlas);

        // 1. Unified Inventory: equip Apple in Creative -> switch to Survival -> eat Apple -> switch to Creative -> slot stays EMPTY!
        var appleIdx = ClientHotbar.FindCatalogIndex(BuiltInItems.Apple);
        appleIdx.Should().BeGreaterThan(0);
        player.SetGameMode(GameMode.Creative);
        player.Hotbar.SelectSlot(0);
        player.Hotbar.EquipPaletteBlock(appleIdx);
        player.Hotbar.SelectedBlockKey.Should().Be(BuiltInItems.Apple);

        player.SetGameMode(GameMode.Survival);
        player.Hotbar.SelectedBlockKey.Should().Be(BuiltInItems.Apple, "switching Creative -> Survival preserves exact unified inventory");
        player.Hotbar.SurvivalInventory.SetSlot(0, ItemStack.Empty); // Simulate eating the 1 Apple
        player.SetGameMode(GameMode.Creative);
        player.Hotbar.SelectedIsEmpty.Should().BeTrue("eaten Survival item NEVER reappears when switching back to Creative");

        // 2. Survival Death drops items and NEVER resets inventory to default Creative blocks or changes mode!
        var diamondId = (ushort)items.GetId(BuiltInItems.Diamond);
        player.SetGameMode(GameMode.Survival);
        player.Hotbar.SurvivalInventory.SetSlot(2, new ItemStack(diamondId, 7));
        var deathDrops = player.Hotbar.ExtractAndClearInventoryForDeathDrop();
        deathDrops.Should().Contain(s => s.ItemId == diamondId && s.Count == 7);
        player.Respawn();
        player.Mode.Should().Be(GameMode.Survival);
        player.Hotbar.SurvivalInventory.GetSlot(0).IsEmpty.Should().BeTrue("respawning never resets slots to default Creative blocks");

        // 3. Middle-Click Pick Block in Creative & Survival
        player.SetGameMode(GameMode.Creative);
        player.Hotbar.TryPickBlock(BuiltInBlocks.AmethystBlock, GameMode.Creative).Should().BeTrue();
        player.Hotbar.SelectedBlockKey.Should().Be(BuiltInBlocks.AmethystBlock);

        player.SetGameMode(GameMode.Survival);
        var lanternId = (ushort)items.GetId(BuiltInBlocks.Lantern);
        player.Hotbar.SurvivalInventory.SetSlot(15, new ItemStack(lanternId, 3)); // In storage row!
        player.Hotbar.TryPickBlock(BuiltInBlocks.Lantern, GameMode.Survival).Should().BeTrue();
        player.Hotbar.SelectedBlockKey.Should().Be(BuiltInBlocks.Lantern, "Survival Pick Block swaps matching item from storage into hotbar");

        // 4. Bow Arrow Consumption in Survival
        var arrowId = (ushort)items.GetId(BuiltInItems.Arrow);
        player.Hotbar.HasArrowForBow(GameMode.Survival).Should().BeFalse();
        player.Hotbar.SurvivalInventory.SetSlot(9, new ItemStack(arrowId, 2));
        player.Hotbar.HasArrowForBow(GameMode.Survival).Should().BeTrue();
        player.Hotbar.TryConsumeArrowForBow(GameMode.Survival).Should().BeTrue();
        player.Hotbar.SurvivalInventory.GetSlot(9).Count.Should().Be(1);

        // 5. Offhand Left-Hand Swing vs Mainhand Right-Hand Swing + Shield Deflection
        player.TriggerHandSwing(isOffhand: true);
        player.OffhandSwingProgress.Should().BeGreaterThan(0.0f);
        player.HandSwingProgress.Should().Be(0.0f, "using Offhand swings ONLY the left arm");
        player.LastSwingWasOffhand.Should().BeTrue();

        var shieldId = (ushort)items.GetId(BuiltInItems.Shield);
        player.Hotbar.SurvivalInventory.OffhandItem = new ItemStack(shieldId, 1);
        player.IsBlockingShield = true;
        player.Camera.SetRotation(0.0f, 0.0f); // Looking toward -Z
        player.TryDeflectWithShield(new Vec3d(0.5, 70.0, -3.0), audio).Should().BeTrue("frontal attack is deflected by raised Offhand Shield");
        player.TryDeflectWithShield(new Vec3d(0.5, 70.0, 4.0), audio).Should().BeFalse("attack from behind bypasses frontal Shield");

        // 6. Spectator Noclip Flight through solid Stone wall & Void Damage below Y = -76 in Survival
        var cache = new ClientWorldCache(blocks, atlas);
        var col = new ChunkColumn(new ChunkPos(0, 0));
        for (var x = 0; x < 16; x++)
        {
            for (var y = 65; y <= 75; y++)
            {
                for (var z = 0; z < 16; z++)
                {
                    _ = col.SetBlockState(x, y, z, blocks.GetDefaultState(BuiltInBlocks.Stone), blocks);
                }
            }
        }

        cache.UpsertColumn(col);
        player.ApplyAuthoritativePose(new Vec3d(8.5, 70.0, 12.5), 0.0f, 0.0f);
        player.SetGameMode(GameMode.Spectator);
        for (var i = 0; i < 12; i++)
        {
            player.StepMovement(cache, blocks, audio, (1.0f, 0.0f, false, true), 0.05);
        }

        player.FeetPosition.Z.Should().BeLessThan(10.0, "Spectator mode flies freely through solid Stone blocks");

        // Void gradual death below Bedrock (Y = -85) in Survival
        player.SetGameMode(GameMode.Survival);
        player.ApplyAuthoritativePose(new Vec3d(0.5, EngineConstants.WorldMinY - 20.0, 0.5), 0.0f, 0.0f);
        for (var i = 0; i < 15; i++)
        {
            player.StepMovement(cache, blocks, audio, (0.0f, 0.0f, false, false), 0.10);
        }

        player.Health.Should().BeLessThan(15.0f, "falling deep into The Void deals 4 HP per 0.5s in Survival");
    }

    [Fact]
    public void DroppedItemEntities_SmoothFallingBlocks_SmartAStarMobs_ParticlesAndStatusEffects_SimulateCleanly()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var items = registries.Items;
        var atlas = ProceduralAtlasGenerator.CreateDefault(blocks);
        var cache = new ClientWorldCache(blocks, atlas);

        var col = new ChunkColumn(new ChunkPos(0, 0));
        var stone = blocks.GetDefaultState(BuiltInBlocks.Stone);
        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++)
            {
                _ = col.SetBlockState(x, 63, z, stone, blocks);
            }
        }

        // Build a 2-block-high wall at Z = 8 from X = 3..9 so an A* Zombie at (6, 64, 11) must pathfind AROUND it to reach player at (6, 64, 5)!
        for (var x = 3; x <= 9; x++)
        {
            _ = col.SetBlockState(x, 64, 8, stone, blocks);
            _ = col.SetBlockState(x, 65, 8, stone, blocks);
        }

        // Place unsupported Sand at (2, 68, 2) to test smooth continuous-Y FallingBlockEntity!
        _ = col.SetBlockState(2, 68, 2, blocks.GetDefaultState(BuiltInBlocks.Sand), blocks);
        cache.UpsertColumn(col);

        // 1. Smooth FallingBlockEntity detaches immediately into continuous floating-point Y entity before landing at Y=64!
        var ticker = new PrimedTntAndWorldTicker(blocks);
        void SetWorld(BlockPos p, BlockState s) => _ = cache.ApplyBlockUpdate(p, s);
        ticker.NotifyWorldBlockChanged(new BlockPos(2, 68, 2), cache.GetBlockState, SetWorld);
        ticker.Step(0.07, cache, new Vec3d(6, 64, 5), cache.GetBlockState, cache.GetLight, SetWorld, []);
        ticker.FallingBlocks.Should().ContainSingle("Sand converts into smooth 3D FallingBlockEntity");
        ticker.Step(0.05, cache, new Vec3d(6, 64, 5), cache.GetBlockState, cache.GetLight, SetWorld, []);
        var midFallY = ticker.FallingBlocks[0].CurrentY;
        midFallY.Should().BeLessThan(68.0).And.BeGreaterThan(64.0, "FallingBlockEntity interpolates smoothly at fractional Y");

        for (var i = 0; i < 20; i++)
        {
            ticker.Step(0.05, cache, new Vec3d(6, 64, 5), cache.GetBlockState, cache.GetLight, SetWorld, []);
        }

        ticker.FallingBlocks.Should().BeEmpty();
        blocks.GetStateMetadata(cache.GetBlockState(new BlockPos(2, 64, 2))).BlockKey.Should().Be(BuiltInBlocks.Sand);

        // 2. 3D DroppedItemManager: merges identical nearby stacks and magnetically collects into PlayerInventory!
        var drops = new DroppedItemManager();
        var goldId = (ushort)items.GetId(BuiltInItems.GoldIngot);
        _ = drops.SpawnScatterDrop(new Vec3d(6.5, 64.5, 5.5), new ItemStack(goldId, 3), pickupDelaySeconds: 0.1f);
        _ = drops.SpawnScatterDrop(new Vec3d(6.6, 64.5, 5.5), new ItemStack(goldId, 4), pickupDelaySeconds: 0.1f);
        var inv = new PlayerInventory();
        var collected = 0;
        for (var i = 0; i < 25; i++)
        {
            collected += drops.Step(0.05, cache, items, inv, new Vec3d(6.5, 64.0, 5.5), GameMode.Survival);
        }

        collected.Should().BeGreaterThan(0);
        inv.CountItem(goldId).Should().Be(7, "both 3D Gold Ingot drops merged/collected into inventory");

        // 3. Smart A* Mob Pathfinding around obstacle wall + Strong Vanilla Knockback!
        var mobs = new WorldMobManager();
        var zombie = mobs.SpawnMob(MobType.Zombie, new Vec3d(6.5, 64.0, 10.5));
        for (var t = 0; t < 110; t++)
        {
            _ = mobs.Step(
                cache,
                _ => (0, 0, false, true, true),
                new Vec3d(6.5, 64.0, 5.5),
                GameMode.Survival,
                0,
                0.1f,
                0.05f);
        }

        zombie.FeetPosition.Z.Should().BeLessThan(8.2, "A* Zombie routed around the X=3..9 wall instead of getting stuck at Z=9");

        // 4. ParticleSystem & StatusEffectTracker
        var particles = new ParticleSystem();
        particles.EmitExplosion(new Vec3d(5, 66, 5));
        particles.Count.Should().BeGreaterThan(20);
        particles.Step(0.05f);

        var effects = new StatusEffectTracker();
        effects.ApplyEffect(StatusEffectType.Swiftness, 30.0f);
        effects.GetSpeedMultiplier().Should().BeGreaterThan(1.15f);
    }

    [Fact]
    public void MineshaftsAndStructures_LogUvFix_XRay_PersistentOptions_UniformStars_3DClouds_AndRemoteHeadPitch_VerifyCleanly()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var atlas = ProceduralAtlasGenerator.CreateDefault(blocks);

        // 1. OverworldGenerator populates Mineshafts + Dungeons/Geodes/Surface structures without crashing or leaking out of bounds
        var gen = new OverworldGenerator(123456789L, registries);
        var foundStructureBlock = false;
        for (var cx = 0; cx <= 8 && !foundStructureBlock; cx++)
        {
            for (var cz = 0; cz <= 8 && !foundStructureBlock; cz++)
            {
                var chunk = new ChunkColumn(new ChunkPos(cx, cz));
                gen.GenerateChunk(chunk);
                for (var y = -32; y <= 90 && !foundStructureBlock; y++)
                {
                    for (var x = 0; x < 16 && !foundStructureBlock; x += 2)
                    {
                        for (var z = 0; z < 16 && !foundStructureBlock; z += 2)
                        {
                            var bk = blocks.GetStateMetadata(chunk.GetBlockState(x, y, z)).BlockKey;
                            if (bk == BuiltInBlocks.OakPlanks || bk == BuiltInBlocks.OakFence || bk == BuiltInBlocks.Rail ||
                                bk == BuiltInBlocks.AmethystBlock || bk == BuiltInBlocks.MonsterSpawner || bk == BuiltInBlocks.Obsidian)
                            {
                                foundStructureBlock = true;
                            }
                        }
                    }
                }
            }
        }

        foundStructureBlock.Should().BeTrue("procedural structure generators place Mineshaft/Geode/Dungeon/Portal blocks");

        // 2. Horizontal OakLog axis=z UV orientation + X-Ray mode stone culling
        var col = new ChunkColumn(new ChunkPos(0, 0));
        var logZ = blocks.WithProperty(blocks.GetDefaultState(BuiltInBlocks.OakLog), "axis", "z");
        _ = col.SetBlockState(4, 64, 4, blocks.GetDefaultState(BuiltInBlocks.Stone), blocks);
        _ = col.SetBlockState(5, 64, 4, blocks.GetDefaultState(BuiltInBlocks.DiamondOre), blocks);
        _ = col.SetBlockState(8, 66, 8, logZ, blocks);
        var nb = new ChunkNeighborhood(col.CreateSnapshot());

        var normalMesh = GreedySectionMesher.MeshSection(nb, 8, blocks, atlas, xrayOverride: false);
        var xrayMesh = GreedySectionMesher.MeshSection(nb, 8, blocks, atlas, xrayOverride: true);
        xrayMesh.OpaqueVertices.Length.Should().BeLessThan(normalMesh.OpaqueVertices.Length, "X-Ray culls Stone while keeping DiamondOre & Logs visible");

        // 3. Persistent options.json round-trip
        var tmpOpt = Path.Combine(Path.GetTempPath(), $"oh_opts_{Guid.NewGuid():N}.json");
        try
        {
            var saved = new PersistedClientSettings { ViewDistanceChunks = 14, FovDegrees = 92.0f, MasterVolume = 0.65f, XRayEnabled = true, InvertMouseY = true };
            saved.Save(tmpOpt);
            var loaded = PersistedClientSettings.LoadOrDefault(tmpOpt);
            loaded.ViewDistanceChunks.Should().Be(14);
            loaded.FovDegrees.Should().Be(92.0f);
            loaded.MasterVolume.Should().BeApproximately(0.65f, 1e-4f);
            loaded.XRayEnabled.Should().BeTrue();
            loaded.InvertMouseY.Should().BeTrue();
        }
        finally
        {
            if (File.Exists(tmpOpt))
            {
                File.Delete(tmpOpt);
            }
        }

        // 4. Uniform Night Stars (distributed up to zenith Y > 170, no equatorial skirt) + 6-Face 3D Volumetric Clouds
        var skyVerts = new List<OverlayVertex>();
        SilkOpenGlOverlayRenderer.AppendCelestialBodies(skyVerts, WorldTimeCycle.MidnightTicks, 70.0);
        skyVerts.Should().Contain(v => v.Position.Y > 170.0f, "SplitMix32 spherical stars cover high zenith overhead");

        var cloudVerts = new List<OverlayVertex>();
        SilkOpenGlOverlayRenderer.AppendWorldClouds(cloudVerts, new Vec3d(0, 70, 0), WorldTimeCycle.NoonTicks, 12.0f);
        cloudVerts.Count.Should().BeGreaterThan(600, "3D volumetric clouds emit top, bottom, and all 4 side faces");

        // 5. Remote Player Head Pitch: because pivot is at the neck base (Y=1.50), looking DOWN (-45) tilts the forehead forward (-Z),
        //    whereas looking UP (+45) tilts the crown backward (+Z)!
        var downPeerVerts = new List<OverlayVertex>();
        var upPeerVerts = new List<OverlayVertex>();
        WorldMob3DMesher.AppendRemotePlayers(downPeerVerts, Vec3d.Zero, [new RemotePlayerAvatar(1, "A", new Vec3d(0, 0, -4), 0.0f, -45.0f, 0, 0, 20.0f)]);
        WorldMob3DMesher.AppendRemotePlayers(upPeerVerts, Vec3d.Zero, [new RemotePlayerAvatar(1, "A", new Vec3d(0, 0, -4), 0.0f, +45.0f, 0, 0, 20.0f)]);
        var downMinHeadZ = downPeerVerts.Where(v => v.Position.Y > 1.55f).Min(v => v.Position.Z);
        var upMinHeadZ = upPeerVerts.Where(v => v.Position.Y > 1.55f).Min(v => v.Position.Z);
        downMinHeadZ.Should().BeLessThan(upMinHeadZ - 0.25f, "looking down (-45) tilts the forehead forward into -Z while looking up (+45) tilts it back");
    }

    [Fact]
    public async Task MultiplayerV8_CreativePvpImmunity_3DArmorSync_DroppedItems_Arrows_AndCleanShutdown_SucceedAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var tempWorldDir = Path.Combine(Path.GetTempPath(), $"oh_mp_v8_{Guid.NewGuid():N}");
        try
        {
            var serverOpts = new ServerOptions(
                ListenPort: 0,
                PlayerViewDistanceChunks: 2,
                DefaultPlayerPermission: PermissionLevel.Admin,
                WorldSeed: 4242L,
                WorldSaveDirectory: tempWorldDir);

            using var serverHost = ServerBootstrap.BuildHost([], serverOpts);
            await serverHost.StartAsync(cts.Token).ConfigureAwait(false);

            try
            {
                var netServer = serverHost.Services.GetRequiredService<INetworkServer>();
                while (netServer.BoundEndPoint is null)
                {
                    await Task.Delay(10, cts.Token).ConfigureAwait(false);
                }

                var endpoint = new IPEndPoint(IPAddress.Loopback, netServer.BoundEndPoint.Port);

                await using var alice = await JavaProtocolClient.ConnectTcpAsync(endpoint, cts.Token).ConfigureAwait(false);
                var aliceLogin = await alice.LoginAsync("Alice", cancellationToken: cts.Token).ConfigureAwait(false);
                aliceLogin.Username.Should().Be("Alice");

                await using var bob = await JavaProtocolClient.ConnectTcpAsync(endpoint, cts.Token).ConfigureAwait(false);
                var bobLogin = await bob.LoginAsync("Bob", cancellationToken: cts.Token).ConfigureAwait(false);

                var registries = CoreRegistries.CreateDefault();
                var diamondSwordId = (ushort)registries.Items.GetId(BuiltInItems.DiamondSword);
                var diamondId = (ushort)registries.Items.GetId(BuiltInItems.Diamond);

                // 1. Put Bob into Creative Mode + full Diamond Armor (tier=4) via 0x05 PlayerEquipmentAndModePacket!
                await bob.SendPlayPacketAsync(
                    new PlayerEquipmentAndModePacket(
                        GameModeByte: (byte)GameMode.Creative,
                        MainHandItemId: diamondSwordId,
                        OffhandItemId: (ushort)registries.Items.GetId(BuiltInItems.Shield),
                        PackedArmorLower: 0x44,
                        PackedArmorUpper: 0x44,
                        ActionFlags: 1,
                        BowChargeByte: 0,
                        SwingCounter: 1),
                    cts.Token).ConfigureAwait(false);

                // Verify Alice receives RemotePlayerEquipmentPacket (0x0C) with Bob's Creative mode & Diamond Armor!
                RemotePlayerEquipmentPacket? receivedEquip = null;
                using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                readCts.CancelAfter(TimeSpan.FromSeconds(5));
                while (!readCts.IsCancellationRequested && receivedEquip is null)
                {
                    var pkt = await alice.ReceivePacketAsync(readCts.Token).ConfigureAwait(false);
                    if (pkt is RemotePlayerEquipmentPacket eq && eq.EntityId == bobLogin.AssignedEntityId)
                    {
                        receivedEquip = eq;
                    }
                }

                receivedEquip.Should().NotBeNull();
                receivedEquip!.GameModeByte.Should().Be((byte)GameMode.Creative);
                receivedEquip.PackedArmorLower.Should().Be(0x44);

                // 2. Alice attacks Creative Bob -> Bob is 100% immune (NO EntityCombatEventPacket emitted for Bob!)
                await alice.SendPlayPacketAsync(
                    new PlayerAttackEntityPacket(bobLogin.AssignedEntityId, diamondSwordId, IsCriticalHit: true),
                    cts.Token).ConfigureAwait(false);

                // 3. Alice throws 3 Diamonds into the world via 0x06 DropItem -> Server broadcasts 0x0D WorldEntitiesSnapshotPacket!
                await alice.SendPlayPacketAsync(
                    new PlayerWorldInteractionPacket(
                        PlayerWorldInteractionKind.DropItem,
                        new Vec3d(0.5, 82.0, 0.5),
                        new Vec3d(0.0, 0.0, -1.0),
                        diamondId,
                        3),
                    cts.Token).ConfigureAwait(false);

                WorldEntitiesSnapshotPacket? entitySnap = null;
                var gotCombatOnCreativeBob = false;
                using var snapCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                snapCts.CancelAfter(TimeSpan.FromSeconds(5));
                while (!snapCts.IsCancellationRequested && entitySnap is null)
                {
                    var pkt = await alice.ReceivePacketAsync(snapCts.Token).ConfigureAwait(false);
                    if (pkt is EntityCombatEventPacket ce && ce.TargetEntityId == bobLogin.AssignedEntityId)
                    {
                        gotCombatOnCreativeBob = true;
                    }

                    if (pkt is WorldEntitiesSnapshotPacket ws && ws.DroppedItems.Count > 0)
                    {
                        entitySnap = ws;
                    }
                }

                gotCombatOnCreativeBob.Should().BeFalse("Creative players cannot be damaged or killed in multiplayer PvP");
                entitySnap.Should().NotBeNull();
                entitySnap!.DroppedItems.Should().Contain(d => d.ItemId == diamondId && d.Count == 3);
            }
            finally
            {
                await serverHost.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            if (Directory.Exists(tempWorldDir))
            {
                Directory.Delete(tempWorldDir, recursive: true);
            }
        }
    }
}
