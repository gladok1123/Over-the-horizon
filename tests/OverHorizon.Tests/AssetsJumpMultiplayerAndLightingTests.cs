using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OverHorizon.Audio;
using OverHorizon.Client;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Rendering.Backends;
using OverHorizon.Rendering.Textures;
using OverHorizon.World.Lighting;
using OverHorizon.World.Storage;
using Xunit;

namespace OverHorizon.Tests;

public sealed class AssetsJumpMultiplayerAndLightingTests
{
    [Fact]
    public void PngImageDecoder_And_ResourcePackLoader_LoadBlockPngAndLangFromZipArchive()
    {
        var tempZip = Path.Combine(Path.GetTempPath(), $"oh-pack-{Guid.NewGuid():N}.zip");
        try
        {
            var customPng = BuildMinimal16x16RgbaPng(42, 180, 235, 255);
            using (var fs = File.Create(tempZip))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                var texEntry = zip.CreateEntry("minecraft-assets-1.21.4/assets/minecraft/textures/block/diamond_ore.png");
                using (var s = texEntry.Open())
                {
                    s.Write(customPng);
                }

                var langEntry = zip.CreateEntry("minecraft-assets-1.21.4/assets/minecraft/lang/ru_ru.json");
                using (var s = langEntry.Open())
                {
                    s.Write(Encoding.UTF8.GetBytes("{\"block.minecraft.diamond_ore\":\"Алмазная руда\"}"));
                }
            }

            using var loader = ResourcePackLoader.Discover(tempZip);
            loader.LoadedBlockTextureCount.Should().BeGreaterThanOrEqualTo(1);
            loader.Translations.Should().ContainKey("block.minecraft.diamond_ore")
                .WhoseValue.Should().Be("Алмазная руда");

            var blocks = BuiltInBlocks.CreateFrozen();
            var atlas = ProceduralAtlasGenerator.Generate(blocks, tempZip);
            var diamondTile = atlas.GetTileIndex(BuiltInBlocks.DiamondOre, Direction.North);
            var tileX = (diamondTile % ProceduralAtlasGenerator.TilesPerRow) * 16;
            var tileY = (diamondTile / ProceduralAtlasGenerator.TilesPerRow) * 16;
            var pixelOffset = (((tileY + 8) * ProceduralAtlasGenerator.AtlasDimension) + (tileX + 8)) * 4;

            var span = atlas.RgbaPixels.Span;
            span[pixelOffset + 0].Should().Be(42);
            span[pixelOffset + 1].Should().Be(180);
            span[pixelOffset + 2].Should().Be(235);
            span[pixelOffset + 3].Should().Be(255);
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
    public void WavPcmDecoder_DecodesRiffWaveMonoSamples()
    {
        var wav = BuildMinimal16BitMonoWav(22_050, [0, 12_000, -12_000, 24_000]);
        WavPcmDecoder.TryDecode(SoundCue.BlockPlace, wav, out var clip).Should().BeTrue();
        clip.SampleRate.Should().Be(22_050);
        clip.Samples.ToArray().Should().Equal(0, 12_000, -12_000, 24_000);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(144)]
    public void ClientPlayerController_JumpClearsOneBlockHeightAt60And144Fps(int fps)
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var atlas = ProceduralAtlasGenerator.CreateDefault(blocks);
        var cache = new ClientWorldCache(blocks, atlas);
        var stone = blocks.GetDefaultState(BuiltInBlocks.Stone);

        var col = new ChunkColumn(new ChunkPos(0, 0));
        for (var z = 0; z < 16; z++)
        {
            for (var x = 0; x < 16; x++)
            {
                _ = col.SetBlockState(x, 63, z, stone, blocks);
            }
        }

        // Place a 1-block step directly ahead along -Z at z=6 (occupying Y=[64, 65)).
        _ = col.SetBlockState(8, 64, 6, stone, blocks);
        cache.UpsertColumn(col);

        using var audio = new OpenAlAudioSubsystem(NullLogger<OpenAlAudioSubsystem>.Instance);
        var player = new ClientPlayerController(new Vec3d(8.5, 64.0, 7.2), blocks);
        var dt = 1.0 / fps;

        // Settle onto ground for 4 frames, then jump forward onto the 1-block ledge!
        for (var i = 0; i < 4; i++)
        {
            player.StepMovement(cache, blocks, audio, (0.0f, 0.0f, false, false), dt);
        }

        player.OnGround.Should().BeTrue();

        var peakY = player.FeetPosition.Y;
        for (var i = 0; i < fps; i++)
        {
            player.StepMovement(cache, blocks, audio, (1.0f, 0.0f, i == 0, false), dt);
            peakY = Math.Max(peakY, player.FeetPosition.Y);
        }

        // Peak jump height above Y=64.0 must exceed 1.20 blocks (easily clearing a 1.0-block step!).
        (peakY - 64.0).Should().BeGreaterThan(1.20);
        player.FeetPosition.Y.Should().BeGreaterThanOrEqualTo(65.0 - 1e-3);
    }

