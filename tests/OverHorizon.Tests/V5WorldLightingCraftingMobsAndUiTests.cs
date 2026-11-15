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
using OverHorizon.Gameplay.Smelting;
using OverHorizon.Rendering.Backends;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Meshing;
using OverHorizon.Rendering.Textures;
using OverHorizon.World.ChunkManagement;
using OverHorizon.World.Generation;
using OverHorizon.World.Lighting;
using OverHorizon.World.Serialization;
using OverHorizon.World.Storage;
using Xunit;

namespace OverHorizon.Tests;

public sealed class V5WorldLightingCraftingMobsAndUiTests
{
    [Fact]
    public void OverworldGenerator_GeneratesDeepslateCavesOresTreesAndTwoLevelMineshafts()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var generator = new OverworldGenerator(worldSeed: 20260500L, registries);

        var foundDeepslate = false;
        var foundCaveAir = false;
        var foundIronOrCoal = false;
        var foundDeepOre = false;
        var foundTreeLog = false;
        var foundMineshaftRailOrTorch = false;

        for (var cx = 0; cx <= 11; cx++)
        {
            for (var cz = 0; cz <= 11; cz++)
            {
                var col = new ChunkColumn(new ChunkPos(cx, cz));
                generator.GenerateChunk(col);

                for (var x = 0; x < 16; x += 2)
                {
                    for (var z = 0; z < 16; z += 2)
                    {
                        var deepKey = blocks.GetStateMetadata(col.GetBlockState(x, -24, z)).BlockKey;
                        if (deepKey == BuiltInBlocks.Deepslate) foundDeepslate = true;
                        if (deepKey == BuiltInBlocks.Air) foundCaveAir = true;

                        for (var y = -56; y <= 92; y += 2)
                        {
                            var k = blocks.GetStateMetadata(col.GetBlockState(x, y, z)).BlockKey;
                            if (k == BuiltInBlocks.IronOre || k == BuiltInBlocks.CoalOre || k == BuiltInBlocks.CopperOre)
                            {
                                foundIronOrCoal = true;
                            }
                            else if (k == BuiltInBlocks.DiamondOre || k == BuiltInBlocks.GoldOre || k == BuiltInBlocks.RedstoneOre || k == BuiltInBlocks.LapisOre)
                            {
                                foundDeepOre = true;
                            }
                            else if (k == BuiltInBlocks.OakLog || k == BuiltInBlocks.BirchLog)
                            {
                                foundTreeLog = true;
                            }
                        }
                    }
                }

                for (var y = 16; y <= 36 && !foundMineshaftRailOrTorch; y++)
                {
                    for (var z = 0; z < 16 && !foundMineshaftRailOrTorch; z++)
                    {
                        for (var x = 0; x < 16; x++)
                        {
                            var mk = blocks.GetStateMetadata(col.GetBlockState(x, y, z)).BlockKey;
                            if (mk == BuiltInBlocks.Rail || mk == BuiltInBlocks.Torch || mk == BuiltInBlocks.Cobweb || mk == BuiltInBlocks.OakFence)
                            {
                                foundMineshaftRailOrTorch = true;
                                break;
                            }
                        }
                    }
                }
            }
        }

