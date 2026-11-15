using FluentAssertions;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.World.Generation;
using OverHorizon.World.Serialization;
using OverHorizon.World.Storage;
using Xunit;

namespace OverHorizon.Tests;

public sealed class ChunkSerializationAndIoTests
{
    [Fact]
    public void ChunkNbtSerializer_PreservesExactChunkHashIncludingBlockPropertiesAndLight()
    {
        var registries = CoreRegistries.CreateDefault();
        var generator = new OverworldGenerator(worldSeed: 123456789L, registries);

        var original = new ChunkColumn(new ChunkPos(-3, 7));
        generator.GenerateChunk(original);

        // Place a non-default block state property (snowy grass + horizontal X-axis log) to test property round-trip.
        var snowyGrass = registries.Blocks.WithProperty(
            registries.Blocks.GetDefaultState(BuiltInBlocks.GrassBlock),
            "snowy",
            "true");
        var xAxisLog = registries.Blocks.WithProperty(
            registries.Blocks.GetDefaultState(BuiltInBlocks.OakLog),
            "axis",
            "x");

        _ = original.SetBlockState(4, 140, 4, snowyGrass, registries.Blocks);
        _ = original.SetBlockState(5, 140, 4, xAxisLog, registries.Blocks);
        original.LastModifiedTick = 77L;

        var expectedHash = original.ComputeDeterministicHash();
        var bytes = ChunkNbtSerializer.SerializeToBytes(original, registries);

        var restored = ChunkNbtSerializer.Deserialize(bytes, registries);
        restored.Position.Should().Be(original.Position);
        restored.LastModifiedTick.Should().Be(77L);
        restored.GetBlockState(4, 140, 4).Should().Be(snowyGrass);
        restored.GetBlockState(5, 140, 4).Should().Be(xAxisLog);
        restored.ComputeDeterministicHash().Should().Be(expectedHash);
    }

    [Fact]
    public async Task FileChunkStorage_SavesAndLoadsChunkViaPipelinesAsync()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"overhorizon_test_{Guid.NewGuid():N}");
        try
        {
            var registries = CoreRegistries.CreateDefault();
            var generator = new OverworldGenerator(worldSeed: 999L, registries);
            var storage = new FileChunkStorage(tempDir, registries);

            var pos = new ChunkPos(2, -5);
            (await storage.LoadChunkAsync(pos, CancellationToken.None).ConfigureAwait(false)).Should().BeNull();

            var chunk = new ChunkColumn(pos);
            generator.GenerateChunk(chunk);
            var expectedHash = chunk.ComputeDeterministicHash();

            await storage.SaveChunkAsync(chunk, CancellationToken.None).ConfigureAwait(false);

            var loaded = await storage.LoadChunkAsync(pos, CancellationToken.None).ConfigureAwait(false);
            loaded.Should().NotBeNull();
            loaded!.ComputeDeterministicHash().Should().Be(expectedHash);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