    [Fact]
    public async Task CrossChunkLighting_And_VramEviction_EliminateBorderSeamsAndFreeDistantMeshesAsync()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var atlas = ProceduralAtlasGenerator.CreateDefault(blocks);
        var cache = new ClientWorldCache(blocks, atlas);
        using var renderer = new HeadlessRenderSubsystem();
        renderer.UploadAtlas(atlas);

        var westCol = new ChunkColumn(new ChunkPos(0, 0));
        var eastCol = new ChunkColumn(new ChunkPos(1, 0));
        var farCol = new ChunkColumn(new ChunkPos(12, 12));

        var stone = blocks.GetDefaultState(BuiltInBlocks.Stone);
        var glowstone = blocks.GetDefaultState(BuiltInBlocks.Glowstone);

        _ = westCol.SetBlockState(15, 70, 8, glowstone, blocks);
        _ = farCol.SetBlockState(4, 70, 4, stone, blocks);
        LightingEngine.InitializeChunkLight(westCol, blocks);
        LightingEngine.InitializeChunkLight(eastCol, blocks);
        LightingEngine.InitializeChunkLight(farCol, blocks);

        cache.UpsertColumn(westCol);
        cache.UpsertColumn(eastCol);
        cache.UpsertColumn(farCol);

        // Glowstone at (chunk 0, localX=15) must illuminate (chunk 1, localX=0) to BlockLight == 14!
        cache.GetLight(new BlockPos(16, 70, 8)).BlockLight.Should().Be(14);

        _ = await cache.RebuildDirtyMeshesAsync(renderer, maxColumns: 8);
        renderer.UploadedSectionCount.Should().Be(2);

        // Evicting outside radius 3 from (0,0) unloads (12,12) and immediately removes its mesh from the renderer!
        var evicted = cache.EvictOutOfRangeChunks(new ChunkPos(0, 0), keepRadius: 3, renderer);
        evicted.Should().Be(1);
        renderer.UploadedSectionCount.Should().Be(1);
    }

    private static byte[] BuildMinimal16x16RgbaPng(byte r, byte g, byte b, byte a)
    {
        using var ms = new MemoryStream();
        ms.Write([137, 80, 78, 71, 13, 10, 26, 10]);

        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr[..4], 16);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.Slice(4, 4), 16);
        ihdr[8] = 8; // 8-bit
        ihdr[9] = 6; // RGBA
        WritePngChunk(ms, "IHDR"u8, ihdr);

        var raw = new byte[16 * (1 + (16 * 4))];
        for (var y = 0; y < 16; y++)
        {
            var row = y * (1 + 64);
            raw[row] = 0; // Filter None
            for (var x = 0; x < 16; x++)
            {
                var px = row + 1 + (x * 4);
                raw[px + 0] = r;
                raw[px + 1] = g;
                raw[px + 2] = b;
                raw[px + 3] = a;
            }
        }

        using var idatPayload = new MemoryStream();
        using (var zlib = new ZLibStream(idatPayload, CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        WritePngChunk(ms, "IDAT"u8, idatPayload.ToArray());
        WritePngChunk(ms, "IEND"u8, []);
        return ms.ToArray();
    }

    private static void WritePngChunk(Stream dst, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> header = stackalloc byte[8];
        BinaryPrimitives.WriteInt32BigEndian(header[..4], data.Length);
        type.CopyTo(header.Slice(4, 4));
        dst.Write(header);
        dst.Write(data);
        Span<byte> crc = [0, 0, 0, 0];
        dst.Write(crc);
    }

    private static byte[] BuildMinimal16BitMonoWav(int sampleRate, ReadOnlySpan<short> samples)
    {
        var dataBytes = samples.Length * 2;
        var total = 44 + dataBytes;
        var buf = new byte[total];
        "RIFF"u8.CopyTo(buf.AsSpan(0, 4));
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(4, 4), total - 8);
        "WAVE"u8.CopyTo(buf.AsSpan(8, 4));
        "fmt "u8.CopyTo(buf.AsSpan(12, 4));
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(16, 4), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(20, 2), 1); // PCM
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(22, 2), 1); // Mono
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(24, 4), sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(28, 4), sampleRate * 2);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(32, 2), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(34, 2), 16);
        "data"u8.CopyTo(buf.AsSpan(36, 4));
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(40, 4), dataBytes);
        for (var i = 0; i < samples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(buf.AsSpan(44 + (i * 2), 2), samples[i]);
        }

        return buf;
    }
}
