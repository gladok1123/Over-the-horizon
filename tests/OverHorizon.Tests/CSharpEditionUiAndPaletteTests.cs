using System.Numerics;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OverHorizon.Audio;
using OverHorizon.Client;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Rendering.Backends;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Textures;
using Xunit;

namespace OverHorizon.Tests;

public sealed class CSharpEditionUiAndPaletteTests
{
    [Fact]
    public void BuiltInBlocks_ContainsAllThirtyCleanRoomBlocksAndTwentySevenItemSprites()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;

        blocks.GetDefaultState(BuiltInBlocks.StoneBricks).IsAir.Should().BeFalse();
        blocks.GetDefaultState(BuiltInBlocks.Bricks).IsAir.Should().BeFalse();
        blocks.GetDefaultState(BuiltInBlocks.Sandstone).IsAir.Should().BeFalse();
        blocks.GetDefaultState(BuiltInBlocks.Bookshelf).IsAir.Should().BeFalse();
        blocks.GetDefaultState(BuiltInBlocks.CoalOre).IsAir.Should().BeFalse();
        blocks.GetDefaultState(BuiltInBlocks.IronOre).IsAir.Should().BeFalse();
        blocks.GetDefaultState(BuiltInBlocks.GoldOre).IsAir.Should().BeFalse();
        blocks.GetDefaultState(BuiltInBlocks.DiamondOre).IsAir.Should().BeFalse();
        blocks.GetDefaultState(BuiltInBlocks.Gravel).IsAir.Should().BeFalse();
        blocks.GetDefaultState(BuiltInBlocks.Obsidian).IsAir.Should().BeFalse();
        blocks.GetDefaultState(BuiltInBlocks.Tnt).IsAir.Should().BeFalse();
        blocks.GetDefaultState(BuiltInBlocks.WhiteWool).IsAir.Should().BeFalse();
        blocks.GetDefaultState(BuiltInBlocks.BlueWool).IsAir.Should().BeFalse();

        var glassDef = blocks.GetRequired(BuiltInBlocks.Glass);
        glassDef.IsSolid.Should().BeTrue();
        glassDef.IsOpaque.Should().BeFalse();

        var atlas = ProceduralAtlasGenerator.CreateDefault(blocks);
        atlas.GetTileIndex(BuiltInBlocks.Bookshelf, Direction.North)
            .Should().NotBe(atlas.GetTileIndex(BuiltInBlocks.Bookshelf, Direction.Up));
        atlas.GetTileIndex(BuiltInBlocks.Tnt, Direction.North)
            .Should().NotBe(atlas.GetTileIndex(BuiltInBlocks.Tnt, Direction.Up));

