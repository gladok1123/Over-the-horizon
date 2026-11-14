using System.IO.Compression;
using System.Numerics;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OverHorizon.Audio;
using OverHorizon.Client;
using OverHorizon.Core.Biomes;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Crafting;
using OverHorizon.Gameplay.Mobs;
using OverHorizon.Gameplay.Physics;
using OverHorizon.Gameplay.Redstone;
using OverHorizon.Gameplay.Survival;
using OverHorizon.Physics;
using OverHorizon.Rendering.Backends;
using OverHorizon.Rendering.Textures;
using OverHorizon.World.Generation;
using OverHorizon.World.Storage;
using Xunit;

namespace OverHorizon.Tests;

public sealed class V9AssetsPhysicsDoorsBiomesAndPerfTests
{
    [Fact]
    public void TwoLayerTintedItems_FenceIconAndNewBlocks_RenderAuthenticPixelsInAtlas()
    {
        var tempZip = Path.Combine(Path.GetTempPath(), $"oh-v9-pack-{Guid.NewGuid():N}.zip");
        try
        {
            using (var zip = ZipFile.Open(tempZip, ZipArchiveMode.Create))
            {
                // Base grayscale 16x16 PNGs + white center overlay 16x16 PNGs
                WriteTest16x16Png(zip, "assets/minecraft/textures/item/potion.png", centerRgba: (90, 90, 90, 255), borderRgba: (180, 180, 190, 255));
                WriteTest16x16Png(zip, "assets/minecraft/textures/item/potion_overlay.png", centerRgba: (255, 255, 255, 255), borderRgba: (0, 0, 0, 0));
                WriteTest16x16Png(zip, "assets/minecraft/textures/item/spawn_egg.png", centerRgba: (220, 220, 220, 255), borderRgba: (220, 220, 220, 255));
                WriteTest16x16Png(zip, "assets/minecraft/textures/item/spawn_egg_overlay.png", centerRgba: (255, 255, 255, 255), borderRgba: (0, 0, 0, 0));
            }

            var registries = CoreRegistries.CreateDefault();
            var atlas = ProceduralAtlasGenerator.Generate(registries.Blocks, tempZip);

            // All 5 potions must composite distinct liquid center colors over the glass bottle border
            var healPx = ReadTilePixel(atlas, atlas.GetItemTileIndex(BuiltInItems.HealingPotion), 8, 8);
            var swiftPx = ReadTilePixel(atlas, atlas.GetItemTileIndex(BuiltInItems.SwiftnessPotion), 8, 8);
            healPx.R.Should().BeGreaterThan(healPx.B);
            swiftPx.B.Should().BeGreaterThan(swiftPx.R);
            healPx.Should().NotBe(swiftPx);

            // Creeper Spawn Egg (green base + dark spots) vs Pig Spawn Egg (pink base + brown spots)
            var creeperBorder = ReadTilePixel(atlas, atlas.GetItemTileIndex(BuiltInItems.CreeperSpawnEgg), 2, 2);
            var pigBorder = ReadTilePixel(atlas, atlas.GetItemTileIndex(BuiltInItems.PigSpawnEgg), 2, 2);
            creeperBorder.G.Should().BeGreaterThan(creeperBorder.R);
            pigBorder.R.Should().BeGreaterThan(pigBorder.G);

            // OakFence inventory icon has transparent exterior corners and opaque post/rail silhouette
            var fenceIconTile = atlas.GetItemTileIndex(BuiltInBlocks.OakFence);
            ReadTilePixel(atlas, fenceIconTile, 0, 0).A.Should().Be(0);
            ReadTilePixel(atlas, fenceIconTile, 3, 5).A.Should().Be(255);

            // ClientHotbar equips the 3D-shaped Fence and the hand-held Lantern
            var hotbar = new ClientHotbar(registries.Blocks, registries.Items, atlas);
            hotbar.EquipPaletteEntryInSlot(ClientHotbar.FindCatalogIndex(BuiltInBlocks.OakFence), 0);
            hotbar.SelectSlot(0);
            hotbar.SelectedBlockKey.Should().Be(BuiltInBlocks.OakFence);
            hotbar.SelectedIsBlock3D.Should().BeFalse("a 1.5-block Fence renders as a shaped post/rail mesh, never a full cube");

            hotbar.EquipPaletteEntryInSlot(ClientHotbar.FindCatalogIndex(BuiltInBlocks.Lantern), 0);
            hotbar.SelectedBlockKey.Should().Be(BuiltInBlocks.Lantern);
            hotbar.SelectedIsTorchStyle.Should().BeTrue("a hand-held Lantern renders as a hanging torch-style 3D prop");
        }
        finally
        {
            if (File.Exists(tempZip))
            {
                File.Delete(tempZip);
            }
        }
    }

