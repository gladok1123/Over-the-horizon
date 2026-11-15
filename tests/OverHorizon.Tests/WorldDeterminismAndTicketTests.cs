using FluentAssertions;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.World.ChunkManagement;
using OverHorizon.World.Generation;
using OverHorizon.World.Serialization;
using Xunit;

namespace OverHorizon.Tests;

public sealed class WorldDeterminismAndTicketTests
{
    [Fact]
    public async Task ParallelWorldgenAndLighting_ProduceIdenticalWorldHashAcross100RunsAsync()
    {
        const long seed = 0x7777_8888_9999_AAAAL;
        const int runCount = 100;

        var registries = CoreRegistries.CreateDefault();
        ulong? baselineHash = null;

        for (var run = 0; run < runCount; run++)
        {
            var generator = new OverworldGenerator(seed, registries);
            var storage = new InMemoryChunkStorage(registries);
            var manager = new ChunkManager(registries, generator, storage);
            await using (manager.ConfigureAwait(false))
            {
                // Radius 1 = 3x3 = 9 chunks generated concurrently in parallel.
                manager.UpsertTicket(new ChunkTicket(OwnerId: 1L, ChunkTicketType.Spawn, new ChunkPos(0, 0), Radius: 1));
                var loaded = await manager.ProcessTicketsAsync(maxChunksToLoad: 16, CancellationToken.None).ConfigureAwait(false);
                loaded.Should().Be(9);

                var hash = manager.ComputeLoadedWorldHash();
                if (baselineHash is null)
                {
                    baselineHash = hash;
                }
                else
                {
                    hash.Should().Be(baselineHash.Value, $"parallel worldgen run #{run} must match baseline hash bit-for-bit");
                }
            }
        }
    }

    [Fact]
    public async Task ChunkManager_LoadsNearestChunksFirstAndPersistsDirtyChunksOnUnloadAsync()
    {
        var registries = CoreRegistries.CreateDefault();
        var generator = new OverworldGenerator(worldSeed: 42L, registries);
        var storage = new InMemoryChunkStorage(registries);

        var manager = new ChunkManager(registries, generator, storage);
        await using (manager.ConfigureAwait(false))
        {
            var center = new ChunkPos(4, -2);
            manager.UpsertTicket(new ChunkTicket(OwnerId: 10L, ChunkTicketType.Player, center, Radius: 2));

            // Loading 1 chunk must pick the exact center chunk (distance 0) first.
            var firstCount = await manager.ProcessTicketsAsync(maxChunksToLoad: 1, CancellationToken.None).ConfigureAwait(false);
            firstCount.Should().Be(1);
            manager.TryGetSnapshot(center, out var initialSnapshot).Should().BeTrue();

            // Mutate a block and verify Copy-on-Write snapshot isolation.
            var glowstone = registries.Blocks.GetDefaultState(BuiltInBlocks.Glowstone);
            var targetBlockPos = center.GetBlockPos(8, 150, 8);
            manager.SetBlockState(targetBlockPos, glowstone, tickNumber: 5).Should().BeTrue();

            initialSnapshot!.GetBlockState(8, 150, 8).Should().Be(BlockState.Air, "old snapshot is immutable");
            manager.TryGetSnapshot(center, out var updatedSnapshot).Should().BeTrue();
            updatedSnapshot!.GetBlockState(8, 150, 8).Should().Be(glowstone);

            // Removing the ticket unloads the dirty chunk and queues it to the async save worker.
            manager.RemoveTicket(10L).Should().BeTrue();
            _ = await manager.ProcessTicketsAsync(maxChunksToLoad: 1, CancellationToken.None).ConfigureAwait(false);
            manager.LoadedChunkCount.Should().Be(0);
        }

        // After DisposeAsync completes the save channel worker, the modified chunk is persisted in storage.
        storage.StoredChunkCount.Should().Be(1);
        var reloaded = await storage.LoadChunkAsync(new ChunkPos(4, -2), CancellationToken.None).ConfigureAwait(false);
        reloaded.Should().NotBeNull();
        reloaded!.GetBlockState(8, 150, 8).Should().Be(registries.Blocks.GetDefaultState(BuiltInBlocks.Glowstone));
    }
}