        foundDeepslate.Should().BeTrue("negative Y strata must transition into Deepslate");
        foundCaveAir.Should().BeTrue("3D cheese & spaghetti cave carvers must excavate underground chambers");
        foundIronOrCoal.Should().BeTrue("upper strata must populate Coal, Copper, and Iron ore veins");
        foundDeepOre.Should().BeTrue("deep strata must populate Gold, Lapis, Redstone, and Diamond ore veins");
        foundTreeLog.Should().BeTrue("surface biomes must populate cross-chunk Oak and Birch trees");
        foundMineshaftRailOrTorch.Should().BeTrue("underground regions must generate 2-level timbered Mineshafts with rails, fences, cobwebs, and torches");
    }

    [Fact]
    public void ShapedBlockMesherAndLighting_EmitsSubVoxelTorchRailFenceAndPacks0To15LightNibbles()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var atlas = ProceduralAtlasGenerator.CreateDefault(blocks);

        var col = new ChunkColumn(new ChunkPos(0, 0));
        _ = col.SetBlockState(4, 64, 4, blocks.GetDefaultState(BuiltInBlocks.Torch), blocks);
        _ = col.SetBlockState(6, 64, 4, blocks.GetDefaultState(BuiltInBlocks.Rail), blocks);
        _ = col.SetBlockState(8, 64, 4, blocks.GetDefaultState(BuiltInBlocks.OakFence), blocks);
        _ = col.SetBlockState(9, 64, 4, blocks.GetDefaultState(BuiltInBlocks.OakFence), blocks);
        _ = col.SetBlockState(11, 64, 4, blocks.GetDefaultState(BuiltInBlocks.Cobweb), blocks);
        col.RecomputeHeightmaps(blocks);
        LightingEngine.InitializeChunkLight(col, blocks);

        col.GetBlockLight(4, 64, 4).Should().Be(14, "placed Torch emits block light 14");
        col.GetBlockLight(5, 64, 4).Should().Be(13, "block light attenuates 1 level per block");

        var effInDarkCaveWithHeldTorch = LightingEngine.ComputeEffectiveLightLevel(skyLight: 0, blockLight: 2, dynamicLight: 14, skyBrightness: 1.0f);
        effInDarkCaveWithHeldTorch.Should().Be(14);
        LightingEngine.LightLevelToBrightness(15).Should().BeApproximately(1.0f, 0.001f);
        LightingEngine.LightLevelToBrightness(0).Should().BeGreaterThan(0.02f).And.BeLessThan(0.10f);

        var nb = new ChunkNeighborhood(col.CreateSnapshot());
        var mesh = GreedySectionMesher.MeshSection(in nb, sectionIndex: 8, blocks, atlas);
        mesh.IsEmpty.Should().BeFalse();
        mesh.OpaqueVertices.Length.Should().BeGreaterThan(40, "Torch, Rail, connected OakFences, and Cobweb emit shaped sub-voxel geometry");

        var anyLitVertex = false;
        var verts = mesh.OpaqueVertices.Span;
        for (var i = 0; i < verts.Length; i++)
        {
            var packed = (int)MathF.Round(verts[i].LightLevel);
            var sky = (packed >> 4) & 15;
            var blk = packed & 15;
            sky.Should().BeInRange(0, 15);
            blk.Should().BeInRange(0, 15);
            if (blk >= 13) anyLitVertex = true;
        }

        anyLitVertex.Should().BeTrue();
    }

    [Fact]
    public void CraftingCursorSlotsAndFurnaceSmelting_CraftToolsArmorAndSmeltRawOres()
    {
        var registries = CoreRegistries.CreateDefault();
        var items = registries.Items;
        var recipes = BuiltInRecipes.CreateFrozen(items);
        recipes.Entries.Count.Should().BeGreaterThanOrEqualTo(60);

        // 1. 2x2 Torch crafting (Coal over Stick -> 4 Torches)
        var grid2x2 = new CraftingGrid(2, 2);
        grid2x2.SetSlot(0, 0, new ItemStack((ushort)items.GetId(BuiltInItems.Coal), 2));
        grid2x2.SetSlot(0, 1, new ItemStack((ushort)items.GetId(BuiltInItems.Stick), 2));

        var inv = new PlayerInventory();
        var cursor = ItemStack.Empty;
        SlotClickInteraction.TryTakeCraftingOutput(grid2x2, recipes, items, inv, ref cursor, craftAllToInventory: false).Should().BeTrue();
        cursor.ItemId.Should().Be((ushort)items.GetId(BuiltInBlocks.Torch));
        cursor.Count.Should().Be(4);

        var slot0 = ItemStack.Empty;
        SlotClickInteraction.ClickSlot(ref slot0, ref cursor, isRightClick: true, items);
        slot0.Count.Should().Be(1);
        cursor.Count.Should().Be(3);
        SlotClickInteraction.ClickSlot(ref slot0, ref cursor, isRightClick: false, items);
        slot0.Count.Should().Be(4);
        cursor.IsEmpty.Should().BeTrue();

        // 2. 3x3 Crafting Table: Diamond Chestplate (8 Diamonds)
        var grid3x3 = new CraftingGrid(3, 3);
        var diamondId = (ushort)items.GetId(BuiltInItems.Diamond);
        for (var r = 0; r < 3; r++)
        {
            for (var c = 0; c < 3; c++)
            {
                if (r == 0 && c == 1) continue;
                grid3x3.SetSlot(c, r, new ItemStack(diamondId, 1));
            }
        }

        SlotClickInteraction.TryTakeCraftingOutput(grid3x3, recipes, items, inv, ref cursor).Should().BeTrue();
        cursor.ItemId.Should().Be((ushort)items.GetId(BuiltInItems.DiamondChestplate));

        // 3. 3-Slot Furnace Smelting: Raw Iron + Coal -> Iron Ingot
        var furnaceRecipes = new FurnaceRecipes(items);
        var furnace = new FurnaceBlockEntity(new BlockPos(2, 65, 2))
        {
            InputSlot = new ItemStack((ushort)items.GetId(BuiltInItems.RawIron), 2),
            FuelSlot = new ItemStack((ushort)items.GetId(BuiltInItems.Coal), 1),
        };

        _ = furnace.Tick(0.25f, furnaceRecipes, items);
        furnace.IsLit.Should().BeTrue();
        furnace.FuelSlot.IsEmpty.Should().BeTrue("1 Coal consumed to light 80s burn timer");

        _ = furnace.Tick(FurnaceRecipes.CookDurationSeconds + 0.1f, furnaceRecipes, items);
        furnace.OutputSlot.ItemId.Should().Be((ushort)items.GetId(BuiltInItems.IronIngot));
        furnace.OutputSlot.Count.Should().Be(1);
        furnace.InputSlot.Count.Should().Be(1);
    }

    [Fact]
    public void WorldMobManager_IgnoresCreativePlayersChasesSurvivalAndDropsLootOnSwordKill()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var atlas = ProceduralAtlasGenerator.CreateDefault(blocks);
        var cache = new ClientWorldCache(blocks, atlas);

        var col = new ChunkColumn(new ChunkPos(0, 0));
        var grass = blocks.GetDefaultState(BuiltInBlocks.GrassBlock);
        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++)
            {
                _ = col.SetBlockState(x, 63, z, grass, blocks);
            }
        }

        cache.UpsertColumn(col);

        var mobs = new WorldMobManager();
        var zombie = mobs.SpawnMob(MobType.Zombie, new Vec3d(8.5, 64.0, 9.2));
        var sheep = mobs.SpawnMob(MobType.Sheep, new Vec3d(6.5, 64.0, 8.5));
        var playerFeet = new Vec3d(8.5, 64.0, 8.5);

        static (byte, byte, bool, bool, bool) NoNaturalSpawnProbe(BlockPos _) => (15, 0, false, false, false);

        for (var i = 0; i < 10; i++)
        {
            var creativeDmg = mobs.Step(cache, NoNaturalSpawnProbe, playerFeet, GameMode.Creative, 0, 1.0f, 0.05f);
            creativeDmg.Should().Be(0.0f, "hostile mobs must strictly ignore Creative players");
        }

        var survivalDmg = 0.0f;
        for (var i = 0; i < 12; i++)
        {
            survivalDmg += mobs.Step(cache, NoNaturalSpawnProbe, zombie.FeetPosition, GameMode.Survival, 0, 1.0f, 0.05f);
        }

        survivalDmg.Should().BeGreaterThan(0.0f);

        var dirToSheep = Vector3.Normalize(new Vector3((float)(sheep.FeetPosition.X - 8.5), 0.0f, (float)(sheep.FeetPosition.Z - 8.5)));
        mobs.TryShearAlongRay(new Vec3d(8.5, 64.6, 8.5), dirToSheep, 4.0, registries.Items, out var wool).Should().BeTrue();
        wool.ItemId.Should().Be((ushort)registries.Items.GetId(BuiltInBlocks.WhiteWool));
        sheep.IsSheared.Should().BeTrue();

        var eye = new Vec3d(zombie.FeetPosition.X, zombie.FeetPosition.Y + 1.0, zombie.FeetPosition.Z - 1.5);
        List<ItemStack> drops = [];
        for (var swing = 0; swing < 4; swing++)
        {
            zombie.InvulnerabilityTimer = 0.0f;
            _ = mobs.TryAttackAlongRay(eye, new Vector3(0.0f, 0.0f, 1.0f), 4.0, weaponDamage: 8.0f, isCriticalStrike: true, registries.Items, out _, out drops);
            if (zombie.IsDead) break;
        }

        zombie.IsDead.Should().BeTrue();
        drops.Should().Contain(s => s.ItemId == (ushort)registries.Items.GetId(BuiltInItems.RottenFlesh));
    }

    [Fact]
    public async Task Held3DViewmodels3DMobsGuiAtlasCreativeFlightAnd64ChunkTicketStreaming_WorkCleanlyAsync()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var atlas = ProceduralAtlasGenerator.CreateDefault(blocks);
        var guiRgba = GuiTextureAtlasGenerator.Generate();
        guiRgba.Length.Should().Be(GuiTextureAtlasGenerator.AtlasWidth * GuiTextureAtlasGenerator.AtlasHeight * 4);

        var camera = new FirstPersonCamera { Position = new Vec3d(0.5, 70.0, 0.5) };
        camera.SetRotation(0.0f, 0.0f);

        var overlayVerts = new List<OverlayVertex>();
        var mobVisuals = new List<WorldMobVisual>();
        foreach (var mobType in Enum.GetValues<MobType>())
        {
            mobVisuals.Add(new WorldMobVisual(new Vec3d(2.0, 70.0, -4.0), 35.0f, 1.2f, (byte)mobType, 0.0f, 0.0f, false, 0.9f));
        }

        WorldMob3DMesher.AppendWorldMobs(overlayVerts, camera.Position, mobVisuals);
        overlayVerts.Count.Should().BeGreaterThan(200, "all 8 peaceful & hostile 3D mob types emit articulated box geometry");

        var hotbar = new ClientHotbar(blocks, registries.Items, atlas);
        var stateBlock = new FrameOverlayState(null, 0, hotbar.ActiveSlotColors, [])
        {
            Mode = UiOverlayMode.CraftingTableScreen,
            HeldIsBlock3D = true,
            HeldTopTile = atlas.GetTileIndex(BuiltInBlocks.GrassBlock, Direction.Up),
            HeldSideTile = atlas.GetTileIndex(BuiltInBlocks.GrassBlock, Direction.East),
            HeldFrontTile = atlas.GetTileIndex(BuiltInBlocks.GrassBlock, Direction.North),
            HeldBottomTile = atlas.GetTileIndex(BuiltInBlocks.GrassBlock, Direction.Down),
            HeldLightBrightness = 0.85f,
        };

        overlayVerts.Clear();
        HeldItem3DMesher.AppendHeldViewmodel(overlayVerts, stateBlock, atlas);
        overlayVerts.Count.Should().Be(36, "held 3D block cube emits 6 textured faces (36 vertices)");

        var stateSword = stateBlock with
        {
            HeldIsBlock3D = false,
            HeldIsToolOrWeapon = true,
            HeldItemSpriteTile = atlas.GetItemTileIndex(BuiltInItems.DiamondSword),
        };
        overlayVerts.Clear();
        HeldItem3DMesher.AppendHeldViewmodel(overlayVerts, stateSword, atlas);
        overlayVerts.Count.Should().BeGreaterThan(100, "16x16 pixel-extruded 3D sword emits front, back, and perimeter edge quads");

        var uiVerts = new List<TexturedUiVertex>();
        UiGeometryBuilder.BuildFrameQuads(uiVerts, camera, 1280, 720, stateBlock);
        uiVerts.Should().Contain(static v => v.Uv.X <= -3.0f, "CraftingTableScreen samples real uGuiAtlas container sheet");

        var cache = new ClientWorldCache(blocks, atlas);
        cache.UpsertColumn(new ChunkColumn(new ChunkPos(0, 0)));
        using var audio = new OpenAlAudioSubsystem(NullLogger<OpenAlAudioSubsystem>.Instance);
        var player = new ClientPlayerController(new Vec3d(0.5, 80.0, 0.5), blocks, registries.Items);
        player.ToggleCreativeFlight().Should().BeTrue();
        player.StepMovement(cache, blocks, audio, (0.0f, 0.0f, Jump: true, Sprint: false), 0.1, descend: false);
        player.FeetPosition.Y.Should().BeGreaterThan(80.05, "Creative Flight ascends smoothly on Space without gravity");

        var parsed64 = ClientBootstrap.ParseOptions(["--view-distance", "64"]);
        parsed64.ViewDistanceChunks.Should().Be(64);

        await using var manager = new ChunkManager(registries, new OverworldGenerator(77L, registries), new InMemoryChunkStorage(registries));
        manager.UpsertTicket(new ChunkTicket(1L, ChunkTicketType.Player, new ChunkPos(0, 0), Radius: 64));
        var loadedFirstBatch = await manager.ProcessTicketsAsync(maxChunksToLoad: 9, CancellationToken.None);
        loadedFirstBatch.Should().Be(9, "64-chunk ticket streams closest inner rings first without stalling");
    }
}