    [Fact]
    public void SoundBankAndBlockSoundProfile_CoverAll93CuesAndMaterialFamilies()
    {
        var allCues = Enum.GetValues<SoundCue>();
        allCues.Should().HaveCount(97);

        var bank = ProceduralSoundBank.CreateDefaultBank();
        bank.Should().HaveCount(97);

        Minecraft23w13aAssetSyncer.AssetManifest.Should().Contain("sounds/block/wooden_door/open1.ogg");
        Minecraft23w13aAssetSyncer.AssetManifest.Should().Contain("sounds/block/iron_door/open1.ogg");
        Minecraft23w13aAssetSyncer.AssetManifest.Should().Contain("sounds/block/amethyst/shimmer.ogg");
        Minecraft23w13aAssetSyncer.AssetManifest.Should().Contain("sounds/item/shovel/flatten1.ogg");
        Minecraft23w13aAssetSyncer.AssetManifest.Should().Contain("textures/item/potion_overlay.png");
        Minecraft23w13aAssetSyncer.AssetManifest.Should().Contain("textures/item/spawn_egg_overlay.png");

        BlockSoundProfile.ResolveBreakCue(BuiltInBlocks.Deepslate).Should().Be(SoundCue.DeepslateBreak);
        BlockSoundProfile.ResolveFootstepCue(BuiltInBlocks.Deepslate).Should().Be(SoundCue.FootstepDeepslate);
        BlockSoundProfile.ResolveBreakCue(BuiltInBlocks.AmethystBlock).Should().Be(SoundCue.AmethystBreak);
        BlockSoundProfile.ResolveFootstepCue(BuiltInBlocks.AmethystBlock).Should().Be(SoundCue.FootstepAmethyst);
        BlockSoundProfile.ResolveBreakCue(BuiltInBlocks.AmethystCluster).Should().Be(SoundCue.AmethystClusterBreak);
        BlockSoundProfile.ResolveBreakCue(BuiltInBlocks.Gravel).Should().Be(SoundCue.GravelBreak);
        BlockSoundProfile.ResolveBreakCue(BuiltInBlocks.SnowBlock).Should().Be(SoundCue.SnowBreak);
        BlockSoundProfile.ResolveBreakCue(BuiltInBlocks.WhiteWool).Should().Be(SoundCue.ClothBreak);

        using var headlessAudio = new OpenAlAudioSubsystem(NullLogger<OpenAlAudioSubsystem>.Instance);
        headlessAudio.PlayPositional(SoundCue.TntFuse, new Vec3d(4.0, 64.0, 4.0));
        headlessAudio.PlayPositional(SoundCue.TntFuse, new Vec3d(4.1, 64.0, 4.0));
        headlessAudio.ReloadSoundPack(null);
    }

    [Fact]
    public void SubVoxelCollisionShapes_EnforceOnePointFiveBlockFenceTrapdoorAndDirtPath()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var atlas = ProceduralAtlasGenerator.CreateDefault(blocks);
        var cache = new ClientWorldCache(blocks, atlas);

