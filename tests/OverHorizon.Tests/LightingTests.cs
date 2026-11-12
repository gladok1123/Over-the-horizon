using FluentAssertions;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.World.Lighting;
using OverHorizon.World.Storage;
using Xunit;

namespace OverHorizon.Tests;

public sealed class LightingTests
{
    [Fact]
    public void SkyLight_PropagatesUnderRoofOverhangWithDistanceAttenuation()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var stone = blocks.GetDefaultState(BuiltInBlocks.Stone);

        var chunk = new ChunkColumn(new ChunkPos(0, 0));

        // Solid floor at Y = 60 across the whole chunk.
        for (var z = 0; z < 16; z++)
        {
            for (var x = 0; x < 16; x++)
            {
                _ = chunk.SetBlockStateRaw(x, 60, z, stone);
            }
        }

        // Opaque roof at Y = 64 covering X in [0..8], leaving X in [9..15] open to the sky.
        for (var z = 0; z < 16; z++)
        {
            for (var x = 0; x <= 8; x++)
            {
                _ = chunk.SetBlockStateRaw(x, 64, z, stone);
            }
        }

        chunk.RecomputeHeightmaps(blocks);
        LightingEngine.InitializeChunkLight(chunk, blocks);

        // Open sky column at X = 9 has full SkyLight = 15.
        chunk.GetSkyLight(9, 62, 8).Should().Be(15);

        // Underneath roof at X = 8, 7, 6 light attenuates by 1 per horizontal step: 14, 13, 12.
        chunk.GetSkyLight(8, 62, 8).Should().Be(14);
        chunk.GetSkyLight(7, 62, 8).Should().Be(13);
        chunk.GetSkyLight(6, 62, 8).Should().Be(12);

        // Inside solid stone block SkyLight remains 0.
        chunk.GetSkyLight(8, 64, 8).Should().Be(0);
    }

    [Fact]
    public void BlockLight_EmitsFromGlowstoneAndAttenuatesFasterThroughWater()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var glowstone = blocks.GetDefaultState(BuiltInBlocks.Glowstone);
        var water = blocks.GetDefaultState(BuiltInBlocks.Water);

        var chunk = new ChunkColumn(new ChunkPos(0, 0));
        _ = chunk.SetBlockStateRaw(8, 0, 8, glowstone);
        _ = chunk.SetBlockStateRaw(9, 0, 8, water);
        _ = chunk.SetBlockStateRaw(10, 0, 8, water);

        chunk.RecomputeHeightmaps(blocks);
        LightingEngine.InitializeChunkLight(chunk, blocks);

        chunk.GetBlockLight(8, 0, 8).Should().Be(15);

        // In open air (-X direction): -1 per block -> 14, 13.
        chunk.GetBlockLight(7, 0, 8).Should().Be(14);
        chunk.GetBlockLight(6, 0, 8).Should().Be(13);

        // Through water (+X direction): -2 per water block -> 13, 11.
        chunk.GetBlockLight(9, 0, 8).Should().Be(13);
        chunk.GetBlockLight(10, 0, 8).Should().Be(11);
    }
}
