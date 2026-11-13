using System.IO.Compression;
using System.Numerics;
using FluentAssertions;
using OverHorizon.Audio;
using OverHorizon.Client;
using OverHorizon.Core;
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
            var healPx = ReadTilePixel(atlas, atlas.GetItemTileIndex(BuiltInItems.PotionHealing), 8, 8);
            var swiftPx = ReadTilePixel(atlas, atlas.GetItemTileIndex(BuiltInItems.PotionSwiftness), 8, 8);
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

            // ClientHotbar flags 3D Fence and 3D Lantern
            var hotbar = new ClientHotbar(registries.Blocks, atlas, registries.Items);
            hotbar.SetSlotCatalogEntry(0, ClientHotbar.FindCatalogIndex(BuiltInBlocks.OakFence));
            hotbar.SelectSlot(0);
            hotbar.SelectedIsFence.Should().BeTrue();

            hotbar.SetSlotCatalogEntry(0, ClientHotbar.FindCatalogIndex(BuiltInBlocks.Lantern));
            hotbar.SelectedIsLantern.Should().BeTrue();
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
    public void SoundBankAndBlockSoundProfile_CoverAll94CuesAndMaterialFamilies()
    {
        var allCues = Enum.GetValues<SoundCue>();
        allCues.Should().HaveCount(94);

        var bank = ProceduralSoundBank.CreateDefaultBank();
        bank.Should().HaveCount(94);

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

        using var headlessAudio = new OpenAlAudioSubsystem(Microsoft.Extensions.Logging.Abstractions.NullLogger<OpenAlAudioSubsystem>.Instance, forceHeadless: true);
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

        // 1. OakFence at (4, 64, 4) has collision Max.Y == 65.5 (1.5 blocks tall!)
        _ = col.SetBlockState(4, 64, 4, blocks.GetDefaultState(BuiltInBlocks.OakFence), blocks);

        // 2. DirtPath at (6, 64, 6) has collision Max.Y == 64.9375 (15/16)
        _ = col.SetBlockState(6, 64, 6, blocks.GetDefaultState(BuiltInBlocks.DirtPath), blocks);

        // 3. Top Trapdoor closed vs open at (8, 65, 8)
        var topClosed = blocks.WithProperty(blocks.WithProperty(blocks.GetDefaultState(BuiltInBlocks.OakTrapdoor), "half", "top"), "open", "false");
        _ = col.SetBlockState(8, 65, 8, topClosed, blocks);

        cache.UpsertChunk(col);

        cache.TryGetCollisionBox(new BlockPos(4, 64, 4), out var fenceBox).Should().BeTrue();
        fenceBox.Max.Y.Should().BeApproximately(65.5, 1e-5);

        cache.TryGetCollisionBox(new BlockPos(6, 64, 6), out var pathBox).Should().BeTrue();
        pathBox.Max.Y.Should().BeApproximately(64.9375, 1e-5);

        cache.TryGetCollisionBox(new BlockPos(8, 65, 8), out var topTrapBox).Should().BeTrue();
        topTrapBox.Min.Y.Should().BeApproximately(65.8125, 1e-5);
        topTrapBox.Max.Y.Should().BeApproximately(66.0, 1e-5);

        // Player jumping onto (4, 64, 4) from Y=64.0 up to peak Y=65.25 cannot stand on top of the 65.5 fence
        var playerAtJumpPeak = new BoundingBox(4.2, 65.25, 3.1, 4.8, 67.05, 3.7);
        var sweptIntoFence = VoxelSweptCollider.Sweep(cache, playerAtJumpPeak, new Vec3d(0.0, 0.0, 1.0));
        sweptIntoFence.HitZ.Should().BeTrue("a 1.25m jump peak at Y=65.25 must still collide horizontally against a 1.5m fence top at Y=65.5");

        // Open trapdoor stands flush against South/North edge so a centered 0.6m player climbs straight up through (8, 65, 8)!
        var openNorth = blocks.WithProperty(blocks.WithProperty(topClosed, "facing", "north"), "open", "true");
        _ = cache.SetBlockImmediate(new BlockPos(8, 65, 8), openNorth);
        var climbingPlayer = new BoundingBox(8.2, 64.0, 8.1, 8.8, 65.8, 8.7);
        var climbUp = VoxelSweptCollider.Sweep(cache, climbingPlayer, new Vec3d(0.0, 1.2, 0.0));
        climbUp.HitY.Should().BeFalse("player centered away from north hinge climbs freely through an open trapdoor hatch");
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

        // Place left door at (0, 64, 0) facing south (+Z), then adjacent door at (1, 64, 0) -> auto right hinge!
        BlockAttachmentAndShapeRules.TryResolvePlacementState(
            blocks, Get, BuiltInBlocks.OakDoor, new BlockPos(0, 64, 0), Direction.Up, new Vector3(0, 0, 1),
            out var leftLower, out var leftUpper).Should().BeTrue();
        leftUpper.Should().NotBeNull();
        Set(new BlockPos(0, 64, 0), leftLower);
        Set(leftUpper!.Value.Pos, leftUpper.Value.State);

        BlockAttachmentAndShapeRules.TryResolvePlacementState(
            blocks, Get, BuiltInBlocks.OakDoor, new BlockPos(1, 64, 0), Direction.Up, new Vector3(0, 0, 1),
            out var rightLower, out var rightUpper).Should().BeTrue();
        blocks.GetStateMetadata(leftLower).Properties["hinge"].Should().NotBe(blocks.GetStateMetadata(rightLower).Properties["hinge"]);
        Set(new BlockPos(1, 64, 0), rightLower);
        Set(rightUpper!.Value.Pos, rightUpper.Value.State);

        // Place IronDoor at (3, 64, 0) and power it with a RedstoneBlock -> BOTH lower & upper halves open!
        BlockAttachmentAndShapeRules.TryResolvePlacementState(
            blocks, Get, BuiltInBlocks.IronDoor, new BlockPos(3, 64, 0), Direction.Up, new Vector3(0, 0, 1),
            out var ironLower, out var ironUpper).Should().BeTrue();
        Set(new BlockPos(3, 64, 0), ironLower);
        Set(ironUpper!.Value.Pos, ironUpper.Value.State);
        Set(new BlockPos(3, 64, 1), blocks.GetDefaultState(BuiltInBlocks.RedstoneBlock));

        var redstone = new RedstoneEngine(blocks);
        _ = redstone.StepTick(Get, Set, [new BlockPos(3, 64, 1)], out var sounds);
        blocks.GetStateMetadata(Get(new BlockPos(3, 64, 0))).Properties["open"].Should().Be("true");
        blocks.GetStateMetadata(Get(new BlockPos(3, 65, 0))).Properties["open"].Should().Be("true");
        sounds.Should().Contain(s => s.SoundKind == 6); // IronDoorOpen

        // Drop rules: BuddingAmethyst drops nothing; AmethystCluster drops 4 shards with Pickaxe; DirtPath drops Dirt
        var ironPick = items.GetRequired(BuiltInItems.IronPickaxe);
        BlockDropResolver.ResolveDrop(BuiltInBlocks.BuddingAmethyst, ironPick, items).IsEmpty.Should().BeTrue();
        var clusterDrop = BlockDropResolver.ResolveDrop(BuiltInBlocks.AmethystCluster, ironPick, items);
        clusterDrop.ItemId.Should().Be((ushort)items.GetId(BuiltInItems.AmethystShard));
        clusterDrop.Count.Should().Be(4);
        BlockDropResolver.ResolveDrop(BuiltInBlocks.DirtPath, heldTool: null, items).ItemId
            .Should().Be((ushort)items.GetId(BuiltInBlocks.Dirt));

        // BuddingAmethyst grows an AmethystCluster in an adjacent air cell on random ticks!
        var ticker = new PrimedTntAndWorldTicker(blocks);
        var col = new ChunkColumn(new ChunkPos(0, 0));
        _ = col.SetBlockState(8, 30, 8, blocks.GetDefaultState(BuiltInBlocks.BuddingAmethyst), blocks);
        ChunkColumn? GetChunk(ChunkPos cp) => cp == col.Position ? col : null;
        void ApplyEdit(BlockPos p, BlockState s) => _ = col.SetBlockState(p.SectionLocalX, p.Y, p.SectionLocalZ, s, blocks);

        var grewCluster = false;
        for (var tick = 0; tick < 600 && !grewCluster; tick++)
        {
            ticker.RunRandomBlockTicks([new ChunkPos(0, 0)], GetChunk, ApplyEdit, samplesPerSection: 24);
            Direction[] dirs = [Direction.Up, Direction.Down, Direction.North, Direction.South, Direction.West, Direction.East];
            foreach (var d in dirs)
            {
                var np = new BlockPos(8, 30, 8).Offset(d);
                var st = col.GetBlockState(np.SectionLocalX, np.Y, np.SectionLocalZ);
                if (blocks.GetStateMetadata(st).BlockKey == BuiltInBlocks.AmethystCluster)
                {
                    grewCluster = true;
                    break;
                }
            }
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
        for (var z = -4096; z <= 4096; z += 64)
        {
            for (var x = -4096; x <= 4096; x += 64)
            {
                discovered.Add(registries.Biomes.GetById(sampler.SampleBiomeId(x, z)).Key);
            }
        }

        discovered.Should().Contain(BuiltInBiomes.Plains);
        discovered.Count.Should().BeGreaterThanOrEqualTo(7, "multi-noise BiomeSampler should distribute diverse biomes across the Overworld");

        // Skeleton line-of-sight blocked by solid stone wall
        var atlas = ProceduralAtlasGenerator.CreateDefault(registries.Blocks);
        var cache = new ClientWorldCache(registries.Blocks, atlas);
        var col = new ChunkColumn(new ChunkPos(0, 0));
        for (var y = 64; y <= 67; y++)
        {
            _ = col.SetBlockState(5, y, 5, registries.Blocks.GetDefaultState(BuiltInBlocks.Stone), registries.Blocks);
        }

        cache.UpsertChunk(col);
        WorldMobManager.HasClearLineOfSight(cache, new Vec3d(2.5, 65.6, 5.5), new Vec3d(8.5, 65.6, 5.5)).Should().BeFalse();
        WorldMobManager.HasClearLineOfSight(cache, new Vec3d(2.5, 65.6, 2.5), new Vec3d(8.5, 65.6, 2.5)).Should().BeTrue();

        // F5 third-person camera cycle + Creative deep-Void (< -64) death
        var player = new ClientPlayerController(new Vec3d(8.5, 70.0, 8.5), registries.Blocks, atlas, registries.Items);
        player.CameraPerspectiveMode.Should().Be(0);
        player.CycleCameraPerspective().Should().Be(1);
        var backCam = player.BuildRenderCamera(cache);
        (backCam.Position - player.Camera.Position).Length.Should().BeGreaterThan(1.0);

        player.CycleCameraPerspective().Should().Be(2);
        var frontCam = player.BuildRenderCamera(cache);
        MathF.Abs(MathF.Abs(frontCam.YawDegrees - player.Camera.YawDegrees) - 180.0f).Should().BeLessThan(1.0f);
        player.CycleCameraPerspective().Should().Be(0);

        // Creative player survives at Y = -60, but dies in deep Void below Y = -64!
        using var audio = new OpenAlAudioSubsystem(Microsoft.Extensions.Logging.Abstractions.NullLogger<OpenAlAudioSubsystem>.Instance, forceHeadless: true);
        player.SetMode(GameMode.Creative);
        player.ApplyAuthoritativePose(new Vec3d(8.5, -70.0, 8.5), 0.0f, 0.0f, updateSpawn: false);
        for (var i = 0; i < 20 && !player.IsDead; i++)
        {
            player.TickSurvivalVitals(0.30, cache, audio, isFlying: false);
        }

        player.IsDead.Should().BeTrue("falling below Y = -64 in Creative mode must kill the player like Java Edition");

        // Crafting recipes include v0.9 doors, iron trapdoor, glass bottle, and amethyst block
        var recipes = BuiltInRecipes.CreateFrozen(registries.Items);
        recipes.Entries.Should().Contain(r => r.Output.ItemId == registries.Items.GetId(BuiltInBlocks.OakDoor) && r.Output.Count == 3);
        recipes.Entries.Should().Contain(r => r.Output.ItemId == registries.Items.GetId(BuiltInBlocks.IronDoor) && r.Output.Count == 3);
        recipes.Entries.Should().Contain(r => r.Output.ItemId == registries.Items.GetId(BuiltInBlocks.IronTrapdoor));
        recipes.Entries.Should().Contain(r => r.Output.ItemId == registries.Items.GetId(BuiltInItems.GlassBottle) && r.Output.Count == 3);
        recipes.Entries.Should().Contain(r => r.Output.ItemId == registries.Items.GetId(BuiltInBlocks.AmethystBlock));

        // Batch remesh updates multiple edited positions in a single pass
        using var headlessRenderer = new HeadlessRenderSubsystem();
        headlessRenderer.Initialize();
        ReadOnlySpan<BlockPos> batch = [new BlockPos(2, 65, 2), new BlockPos(2, 66, 2)];
        _ = cache.SetBlockDataOnly(batch[0], registries.Blocks.GetDefaultState(BuiltInBlocks.OakPlanks));
        _ = cache.SetBlockDataOnly(batch[1], registries.Blocks.GetDefaultState(BuiltInBlocks.OakPlanks));
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