        var col = new ChunkColumn(new ChunkPos(0, 0));
        for (var z = 0; z < 16; z++)
        {
            for (var x = 0; x < 16; x++)
            {
                _ = col.SetBlockState(x, 63, z, blocks.GetDefaultState(BuiltInBlocks.Stone), blocks);
            }
        }

        // 1. OakFence at (4, 64, 4) has collision MaxY == 65.5 (1.5 blocks tall!)
        _ = col.SetBlockState(4, 64, 4, blocks.GetDefaultState(BuiltInBlocks.OakFence), blocks);

        // 2. DirtPath at (6, 64, 6) has collision MaxY == 64.9375 (15/16)
        _ = col.SetBlockState(6, 64, 6, blocks.GetDefaultState(BuiltInBlocks.DirtPath), blocks);

        // 3. Top Trapdoor closed vs open at (8, 65, 8)
        var topClosed = blocks.WithProperty(blocks.WithProperty(blocks.GetDefaultState(BuiltInBlocks.OakTrapdoor), "half", "top"), "open", "false");
        _ = col.SetBlockState(8, 65, 8, topClosed, blocks);

        cache.UpsertColumn(col);

        cache.TryGetCollisionBox(new BlockPos(4, 64, 4), out var fenceBox).Should().BeTrue();
        fenceBox.MaxY.Should().BeApproximately(65.5, 1e-5);

        cache.TryGetCollisionBox(new BlockPos(6, 64, 6), out var pathBox).Should().BeTrue();
        pathBox.MaxY.Should().BeApproximately(64.9375, 1e-5);

        cache.TryGetCollisionBox(new BlockPos(8, 65, 8), out var topTrapBox).Should().BeTrue();
        topTrapBox.MinY.Should().BeApproximately(65.8125, 1e-5);
        topTrapBox.MaxY.Should().BeApproximately(66.0, 1e-5);

        // A player at a 1.25m jump peak (Y 65.0..66.8) still collides horizontally against the 1.5m Fence!
        var atJumpPeak = new BoundingBox(4.2, 65.0, 3.1, 4.8, 66.8, 3.7);
        var intoFence = VoxelSweptCollider.MoveAndCollide(cache, atJumpPeak, new Vec3d(0.0, 0.0, 1.0));
        intoFence.HitHorizontal.Should().BeTrue("a 1.25m jump peak must still collide against a 1.5m Fence top");

        // Clearing the 65.5 Fence top (Y 65.6..67.4) lets the same player slide straight over it!
        var aboveFence = new BoundingBox(4.2, 65.6, 3.1, 4.8, 67.4, 3.7);
        var overFence = VoxelSweptCollider.MoveAndCollide(cache, aboveFence, new Vec3d(0.0, 0.0, 1.0));
        overFence.HitHorizontal.Should().BeFalse("a player above Y=65.5 clears the Fence top");

        // A closed top Trapdoor slab blocks the hatch, but the folded-open hatch is passable!
        var inHatch = new BoundingBox(8.2, 65.0, 8.1, 8.8, 66.0, 8.7);
        VoxelSweptCollider.IntersectsAnySolid(cache, inHatch).Should().BeTrue("a closed Trapdoor slab fills the top 3/16 of the cell");

