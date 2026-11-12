using System.Numerics;
using FluentAssertions;
using OverHorizon.Audio;
using OverHorizon.Client;
using OverHorizon.Core.Blocks;
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
    public void BuiltInBlocks_ContainsAllTwentyFiveCleanRoomBlocksWithDistinctAtlasTiles()
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
        blocks.GetDefaultState(BuiltInBlocks.MossyCobblestone).IsAir.Should().BeFalse();
        blocks.GetDefaultState(BuiltInBlocks.SnowBlock).IsAir.Should().BeFalse();
        blocks.GetDefaultState(BuiltInBlocks.CrimsonWool).IsAir.Should().BeFalse();

        var glassDef = blocks.GetRequired(BuiltInBlocks.Glass);
        glassDef.IsOpaque.Should().BeFalse();
        glassDef.LightOpacity.Should().Be(1);

        var atlas = ProceduralAtlasGenerator.CreateDefault(blocks);
        atlas.GetTileIndex(BuiltInBlocks.Bookshelf, Direction.North)
            .Should().NotBe(atlas.GetTileIndex(BuiltInBlocks.Bookshelf, Direction.Up));
        atlas.GetTileIndex(BuiltInBlocks.DiamondOre, Direction.North)
            .Should().NotBe(atlas.GetTileIndex(BuiltInBlocks.Stone, Direction.North));
    }

    [Fact]
    public void PixelFontGlyphs_GeneratesCrispBitmapAtlasAndSolidWhiteCell()
    {
        var rgba = PixelFontGlyphs.CreateRgbaAtlas();
        rgba.Length.Should().Be(PixelFontGlyphs.AtlasWidth * PixelFontGlyphs.AtlasHeight * 4);

        // Cell 127 (col 15, row 7) is the solid white rectangle used for UI panels.
        var whiteOffset = ((7 * 8 + 4) * PixelFontGlyphs.AtlasWidth + (15 * 8 + 4)) * 4;
        rgba[whiteOffset + 0].Should().Be(255);
        rgba[whiteOffset + 1].Should().Be(255);
        rgba[whiteOffset + 2].Should().Be(255);
        rgba[whiteOffset + 3].Should().Be(255);

        // '#' glyph (ASCII 35 -> col 3, row 2) must contain opaque white pixels.
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
    public void ClientHotbar_CreativePaletteEquipsAnyOfTwentyTwoBlocksIntoSelectedSlot()
    {
        var blocks = BuiltInBlocks.CreateFrozen();
        var hotbar = new ClientHotbar(blocks);

        ClientHotbar.PaletteCount.Should().Be(22);
        hotbar.SelectSlot(2);
        hotbar.EquipPaletteBlock(21); // Diamond Ore

        hotbar.SelectedBlockKey.Should().Be(BuiltInBlocks.DiamondOre);
        hotbar.SelectedBlockDisplayName.Should().Be("Diamond Ore");
        hotbar.ActiveSlotColors.Span[2].Should().Be(hotbar.SelectedBlockColor);
    }

    [Fact]
    public void UiGeometryBuilder_EmitsCSharpEditionTitleScreenAndProjectedFriendNametags()
    {
        var blocks = BuiltInBlocks.CreateFrozen();
        var hotbar = new ClientHotbar(blocks);
        var camera = new FirstPersonCamera { Position = new Vec3d(0.5, 75.0, 0.5) };
        camera.SetRotation(0.0f, 0.0f); // Looking along -Z

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
            SplashText = "100% Pure C# 13 & .NET 9!",
            Buttons =
            [
                new UiButtonItem("singleplayer", "Singleplayer", 430, 300, 420, 44, IsHovered: true),
                new UiButtonItem("open_multiplayer", "Multiplayer (Play With Friends)", 430, 356, 420, 44),
            ],
        };

        UiGeometryBuilder.BuildFrameQuads(vertices, camera, 1280, 720, titleState);
        (vertices.Count % 6).Should().Be(0);
        vertices.Count.Should().BeGreaterThan(300);

        // Verify InGame HUD + 3D projected friend nametag in front of camera (-Z) vs behind (+Z).
        var visibleFriend = new RemotePlayerAvatar(7, "Alex", new Vec3d(0.5, 74.0, -6.0), 0.0f, 0.0f, new Vector3(0.3f, 0.7f, 0.9f));
        var behindFriend = new RemotePlayerAvatar(8, "Behind", new Vec3d(0.5, 74.0, 8.0), 0.0f, 0.0f, new Vector3(0.9f, 0.4f, 0.3f));

        var withoutFriends = new FrameOverlayState(null, 0, hotbar.ActiveSlotColors, [])
        {
            Mode = UiOverlayMode.InGame,
            ActiveBlockDisplayName = "Cobblestone",
            RecentChatLines = ["<Alex> Building castle!"],
        };
        UiGeometryBuilder.BuildFrameQuads(vertices, camera, 1280, 720, withoutFriends);
        var baseHudCount = vertices.Count;

        var withBehindOnly = withoutFriends with { RemotePlayers = [behindFriend] };
        UiGeometryBuilder.BuildFrameQuads(vertices, camera, 1280, 720, withBehindOnly);
        vertices.Count.Should().Be(baseHudCount);

        var withVisibleFriend = withoutFriends with { RemotePlayers = [visibleFriend, behindFriend] };
        UiGeometryBuilder.BuildFrameQuads(vertices, camera, 1280, 720, withVisibleFriend);
        vertices.Count.Should().BeGreaterThan(baseHudCount);
    }

    [Fact]
    public void ClientPlayerController_HandSwingProgressAnimatesAndSettles()
    {
        var blocks = BuiltInBlocks.CreateFrozen();
        var atlas = ProceduralAtlasGenerator.CreateDefault(blocks);
        var cache = new ClientWorldCache(blocks, atlas);
        using var audio = new OpenAlAudioSubsystem();
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
