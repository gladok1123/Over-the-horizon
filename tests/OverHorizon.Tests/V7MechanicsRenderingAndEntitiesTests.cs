using System.Numerics;
using FluentAssertions;
using OverHorizon.Client;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Crafting;
using OverHorizon.Gameplay.Inventory;
using OverHorizon.Gameplay.Mobs;
using OverHorizon.Gameplay.Projectiles;
using OverHorizon.Gameplay.Redstone;
using OverHorizon.Gameplay.Survival;
using OverHorizon.Gameplay.Vehicles;
using OverHorizon.Physics;
using OverHorizon.Rendering.Backends;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Meshing;
using OverHorizon.Rendering.Textures;
using OverHorizon.World.ChunkManagement;
using OverHorizon.World.Chunks;
using OverHorizon.World.Generation;
using OverHorizon.World.Serialization;
using OverHorizon.World.Storage;
using Xunit;

namespace OverHorizon.Tests;

public sealed class V7MechanicsRenderingAndEntitiesTests
{
    [Fact]
    public void Lantern3D_ExtendedPiston_AndRedBed_PlaceMeshAndDropAuthentically()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var items = registries.Items;
        var atlas = ProceduralAtlasGenerator.CreateDefault(blocks);

        var world = new Dictionary<BlockPos, BlockState>();
        BlockState Get(BlockPos p) => world.GetValueOrDefault(p, BlockState.Air);
        var stone = blocks.GetDefaultState(BuiltInBlocks.Stone);

        // 1. Ceiling-hung Lantern (ClickedFace = Down against solid ceiling at Y=70 -> hanging=true at Y=69)
        world[new BlockPos(2, 70, 2)] = stone;
        var hangPlaced = BlockAttachmentAndShapeRules.TryResolvePlacementState(
            blocks, Get, BuiltInBlocks.Lantern, new BlockPos(2, 69, 2), Direction.Down, Vector3.UnitZ, out var hangingLantern, out _);
        hangPlaced.Should().BeTrue();
        blocks.GetStateMetadata(hangingLantern).Properties["hanging"].Should().Be("true");
        BlockAttachmentAndShapeRules.IsStillSupported(blocks, Get, new BlockPos(2, 69, 2), hangingLantern).Should().BeTrue();

        // Removing ceiling support invalidates hanging lantern!
        world.Remove(new BlockPos(2, 70, 2));
        BlockAttachmentAndShapeRules.IsStillSupported(blocks, Get, new BlockPos(2, 69, 2), hangingLantern).Should().BeFalse();

        // 2. Floor-standing Lantern, Extended Sticky Piston, and 2-part RedBed emit rich 3D shaped geometry
        var col = new ChunkColumn(new ChunkPos(0, 0));
        _ = col.SetBlockState(2, 64, 2, stone, blocks);
        _ = col.SetBlockState(2, 65, 2, blocks.GetDefaultState(BuiltInBlocks.Lantern), blocks);
        _ = col.SetBlockState(4, 66, 2, hangingLantern, blocks);
        var extSticky = blocks.GetAllStates(BuiltInBlocks.StickyPiston)
            .First(m => m.Properties["facing"] == "north" && m.Properties["extended"] == "true").State;
        var stickyHead = blocks.GetAllStates(BuiltInBlocks.PistonHead)
            .First(m => m.Properties["facing"] == "north" && m.Properties["type"] == "sticky").State;
        var bedFoot = blocks.GetAllStates(BuiltInBlocks.RedBed)
            .First(m => m.Properties["facing"] == "north" && m.Properties["part"] == "foot").State;
        var bedHead = blocks.GetAllStates(BuiltInBlocks.RedBed)
            .First(m => m.Properties["facing"] == "north" && m.Properties["part"] == "head").State;

        _ = col.SetBlockState(6, 65, 2, extSticky, blocks);
        _ = col.SetBlockState(6, 65, 1, stickyHead, blocks);
        _ = col.SetBlockState(9, 65, 4, bedFoot, blocks);
        _ = col.SetBlockState(9, 65, 3, bedHead, blocks);

        var mesh = GreedySectionMesher.MeshSection(new ChunkNeighborhood(col.CreateSnapshot()), sectionIndex: 8, blocks, atlas);
        mesh.OpaqueVertices.Length.Should().BeGreaterThan(120, "3D Lantern + chain, shortened Extended Piston + Head, and 2-part RedBed with legs emit shaped geometry");