        var openNorth = blocks.WithProperty(blocks.WithProperty(topClosed, "facing", "north"), "open", "true");
        cache.ApplyBlockUpdate(new BlockPos(8, 65, 8), openNorth).Should().BeTrue();
        VoxelSweptCollider.IntersectsAnySolid(cache, inHatch).Should().BeFalse("an open Trapdoor folds flat against its hinge wall");
    }

    [Fact]
    public void TwoBlockDoorsRedstoneAndBuddingAmethyst_SyncHalvesSmartHingesAndDrops()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var items = registries.Items;
        var world = new Dictionary<BlockPos, BlockState>();

        BlockState Get(BlockPos p) => world.TryGetValue(p, out var s) ? s : BlockState.Air;
        void Set(BlockPos p, BlockState s)
        {
            if (s.IsAir) world.Remove(p);
            else world[p] = s;
        }

        // Solid floor
        for (var x = -2; x <= 4; x++)
        {
            for (var z = -2; z <= 4; z++)
            {
                Set(new BlockPos(x, 63, z), blocks.GetDefaultState(BuiltInBlocks.Stone));
            }
        }

        // Place the first door at (0, 64, 0) facing south (+Z): no neighbouring doors -> left hinge!
        BlockAttachmentAndShapeRules.TryResolvePlacementState(
            blocks, Get, BuiltInBlocks.OakDoor, new BlockPos(0, 64, 0), Direction.Up, new Vector3(0, 0, 1),
            out var leftLower, out var leftUpper).Should().BeTrue();
        leftUpper.Should().NotBeNull();
        Set(new BlockPos(0, 64, 0), leftLower);
        Set(leftUpper!.Value.Pos, leftUpper.Value.State);
        blocks.GetStateMetadata(leftLower).Properties["facing"].Should().Be("south");
        blocks.GetStateMetadata(leftLower).Properties["hinge"].Should().Be("left");

        // A second door placed on the east neighbour's west side snaps to the mirrored right hinge!
        BlockAttachmentAndShapeRules.TryResolvePlacementState(
            blocks, Get, BuiltInBlocks.OakDoor, new BlockPos(-1, 64, 0), Direction.Up, new Vector3(0, 0, 1),
            out var rightLower, out var rightUpper).Should().BeTrue();
        rightUpper.Should().NotBeNull();
        blocks.GetStateMetadata(rightLower).Properties["hinge"].Should().Be("right");
        Set(new BlockPos(-1, 64, 0), rightLower);
        Set(rightUpper!.Value.Pos, rightUpper.Value.State);

        // Place IronDoor at (3, 64, 0) and power it with a RedstoneBlock -> BOTH lower & upper halves open!
        BlockAttachmentAndShapeRules.TryResolvePlacementState(
            blocks, Get, BuiltInBlocks.IronDoor, new BlockPos(3, 64, 0), Direction.Up, new Vector3(0, 0, 1),
            out var ironLower, out var ironUpper).Should().BeTrue();
        Set(new BlockPos(3, 64, 0), ironLower);
        Set(ironUpper!.Value.Pos, ironUpper.Value.State);
        Set(new BlockPos(3, 64, 1), blocks.GetDefaultState(BuiltInBlocks.RedstoneBlock));

        var redstone = new RedstoneEngine(blocks);
        redstone.NotifyBlockChanged(new BlockPos(3, 64, 1));
        var ignited = new List<BlockPos>();
        var sounds = new List<(BlockPos Pos, RedstoneSoundKind Sound)>();
        redstone.EvaluateImmediate(Get, Set, ignited, sounds);

        blocks.GetStateMetadata(Get(new BlockPos(3, 64, 0))).Properties["open"].Should().Be("true");
        blocks.GetStateMetadata(Get(new BlockPos(3, 65, 0))).Properties["open"].Should().Be("true");
        sounds.Should().Contain(s => s.Sound == RedstoneSoundKind.IronDoorOpen);

        // Drop rules: BuddingAmethyst drops nothing; AmethystCluster drops 4 shards with Pickaxe; DirtPath drops Dirt
        var ironPick = items.GetRequired(BuiltInItems.IronPickaxe);
        BlockDropResolver.ResolveDrop(BuiltInBlocks.BuddingAmethyst, ironPick, items).IsEmpty.Should().BeTrue();
        var clusterDrop = BlockDropResolver.ResolveDrop(BuiltInBlocks.AmethystCluster, ironPick, items);
        clusterDrop.ItemId.Should().Be((ushort)items.GetId(BuiltInItems.AmethystShard));
        clusterDrop.Count.Should().Be(4);
        BlockDropResolver.ResolveDrop(BuiltInBlocks.DirtPath, heldTool: null, items).ItemId
            .Should().Be((ushort)items.GetId(BuiltInBlocks.Dirt));

        // BuddingAmethyst grows an AmethystCluster in an adjacent air cell on random ticks!
        var atlas = ProceduralAtlasGenerator.CreateDefault(blocks);
        var cache = new ClientWorldCache(blocks, atlas);
        var ticker = new PrimedTntAndWorldTicker(blocks);
        var col = new ChunkColumn(new ChunkPos(0, 0));
        for (var z = 0; z < 16; z++)
        {
            for (var x = 0; x < 16; x++)
            {
                _ = col.SetBlockState(x, 30, z, blocks.GetDefaultState(BuiltInBlocks.BuddingAmethyst), blocks);
            }
        }

        var grewCluster = false;
        BlockState GetChunkBlock(BlockPos p) => col.GetBlockState(p.SectionLocalX, p.Y, p.SectionLocalZ);
        void ApplyEdit(BlockPos p, BlockState s)
        {
            if (p.X is < 0 or > 15 || p.Z is < 0 or > 15) return;
            _ = col.SetBlockState(p.SectionLocalX, p.Y, p.SectionLocalZ, s, blocks);
            if (blocks.GetStateMetadata(s).BlockKey == BuiltInBlocks.AmethystCluster)
            {
                grewCluster = true;
            }
        }

        var detonated = new List<Vec3d>();
        for (var i = 0; i < 2000 && !grewCluster; i++)
        {
            ticker.Step(0.1, cache, new Vec3d(8.5, 30.5, 8.5), GetChunkBlock, _ => ((byte)15, (byte)0), ApplyEdit, detonated);
        }

        grewCluster.Should().BeTrue("BuddingAmethyst must sprout AmethystCluster crystals on adjacent air faces");
    }

    [Fact]
    public void TenBiomesSkeletonLineOfSightThirdPersonCameraAndCreativeVoidDeath_BehaveAuthentically()
    {
        var registries = CoreRegistries.CreateDefault();
        registries.Biomes.Count.Should().Be(10);

        var sampler = new BiomeSampler(worldSeed: 20261112L, registries.Biomes);
        var discovered = new HashSet<ResourceKey>();
        for (var z = -4096; z <= 4096; z += 128)
        {
            for (var x = -4096; x <= 4096; x += 128)
            {
                discovered.Add(registries.Biomes.GetById(sampler.SampleBiomeId(x, z)).Key);
            }
        }

        discovered.Should().Contain(BuiltInBiomes.Plains);
        discovered.Count.Should().BeGreaterThanOrEqualTo(5, "multi-noise BiomeSampler should distribute diverse biomes across the Overworld");

        // Skeleton line-of-sight blocked by solid stone wall
        var atlas = ProceduralAtlasGenerator.CreateDefault(registries.Blocks);
        var cache = new ClientWorldCache(registries.Blocks, atlas);
        var col = new ChunkColumn(new ChunkPos(0, 0));
        for (var y = 64; y <= 67; y++)
        {
            _ = col.SetBlockState(5, y, 5, registries.Blocks.GetDefaultState(BuiltInBlocks.Stone), registries.Blocks);
        }

        cache.UpsertColumn(col);
        WorldMobManager.HasClearLineOfSight(cache, new Vec3d(2.5, 65.6, 5.5), new Vec3d(8.5, 65.6, 5.5)).Should().BeFalse();
        WorldMobManager.HasClearLineOfSight(cache, new Vec3d(2.5, 65.6, 2.5), new Vec3d(8.5, 65.6, 2.5)).Should().BeTrue();

        // F5 third-person camera cycle + Creative deep-Void (< -76) death
        var player = new ClientPlayerController(new Vec3d(8.5, 90.0, 8.5), registries.Blocks, registries.Items);
        player.CameraPerspectiveMode.Should().Be(0);
        player.CycleCameraPerspective().Should().Be(1);
        var backCam = player.BuildRenderCamera(cache);
        (backCam.Position - player.Camera.Position).Length.Should().BeGreaterThan(1.0);

        player.CycleCameraPerspective().Should().Be(2);
        var frontCam = player.BuildRenderCamera(cache);
        MathF.Abs(MathF.Abs(frontCam.YawDegrees - player.Camera.YawDegrees) - 180.0f).Should().BeLessThan(1.0f);
        player.CycleCameraPerspective().Should().Be(0);

        // Creative player dies in the deep Void below Y = -76!
        using var audio = new OpenAlAudioSubsystem(NullLogger<OpenAlAudioSubsystem>.Instance);
        player.SetGameMode(GameMode.Creative);
        player.ApplyAuthoritativePose(new Vec3d(8.5, -90.0, 8.5), 0.0f, 0.0f, updateSpawn: false);
        for (var i = 0; i < 200 && !player.IsDead; i++)
        {
            player.StepMovement(cache, registries.Blocks, audio, (0.0f, 0.0f, false, false), 0.1);
        }

        player.IsDead.Should().BeTrue("falling into the deep Void must kill the player like Java Edition");

        // Crafting recipes include v0.9 doors, iron trapdoor, glass bottle, and amethyst block
        var recipes = BuiltInRecipes.CreateFrozen(registries.Items);
        var oakDoorId = (ushort)registries.Items.GetId(BuiltInBlocks.OakDoor);
        var ironDoorId = (ushort)registries.Items.GetId(BuiltInBlocks.IronDoor);
        var ironTrapdoorId = (ushort)registries.Items.GetId(BuiltInBlocks.IronTrapdoor);
        var glassBottleId = (ushort)registries.Items.GetId(BuiltInItems.GlassBottle);
        var amethystBlockId = (ushort)registries.Items.GetId(BuiltInBlocks.AmethystBlock);

        recipes.Entries.Should().Contain(r => r.Output.ItemId == oakDoorId && r.Output.Count == 3);
        recipes.Entries.Should().Contain(r => r.Output.ItemId == ironDoorId && r.Output.Count == 3);
        recipes.Entries.Should().Contain(r => r.Output.ItemId == ironTrapdoorId);
        recipes.Entries.Should().Contain(r => r.Output.ItemId == glassBottleId && r.Output.Count == 3);
        recipes.Entries.Should().Contain(r => r.Output.ItemId == amethystBlockId);

        // Batch remesh updates multiple edited positions in a single pass
        using var headlessRenderer = new HeadlessRenderSubsystem();
        headlessRenderer.UploadAtlas(atlas);
        ReadOnlySpan<BlockPos> batch = [new BlockPos(2, 65, 2), new BlockPos(2, 66, 2)];
        cache.ApplyBlockUpdate(batch[0], registries.Blocks.GetDefaultState(BuiltInBlocks.OakPlanks)).Should().BeTrue();
        cache.ApplyBlockUpdate(batch[1], registries.Blocks.GetDefaultState(BuiltInBlocks.OakPlanks)).Should().BeTrue();
        cache.RemeshEditedBlocksBatchImmediate(batch, headlessRenderer);
        headlessRenderer.UploadedSectionCount.Should().BeGreaterThan(0);
    }

    private static (byte R, byte G, byte B, byte A) ReadTilePixel(TextureAtlasData atlas, int tileIndex, int px, int py)
    {
        var ox = (tileIndex % ProceduralAtlasGenerator.TilesPerRow) * 16;
        var oy = (tileIndex / ProceduralAtlasGenerator.TilesPerRow) * 16;
        var i = (((oy + py) * ProceduralAtlasGenerator.AtlasDimension) + ox + px) * 4;
        var s = atlas.RgbaPixels.Span;
        return (s[i + 0], s[i + 1], s[i + 2], s[i + 3]);
    }

    private static void WriteTest16x16Png(
        ZipArchive zip,
        string entryPath,
        (byte R, byte G, byte B, byte A) centerRgba,
        (byte R, byte G, byte B, byte A) borderRgba)
    {
        // Build uncompressed stored-DEFLATE 16x16 RGBA PNG with valid CRC32 & Adler32
        using var rawRows = new MemoryStream();
        for (var y = 0; y < 16; y++)
        {
            rawRows.WriteByte(0); // filter type 0 (None)
            for (var x = 0; x < 16; x++)
            {
                var c = (x is >= 4 and <= 11 && y is >= 4 and <= 11) ? centerRgba : borderRgba;
                rawRows.WriteByte(c.R);
                rawRows.WriteByte(c.G);
                rawRows.WriteByte(c.B);
                rawRows.WriteByte(c.A);
            }
        }

        var raw = rawRows.ToArray();
        using var zlib = new MemoryStream();
        zlib.WriteByte(0x78);
        zlib.WriteByte(0x01);
        zlib.WriteByte(0x01); // final stored block
        var len = (ushort)raw.Length;
        var nlen = (ushort)~len;
        zlib.WriteByte((byte)(len & 0xFF));
        zlib.WriteByte((byte)(len >> 8));
        zlib.WriteByte((byte)(nlen & 0xFF));
        zlib.WriteByte((byte)(nlen >> 8));
        zlib.Write(raw, 0, raw.Length);
        var adler = ComputeAdler32(raw);
        zlib.WriteByte((byte)(adler >> 24));
        zlib.WriteByte((byte)(adler >> 16));
        zlib.WriteByte((byte)(adler >> 8));
        zlib.WriteByte((byte)adler);

        using var png = new MemoryStream();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        WritePngChunk(png, "IHDR"u8, [0, 0, 0, 16, 0, 0, 0, 16, 8, 6, 0, 0, 0]);
        WritePngChunk(png, "IDAT"u8, zlib.ToArray());
        WritePngChunk(png, "IEND"u8, []);

        var entry = zip.CreateEntry(entryPath);
        using var es = entry.Open();
        var bytes = png.ToArray();
        es.Write(bytes, 0, bytes.Length);
    }

    private static void WritePngChunk(Stream dst, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> be = stackalloc byte[4];
        var len = (uint)data.Length;
        be[0] = (byte)(len >> 24);
        be[1] = (byte)(len >> 16);
        be[2] = (byte)(len >> 8);
        be[3] = (byte)len;
        dst.Write(be);
        dst.Write(type);
        dst.Write(data);

        var crc = 0xFFFFFFFFU;
        for (var i = 0; i < type.Length; i++) crc = StepCrc32(crc, type[i]);
        for (var i = 0; i < data.Length; i++) crc = StepCrc32(crc, data[i]);
        crc ^= 0xFFFFFFFFU;
        be[0] = (byte)(crc >> 24);
        be[1] = (byte)(crc >> 16);
        be[2] = (byte)(crc >> 8);
        be[3] = (byte)crc;
        dst.Write(be);
    }

    private static uint StepCrc32(uint crc, byte b)
    {
        crc ^= b;
        for (var bit = 0; bit < 8; bit++)
        {
            crc = (crc & 1U) != 0 ? (crc >> 1) ^ 0xEDB88320U : crc >> 1;
        }

        return crc;
    }

    private static uint ComputeAdler32(ReadOnlySpan<byte> data)
    {
        uint a = 1, b = 0;
        for (var i = 0; i < data.Length; i++)
        {
            a = (a + data[i]) % 65521U;
            b = (b + a) % 65521U;
        }

        return (b << 16) | a;
    }
}
