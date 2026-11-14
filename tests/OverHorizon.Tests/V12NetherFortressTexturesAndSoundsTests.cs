using FluentAssertions;
using OverHorizon.Audio;
using OverHorizon.Client;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay.Crafting;
using OverHorizon.Rendering.Textures;
using OverHorizon.World.Generation;
using OverHorizon.World.Storage;
using Xunit;

namespace OverHorizon.Tests;

/// <summary>
/// Covers the <c>v0.12.0-alpha</c> Nether overhaul: authentic Nether block textures in the asset manifest,
/// the 512x512 atlas tile-addressing fix, the Netherite storage block, Nether Portal sound cues, and the
/// deterministic Nether Fortress structure generator.
/// </summary>
public sealed class V12NetherFortressTexturesAndSoundsTests
{
    [Fact]
    public void AssetManifest_SyncsAuthenticNetherBlockTexturesAndPortalSounds()
    {
        var manifest = Minecraft23w13aAssetSyncer.AssetManifest;

        manifest.Should().Contain("textures/block/netherrack.png");
        manifest.Should().Contain("textures/block/nether_portal.png");
        manifest.Should().Contain("textures/block/nether_bricks.png");
        manifest.Should().Contain("textures/block/ancient_debris_side.png");
        manifest.Should().Contain("textures/block/ancient_debris_top.png");
        manifest.Should().Contain("textures/block/netherite_block.png");
        manifest.Should().Contain("textures/block/lava_still.png");
        manifest.Should().Contain("textures/block/lava_flow.png");
        manifest.Should().Contain("textures/block/crimson_nylium_side.png");
        manifest.Should().Contain("textures/block/warped_nylium_side.png");
        manifest.Should().Contain("textures/block/shroomlight.png");
        manifest.Should().Contain("textures/block/magma.png");
        manifest.Should().Contain("textures/block/soul_sand.png");
        manifest.Should().Contain("textures/block/soul_soil.png");
        manifest.Should().Contain("textures/block/basalt_side.png");
        manifest.Should().Contain("textures/block/basalt_top.png");
        manifest.Should().Contain("textures/block/blackstone.png");
        manifest.Should().Contain("textures/block/nether_wart_block.png");
        manifest.Should().Contain("textures/block/warped_wart_block.png");

        manifest.Should().Contain("sounds/portal/portal.ogg");
        manifest.Should().Contain("sounds/portal/travel.ogg");
        manifest.Should().Contain("sounds/portal/trigger.ogg");

        // The sound manifest was split into a partial file: every historically synced .ogg must still be present.
        manifest.Should().Contain("sounds/liquid/splash.ogg");
        manifest.Should().Contain("sounds/ambient/underwater/underwater_ambience.ogg");
        manifest.Should().Contain("sounds/item/shovel/flatten1.ogg");
        manifest.Should().Contain("lang/en_us.json");
    }

    [Fact]
    public void ProceduralAtlas_PaintsEveryNetherTileAtIts32TilePerRowAddress()
    {
        var registries = CoreRegistries.CreateDefault();
        var atlas = ProceduralAtlasGenerator.Generate(registries.Blocks);
        var pixels = atlas.RgbaPixels.Span;
        var dim = ProceduralAtlasGenerator.AtlasDimension;
        var perRow = ProceduralAtlasGenerator.TilesPerRow;
        var tileSize = ProceduralAtlasGenerator.TileDimension;

        for (var tile = 256; tile <= 277; tile++)
        {
            var originX = (tile % perRow) * tileSize;
            var originY = (tile / perRow) * tileSize;
            var idx = (((originY + 8) * dim) + originX + 8) * 4;
            pixels[idx + 3].Should().BeGreaterThan(
                0,
                $"Nether atlas tile {tile} must be painted at row {tile / perRow} of a {perRow}-tile-wide atlas");
        }

        // Netherrack and the Nether Portal must resolve to their own dedicated tiles, never the custom noise range.
        atlas.GetTileIndex(BuiltInBlocks.Netherrack, Direction.Up).Should().Be(256);
        atlas.GetTileIndex(BuiltInBlocks.NetherPortal, Direction.North).Should().Be(268);
        atlas.GetTileIndex(BuiltInBlocks.NetheriteBlock, Direction.Up).Should().Be(277);
    }

    [Fact]
    public void NetheriteBlock_IsRegisteredWithACompressAndUnpackRecipePair()
    {
        var registries = CoreRegistries.CreateDefault();
        var blockState = registries.Blocks.GetDefaultState(BuiltInBlocks.NetheriteBlock);
        registries.Blocks.GetStateMetadata(blockState).BlockKey.Should().Be(BuiltInBlocks.NetheriteBlock);
        registries.Blocks.GetStateMetadata(blockState).Hardness.Should().BeGreaterThan(10.0f);

        var recipes = BuiltInRecipes.CreateFrozen(registries.Items);
        recipes.TryGet(ResourceKey.OverHorizon("netherite_block_compress"), out _).Should().BeTrue();
        recipes.TryGet(ResourceKey.OverHorizon("netherite_block_unpack"), out _).Should().BeTrue();
    }

    [Fact]
    public void SoundBank_ExposesTheThreeNetherPortalCues()
    {
        var clips = ProceduralSoundBank.CreateDefaultBank();

        clips.Should().ContainKey(SoundCue.PortalAmbient);
        clips.Should().ContainKey(SoundCue.PortalTravel);
        clips.Should().ContainKey(SoundCue.PortalTrigger);

        clips[SoundCue.PortalAmbient].Samples.Length.Should().BeGreaterThan(1000);
        clips[SoundCue.PortalTravel].Samples.Length.Should().BeGreaterThan(1000);
        clips[SoundCue.PortalTrigger].Samples.Length.Should().BeGreaterThan(1000);
    }