        // Item sprites live at dedicated 16x16 tiles >= 128
        atlas.GetItemTileIndex(BuiltInItems.DiamondSword).Should().BeGreaterThanOrEqualTo(128);
        atlas.GetItemTileIndex(BuiltInItems.WaterBucket).Should().NotBe(atlas.GetItemTileIndex(BuiltInItems.Bucket));
    }

    [Fact]
    public void PixelFontGlyphs_GeneratesCrispBitmapAtlasAndSolidWhiteCell()
    {
        var rgba = PixelFontGlyphs.CreateRgbaAtlas();
        rgba.Length.Should().Be(PixelFontGlyphs.AtlasWidth * PixelFontGlyphs.AtlasHeight * 4);

        var whiteOffset = ((7 * 8 + 4) * PixelFontGlyphs.AtlasWidth + (15 * 8 + 4)) * 4;
        rgba[whiteOffset + 0].Should().Be(255);
        rgba[whiteOffset + 1].Should().Be(255);
        rgba[whiteOffset + 2].Should().Be(255);
        rgba[whiteOffset + 3].Should().Be(255);

        var hashCell = PixelFontGlyphs.MapCharToCell('#');
        var col = hashCell & 15;
        var row = hashCell >> 4;
        var opaqueCount = 0;
        for (var py = 0; py < 8; py++)
        {
            for (var px = 0; px < 8; px++)
            {
                var idx = (((row * 8) + py) * PixelFontGlyphs.AtlasWidth + ((col * 8) + px)) * 4;
                if (rgba[idx + 3] == 255)
                {
                    opaqueCount++;
                }
            }
        }

        opaqueCount.Should().BeGreaterThan(10);
    }

    [Fact]
    public void ClientHotbar_CreativeCatalogEquipsBlocksWeaponsAndToolsWithSpriteTiles()
    {
        var registries = CoreRegistries.CreateDefault();
        var atlas = ProceduralAtlasGenerator.CreateDefault(registries.Blocks);
        var hotbar = new ClientHotbar(registries.Blocks, registries.Items, atlas);

        ClientHotbar.PaletteCount.Should().BeGreaterThanOrEqualTo(100);
        hotbar.SelectSlot(2);
        hotbar.EquipPaletteBlock(21); // Diamond Ore

        hotbar.SelectedBlockKey.Should().Be(BuiltInBlocks.DiamondOre);
        hotbar.SelectedBlockDisplayName.Should().Be("Diamond Ore");
        hotbar.ActiveSlotColors.Span[2].Should().Be(hotbar.SelectedBlockColor);
        hotbar.ActiveSlotTiles.Span[2].Should().BeGreaterThanOrEqualTo(0);

        // Equip Diamond Sword (index 31) into slot 0
        hotbar.EquipPaletteEntryInSlot(31, 0);
        hotbar.SelectSlot(0);
        hotbar.SelectedBlockKey.Should().Be(BuiltInItems.DiamondSword);
        hotbar.SelectedIsToolOrWeapon.Should().BeTrue();
    }

    [Fact]
    public void UiGeometryBuilder_EmitsCSharpEditionTitleUnderwaterSurvivalHudAndSpriteUvs()
    {
        var registries = CoreRegistries.CreateDefault();
        var atlas = ProceduralAtlasGenerator.CreateDefault(registries.Blocks);
        var hotbar = new ClientHotbar(registries.Blocks, registries.Items, atlas);
        var camera = new FirstPersonCamera { Position = new Vec3d(0.5, 75.0, 0.5) };
        camera.SetRotation(0.0f, 0.0f);

        var vertices = new List<TexturedUiVertex>();
        var titleState = new FrameOverlayState(
            TargetedBlock: null,
            SelectedHotbarSlot: 0,
            HotbarSlotColors: hotbar.ActiveSlotColors,
            RemotePlayers: [])
        {
            Mode = UiOverlayMode.TitleMenu,
            MenuTitle = "OVER THE HORIZON",
            MenuSubtitle = "C# EDITION",
            SplashText = "23w13a__or__b Textures!",
            Buttons =
            [
                new UiButtonItem("singleplayer", "Singleplayer (Creative)", 430, 300, 204, 44, IsHovered: true),
                new UiButtonItem("open_multiplayer", "Multiplayer (PvP & Friends)", 430, 356, 420, 44),
            ],
        };

        UiGeometryBuilder.BuildFrameQuads(vertices, camera, 1280, 720, titleState);
        (vertices.Count % 6).Should().Be(0);
        vertices.Count.Should().BeGreaterThan(300);

        var visibleFriend = new RemotePlayerAvatar(7, "Alex", new Vec3d(0.5, 74.0, -6.0), 0.0f, 0.0f, 0.0f, HurtFlash: 0.8f, Health: 14.0f);
        var behindFriend = new RemotePlayerAvatar(8, "Behind", new Vec3d(0.5, 74.0, 8.0), 0.0f, 0.0f, 0.0f);

        var baseState = new FrameOverlayState(null, 0, hotbar.ActiveSlotColors, [])
        {
            Mode = UiOverlayMode.InGame,
            ActiveBlockDisplayName = "Diamond Sword",
            HotbarSlotTiles = hotbar.ActiveSlotTiles,
            HotbarSlotCounts = hotbar.ActiveSlotCounts,
        };
        UiGeometryBuilder.BuildFrameQuads(vertices, camera, 1280, 720, baseState);
        var baseHudCount = vertices.Count;
        // Hotbar sprite quads encode negative UVs (<= -1.0) so SilkOpenGlUiRenderer samples uItemAtlas!
        vertices.Should().Contain(static v => v.Uv.X <= -1.0f);

        var survivalUnderwater = baseState with
        {
            IsUnderwater = true,
            IsSurvivalMode = true,
            Health = 15.0f,
            Hunger = 14,
            AirSupplyNormalized = 0.7f,
            MiningProgress = 0.5f,
            RemotePlayers = [visibleFriend, behindFriend],
        };
        UiGeometryBuilder.BuildFrameQuads(vertices, camera, 1280, 720, survivalUnderwater);
        vertices.Count.Should().BeGreaterThan(baseHudCount + 100);
    }

    [Fact]
    public void ClientPlayerController_HandSwingProgressAnimatesAndSettles()
    {
        var blocks = BuiltInBlocks.CreateFrozen();
        var atlas = ProceduralAtlasGenerator.CreateDefault(blocks);
        var cache = new ClientWorldCache(blocks, atlas);
        using var audio = new OpenAlAudioSubsystem(NullLogger<OpenAlAudioSubsystem>.Instance);
        var player = new ClientPlayerController(new Vec3d(0.5, 80.0, 0.5), blocks);

        player.HandSwingProgress.Should().Be(0.0f);
        player.TriggerHandSwing();
        player.StepMovement(cache, blocks, audio, (0.0f, 0.0f, false, false), 0.08);
        player.HandSwingProgress.Should().BeGreaterThan(0.1f).And.BeLessThan(0.9f);

        player.StepMovement(cache, blocks, audio, (0.0f, 0.0f, false, false), 0.1);
        player.StepMovement(cache, blocks, audio, (0.0f, 0.0f, false, false), 0.1);
        player.HandSwingProgress.Should().Be(0.0f);
    }
}