        // RedBed drops 1 RedBed item when broken cleanly
        var bedDrop = BlockDropResolver.ResolveDrop(BuiltInBlocks.RedBed, heldTool: null, items);
        bedDrop.IsEmpty.Should().BeFalse();
        bedDrop.Count.Should().Be(1);
        items.GetById(bedDrop.ItemId)!.Key.Should().Be(BuiltInBlocks.RedBed);
    }

    [Fact]
    public void WaterSlopeSearch_FlowsTowardNearbyDropAndAvoidsFloatingShelves()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var atlas = ProceduralAtlasGenerator.CreateDefault(blocks);

        var cw = new FlatTestCollisionWorld();
        cw.FillSolid(new BlockPos(-5, 63, -5), new BlockPos(5, 63, 5));
        // Carve a 1-block hole in the floor at (+2, 63, 0) — 2 blocks East of water source at (0, 64, 0)!
        cw.SetSolid(new BlockPos(2, 63, 0), false);
        cw.SetFluid(new BlockPos(0, 64, 0), 0);

        var step = FluidSimulator.EvaluateStep(cw, [new BlockPos(0, 64, 0)]);
        // Water at (0, 64, 0) must prefer flowing East toward the drop at (+2, 64, 0) instead of spreading West/North/South!
        step.Should().Contain(new FluidChange(new BlockPos(1, 64, 0), NewLevel: 1));
        step.Should().NotContain(c => c.Position == new BlockPos(-1, 64, 0));

        // Sloped flowing water (level 1 -> level 2) emits smooth corner-averaged translucent geometry
        var col = new ChunkColumn(new ChunkPos(0, 0));
        var water1 = blocks.TryGetStateWithProperty(BuiltInBlocks.Water, "level", "1", out var w1) ? w1 : blocks.GetDefaultState(BuiltInBlocks.Water);
        var water2 = blocks.TryGetStateWithProperty(BuiltInBlocks.Water, "level", "2", out var w2) ? w2 : blocks.GetDefaultState(BuiltInBlocks.Water);
        _ = col.SetBlockState(4, 64, 4, water1, blocks);
        _ = col.SetBlockState(5, 64, 4, water2, blocks);

        var mesh = GreedySectionMesher.MeshSection(new ChunkNeighborhood(col.CreateSnapshot()), sectionIndex: 8, blocks, atlas);
        mesh.TranslucentVertices.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void MobFallDamage_ChickenGlidesSafely_AndSkeletonShootsRangedArrows()
    {
        var cw = new FlatTestCollisionWorld();
        cw.FillSolid(new BlockPos(-16, 63, -16), new BlockPos(16, 63, 16));

        var mobs = new WorldMobManager();
        var highPig = mobs.SpawnMob(MobType.Pig, new Vec3d(2.5, 72.0, 2.5));
        var highChicken = mobs.SpawnMob(MobType.Chicken, new Vec3d(-2.5, 72.0, 2.5));
        _ = mobs.SpawnMob(MobType.Skeleton, new Vec3d(0.5, 64.0, -8.5));

        var landingEvents = new List<(Vec3d Position, MobType Type, float FallDamage, bool Died)>();
        var skeletonArrows = new List<(Vec3d Origin, Vec3d Velocity)>();

        for (var tick = 0; tick < 55; tick++)
        {
            _ = mobs.Step(
                cw,
                _ => (0, 0, false, true, true),
                new Vec3d(0.5, 64.0, 0.5),
                GameMode.Survival,
                playerArmorDefense: 0,
                skyBrightness: 0.1f,
                deltaSeconds: 0.05f,
                onSkeletonShootArrow: (origin, vel) => skeletonArrows.Add((origin, vel)),
                mobLandingHurtEvents: landingEvents);
        }

        // Pig dropped 8 blocks (> 3 safe blocks) -> took ~5 fall damage!
        highPig.Health.Should().BeLessThan(highPig.MaxHealth);
        landingEvents.Should().Contain(e => e.Type == MobType.Pig && e.FallDamage > 3.0f);

        // Chicken glided gently and took ZERO fall damage!
        highChicken.Health.Should().Be(highChicken.MaxHealth);
        landingEvents.Should().NotContain(e => e.Type == MobType.Chicken);

        // Hostile Skeleton at 9m range fired at least one ranged arrow toward the Survival player!
        skeletonArrows.Should().NotBeEmpty();
    }

    [Fact]
    public void BowAndBallisticArrows_ChargeStrikeMobsEmbedInBlocksAndRecoverOnWalkOver()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var items = registries.Items;

        ArrowProjectileManager.ComputeChargeNormalized(0.0f).Should().Be(0.0f);
        ArrowProjectileManager.ComputeChargeNormalized(0.5f).Should().BeGreaterThan(0.35f).And.BeLessThan(0.8f);
        ArrowProjectileManager.ComputeChargeNormalized(1.2f).Should().BeApproximately(1.0f, 1e-4f);

        var cw = new FlatTestCollisionWorld();
        cw.FillSolid(new BlockPos(-16, 63, -16), new BlockPos(16, 63, 16));
        cw.FillSolid(new BlockPos(-4, 64, -10), new BlockPos(4, 68, -10));

        var stone = blocks.GetDefaultState(BuiltInBlocks.Stone);
        BlockState GetBlock(BlockPos p) => cw.IsSolidBlock(p) ? stone : BlockState.Air;

        var mobs = new WorldMobManager();
        var targetZombie = mobs.SpawnMob(MobType.Zombie, new Vec3d(0.5, 64.0, -5.0));
        var arrows = new ArrowProjectileManager(blocks);
        var drops = new List<ItemStack>();
        var hitSounds = new List<Vec3d>();

        // 1. Fully charged arrow strikes Zombie!
        _ = arrows.SpawnPlayerArrow(new Vec3d(0.5, 65.5, 0.5), new Vector3(0.0f, 0.0f, -1.0f), chargeNormalized: 1.0f, canBePickedUp: true);
        for (var i = 0; i < 15; i++)
        {
            _ = arrows.Step(0.02, cw, GetBlock, mobs, items, new Vec3d(0.5, 64.0, 0.5), GameMode.Survival, drops, hitSounds);
        }

        targetZombie.Health.Should().BeLessThan(targetZombie.MaxHealth);
        hitSounds.Should().NotBeEmpty();

        // 2. Second arrow embeds into the Stone wall at Z = -10, then Survival player walks over and recovers it!
        mobs.Clear();
        var wallArrow = arrows.SpawnPlayerArrow(new Vec3d(2.5, 65.5, -6.0), new Vector3(0.0f, 0.0f, -1.0f), chargeNormalized: 0.9f, canBePickedUp: true);
        for (var i = 0; i < 20 && !wallArrow.IsStuckInBlock; i++)
        {
            _ = arrows.Step(0.02, cw, GetBlock, mobs, items, new Vec3d(2.5, 64.0, 0.5), GameMode.Survival, drops, hitSounds);
        }

        wallArrow.IsStuckInBlock.Should().BeTrue();
        drops.Clear();

        for (var i = 0; i < 15; i++)
        {
            _ = arrows.Step(0.05, cw, GetBlock, mobs, items, new Vec3d(wallArrow.Position.X, 64.0, wallArrow.Position.Z), GameMode.Survival, drops, hitSounds);
        }

        drops.Should().Contain(s => s.ItemId == items.GetId(BuiltInItems.Arrow) && s.Count == 1);
    }

    [Fact]
    public void CurvedAndPoweredRails_AutoConnectEnergizeAndAccelerateRideableMinecarts()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var items = registries.Items;
        var recipes = BuiltInRecipes.CreateFrozen(items);

        // Verify new v0.7.0 crafting recipes exist (PoweredRail, Minecart, Bow, Arrow)
        ushort[] expectedOutputs =
        [
            (ushort)items.GetId(BuiltInBlocks.PoweredRail),
            (ushort)items.GetId(BuiltInItems.Minecart),
            (ushort)items.GetId(BuiltInItems.Bow),
            (ushort)items.GetId(BuiltInItems.Arrow),
        ];
        foreach (var outId in expectedOutputs)
        {
            recipes.Entries.Should().Contain(r => r.Output.ItemId == outId);
        }

        var world = new Dictionary<BlockPos, BlockState>();
        var stone = blocks.GetDefaultState(BuiltInBlocks.Stone);
        for (var x = -4; x <= 12; x++)
        {
            for (var z = -4; z <= 4; z++)
            {
                world[new BlockPos(x, 63, z)] = stone;
            }
        }

        BlockState Get(BlockPos p) => world.GetValueOrDefault(p, BlockState.Air);
        void Set(BlockPos p, BlockState s) => world[p] = s;

        // 1. Corner auto-join: Rail at (0,64,1) + Rail at (1,64,0) -> Rail placed at (0,64,0) resolves to south_east corner!
        world[new BlockPos(0, 64, 1)] = blocks.GetDefaultState(BuiltInBlocks.Rail);
        world[new BlockPos(1, 64, 0)] = blocks.TryGetStateWithProperty(BuiltInBlocks.Rail, "shape", "east_west", out var ew) ? ew : blocks.GetDefaultState(BuiltInBlocks.Rail);
        var corner = RailPlacementRules.ResolveRailState(blocks, Get, BuiltInBlocks.Rail, new BlockPos(0, 64, 0), Vector3.UnitZ);
        blocks.GetStateMetadata(corner).Properties["shape"].Should().Be("south_east");

        // 2. Redstone Block powers a straight line of PoweredRails, and a Minecart accelerates & carries a mounted rider!
        world[new BlockPos(2, 64, 0)] = blocks.GetDefaultState(BuiltInBlocks.RedstoneBlock);
        for (var x = 3; x <= 8; x++)
        {
            world[new BlockPos(x, 64, 0)] = blocks.GetAllStates(BuiltInBlocks.PoweredRail)
                .First(m => m.Properties["shape"] == "east_west" && m.Properties["powered"] == "false").State;
        }

        var redstone = new RedstoneEngine(blocks);
        redstone.Step(0.05f, [new Vec3d(0.5, 64.0, 0.5)], Get, Set, [], []);
        blocks.GetStateMetadata(Get(new BlockPos(5, 64, 0))).Properties["powered"].Should().Be("true", "RedstoneBlock energizes adjacent PoweredRail chain");

        var cw = new FlatTestCollisionWorld();
        cw.FillSolid(new BlockPos(-4, 63, -4), new BlockPos(12, 63, 4));

        var carts = new MinecartManager(blocks);
        var cart = carts.TrySpawnOnRail(new BlockPos(3, 64, 0), Get);
        cart.Should().NotBeNull();

        var mounted = carts.TryMountAlongRay(new Vec3d(3.5, 65.6, 2.5), Vector3.Normalize(new Vector3(0.0f, -0.55f, -1.0f)), 4.2);
        mounted.Should().BeSameAs(cart);
        carts.RiddenMinecart.Should().BeSameAs(cart);

        for (var t = 0; t < 15; t++)
        {
            carts.Step(0.05, cw, Get, cart!.FeetPosition, Vector3.UnitX, riderForwardInput: 1.0f);
        }

        cart!.FeetPosition.X.Should().BeGreaterThan(4.2, "energized PoweredRail + rider W-steer accelerates Minecart along +X");
        carts.TryBreakAlongRay(new Vec3d(cart.FeetPosition.X, 65.6, 2.5), Vector3.Normalize(new Vector3(0.0f, -0.55f, -1.0f)), 4.2, out _).Should().BeTrue();
        carts.Minecarts.Should().BeEmpty();
    }

    [Fact]
    public async Task Armor3D_VehicleAndArrowMeshes_CreativeHotbarSync_SkyFarPlane_AndBatchChunkEdits_WorkEndToEndAsync()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var items = registries.Items;
        var recipes = BuiltInRecipes.CreateFrozen(items);
        var atlas = ProceduralAtlasGenerator.CreateDefault(blocks);
        var entityAtlas = EntityTextureAtlasGenerator.CreateDefault();

        // 1. Entity atlas preserves 512x256 dimensions and paints 3D Armor (cells 10..17), Minecart (18), and Arrow (19)
        entityAtlas.Width.Should().Be(512);
        entityAtlas.Height.Should().Be(256);

        var bareSteve = new List<OverlayVertex>();
        var armoredSteve = new List<OverlayVertex>();
        WorldMob3DMesher.AppendSteveModel(bareSteve, Matrix4x4.Identity, 0.0f, 0.0f, 0.0f, Vector4.One, default);
        WorldMob3DMesher.AppendSteveModel(
            armoredSteve, Matrix4x4.Identity, 0.0f, 0.0f, 0.0f, Vector4.One, new PlayerArmorVisual(4, 4, 4, 4));
        armoredSteve.Count.Should().BeGreaterThan(bareSteve.Count, "equipped Diamond Armor appends 3D helmet, chestplate+pauldrons, leggings, and boots");

        var entityVerts = new List<OverlayVertex>();
        WorldMob3DMesher.AppendMinecart(entityVerts, Vec3d.Zero, new MinecartVisual(1, new Vec3d(0.5, 64.0, -3.0), 90.0f, 0.0f, 0.0f));
        WorldMob3DMesher.AppendArrowProjectile(entityVerts, Vec3d.Zero, new ArrowProjectileVisual(2, new Vec3d(0.5, 65.0, -4.0), 0.0f, -10.0f, 0.2f, 1.0f, true));
        entityVerts.Count.Should().BeGreaterThan(180, "3D Minecart tub + 3D crossed-quad ballistic Arrow emit world geometry");

        // 2. Protruding empty hand + Creative Inventory <-> Hotbar synchronization + FarPlane >= 640 (no sky/cloud clipping!)
        var player = new ClientPlayerController(new Vec3d(0.5, 70.0, 0.5), blocks, items);
        player.SetGameMode(GameMode.Creative);
        var diamondSwordId = (ushort)items.GetId(BuiltInItems.DiamondSword);
        player.Hotbar.SurvivalInventory.SetSlot(0, new ItemStack(diamondSwordId, 1));
        player.Hotbar.SyncCreativeSlotFromInventory(0);
        player.Hotbar.RefreshSlotVisuals(GameMode.Creative);
        player.Hotbar.SelectedBlockDisplayName.Should().Contain("Diamond Sword", "Creative hotbar reflects items placed into slot 0 via inventory drag-and-drop");

        var menu = new ClientMenuController(new ClientOptions { ViewDistance = 5 }, UiOverlayMode.InGame, items, recipes);
        var cache = new ClientWorldCache(blocks, atlas);
        var overlay = menu.BuildOverlayState(new Vector2(1280, 720), player, cache, [], [], [], 6000L, 1.0f, new ClientChatHistory(), string.Empty);
        player.Camera.FarPlane.Should().BeGreaterThanOrEqualTo(640.0f, "FarPlane never clips Sun/Moon/Stars/Clouds even at low ViewDistance");
        overlay.ChunkFogDistance.Should().BeLessThan(player.Camera.FarPlane);

        var handVerts = new List<OverlayVertex>();
        HeldItem3DMesher.AppendHeldViewmodel(handVerts, overlay with { HeldIsEmpty = true, HeldIsToolOrWeapon = false, HeldIsBlock3D = false }, atlas);
        handVerts.Should().Contain(v => v.Position.Z < -0.35f && v.Position.Y > -0.65f, "protruding 3D empty right arm is clearly visible inside the viewport");

        // 3. Batch SetBlockStatesBatch applies multi-block TNT craters atomically without per-block lock churn
        var manager = new ChunkManager(registries, new OverworldGenerator(777L, registries), new InMemoryChunkStorage(registries));
        await using (manager.ConfigureAwait(false))
        {
            manager.UpsertTicket(new ChunkTicket(1L, ChunkTicketType.Spawn, new ChunkPos(0, 0), 1));
            _ = await manager.ProcessTicketsAsync(9, CancellationToken.None).ConfigureAwait(false);

            var batchEdits = new (BlockPos Position, BlockState NewState)[]
            {
                (new BlockPos(2, 60, 2), BlockState.Air),
                (new BlockPos(3, 60, 2), BlockState.Air),
                (new BlockPos(2, 60, 3), blocks.GetDefaultState(BuiltInBlocks.Cobblestone)),
            };
            manager.SetBlockStatesBatch(batchEdits).Should().Be(3);
            manager.TryGetSnapshot(new ChunkPos(0, 0), out var snap).Should().BeTrue();
            snap.GetBlockState(2, 60, 2).Should().Be(BlockState.Air);
            snap.GetBlockState(2, 60, 3).Should().Be(blocks.GetDefaultState(BuiltInBlocks.Cobblestone));
        }
    }

    private sealed class FlatTestCollisionWorld : IVoxelCollisionReader
    {
        private readonly HashSet<BlockPos> _solids = [];
        private readonly Dictionary<BlockPos, int> _fluids = [];

        public void SetSolid(BlockPos pos, bool solid)
        {
            if (solid) _ = _solids.Add(pos);
            else _ = _solids.Remove(pos);
        }

        public void FillSolid(BlockPos min, BlockPos max)
        {
            for (var x = min.X; x <= max.X; x++)
                for (var y = min.Y; y <= max.Y; y++)
                    for (var z = min.Z; z <= max.Z; z++)
                        _ = _solids.Add(new BlockPos(x, y, z));
        }

        public void SetFluid(BlockPos pos, int level)
        {
            if (level < 0) _ = _fluids.Remove(pos);
            else _fluids[pos] = level;
        }

        public bool IsColumnLoaded(BlockPos pos) => true;
        public bool IsSolidBlock(BlockPos pos) => _solids.Contains(pos);
        public int GetFluidLevelOrNegative(BlockPos pos) => _fluids.GetValueOrDefault(pos, -1);
    }
}