    [Fact]
    public void NetherFortressGenerator_BuildsBrickKeepCorridorsWartFarmSpawnerHallAndVault()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var fortress = new NetherFortressGenerator(20261112L, blocks);
        fortress.GetFortressOrigin(0, 0, out var hubX, out var hubZ, out var baseY);

        baseY.Should().BeGreaterThan(DimensionRules.NetherLavaSeaY);
        (baseY + 12).Should().BeLessThan(DimensionRules.NetherCeilingY - 2);

        var bricks = blocks.GetDefaultState(BuiltInBlocks.NetherBricks);
        var soulSand = blocks.GetDefaultState(BuiltInBlocks.SoulSand);
        var spawner = blocks.GetDefaultState(BuiltInBlocks.MonsterSpawner);
        var chest = blocks.GetDefaultState(BuiltInBlocks.Chest);
        var glowstone = blocks.GetDefaultState(BuiltInBlocks.Glowstone);

        var sawBricks = false;
        var sawSoulSand = false;
        var sawSpawner = false;
        var sawChest = false;
        var sawGlowstone = false;

        for (var cz = -3; cz <= 3; cz++)
        {
            for (var cx = -3; cx <= 3; cx++)
            {
                var chunk = new ChunkColumn(new ChunkPos((hubX >> 4) + cx, (hubZ >> 4) + cz));
                fortress.PopulateChunk(chunk);

                for (var lz = 0; lz < 16; lz++)
                {
                    for (var lx = 0; lx < 16; lx++)
                    {
                        for (var y = baseY - 1; y <= baseY + 12; y++)
                        {
                            var state = chunk.GetBlockState(lx, y, lz);
                            if (state == bricks) sawBricks = true;
                            else if (state == soulSand) sawSoulSand = true;
                            else if (state == spawner) sawSpawner = true;
                            else if (state == chest) sawChest = true;
                            else if (state == glowstone) sawGlowstone = true;
                        }
                    }
                }
            }
        }

        sawBricks.Should().BeTrue("the fortress is built from Nether Bricks");
        sawSoulSand.Should().BeTrue("the Nether Wart farm plants Soul Sand rows");
        sawSpawner.Should().BeTrue("the Blaze spawner hall holds a Monster Spawner");
        sawChest.Should().BeTrue("the treasure vault holds loot chests");
        sawGlowstone.Should().BeTrue("the keep and wings are lit with Glowstone");
    }

    [Fact]
    public void NetherFortressGenerator_IsDeterministicAndSeedDependent()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;

        string Fingerprint(long seed, int rx, int rz)
        {
            var fortress = new NetherFortressGenerator(seed, blocks);
            fortress.GetFortressOrigin(rx, rz, out var hubX, out var hubZ, out var baseY);
            var chunk = new ChunkColumn(new ChunkPos(hubX >> 4, hubZ >> 4));
            fortress.PopulateChunk(chunk);

            var hash = 1469598103934665603UL;
            for (var lz = 0; lz < 16; lz++)
            {
                for (var lx = 0; lx < 16; lx++)
                {
                    for (var y = baseY - 1; y <= baseY + 12; y++)
                    {
                        hash = (hash ^ chunk.GetBlockState(lx, y, lz).Id) * 1099511628211UL;
                    }
                }
            }

            return $"{hubX}:{hubZ}:{baseY}:{hash}";
        }

        Fingerprint(4242L, 0, 0).Should().Be(Fingerprint(4242L, 0, 0));
        Fingerprint(4242L, 0, 0).Should().NotBe(Fingerprint(4243L, 0, 0));
        Fingerprint(4242L, 1, 0).Should().NotBe(Fingerprint(4242L, 0, 0));
    }

    [Fact]
    public void NetherGenerator_StampsAFortressIntoGeneratedTerrain()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var fortress = new NetherFortressGenerator(20261112L, blocks);
        fortress.GetFortressOrigin(0, 0, out var hubX, out var hubZ, out var baseY);

        var generator = new NetherGenerator(20261112L, registries);
        var chunk = new ChunkColumn(new ChunkPos(hubX >> 4, hubZ >> 4));
        generator.GenerateChunk(chunk);

        var bricks = blocks.GetDefaultState(BuiltInBlocks.NetherBricks);
        var sawBricks = false;
        for (var lz = 0; lz < 16 && !sawBricks; lz++)
        {
            for (var lx = 0; lx < 16 && !sawBricks; lx++)
            {
                for (var y = baseY; y <= baseY + 12; y++)
                {
                    if (chunk.GetBlockState(lx, y, lz) == bricks)
                    {
                        sawBricks = true;
                        break;
                    }
                }
            }
        }

        sawBricks.Should().BeTrue("generated Nether terrain must contain the stamped fortress keep");

        // The fortress never punches through the bedrock floor or roof.
        registries.Blocks.GetStateMetadata(chunk.GetBlockState(0, DimensionRules.NetherFloorY, 0)).BlockKey
            .Should().Be(BuiltInBlocks.Bedrock);
        registries.Blocks.GetStateMetadata(chunk.GetBlockState(0, DimensionRules.NetherCeilingY - 1, 0)).BlockKey
            .Should().Be(BuiltInBlocks.Bedrock);
    }
}
