using FluentAssertions;
using OverHorizon.Core;
using OverHorizon.Core.Biomes;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Entities;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Ai;
using OverHorizon.Gameplay.Ecs.Components;
using OverHorizon.Gameplay.Spawning;
using OverHorizon.Physics;
using OverHorizon.Shared.Random;
using OverHorizon.World.ChunkManagement;
using OverHorizon.World.Generation;
using OverHorizon.World.Serialization;
using OverHorizon.World.Storage;
using Xunit;

namespace OverHorizon.Tests;

public sealed class PathfindingSpawningAndWorldGameplayTests
{
    [Fact]
    public void VoxelPathfinder_NavigatesAroundChasmAndClimbsOneBlockStep()
    {
        var world = new SimpleGridWorld();

        // Build solid floor Y = 63 across [-8..8] x [-8..8], except an 8-block lethal chasm at X = 2, Z in [-8..2].
        for (var x = -8; x <= 8; x++)
        {
            for (var z = -8; z <= 8; z++)
            {
                if (x == 2 && z <= 2)
                {
                    world.SetSolid(new BlockPos(x, 55, z));
                }
                else
                {
                    world.SetSolid(new BlockPos(x, 63, z));
                }
            }
        }

        // Add a 1-block step up at X = 4, Z = 4 so the path must detour around the chasm AND climb a 1-block step.
        world.SetSolid(new BlockPos(4, 64, 4));

        var waypoints = VoxelPathfinder.FindPath(
            world,
            start: new BlockPos(0, 64, 0),
            goal: new BlockPos(4, 65, 4));

        waypoints.Should().NotBeEmpty();
        waypoints[0].Should().Be(new BlockPos(0, 64, 0));
        waypoints[^1].Should().Be(new BlockPos(4, 65, 4));
        waypoints.Should().NotContain(static p => p.X == 2 && p.Z <= 2);
    }

    [Fact]
    public void SpawnRuleEvaluator_EnforcesLightBiomeAndClearanceRules()
    {
        var registries = CoreRegistries.CreateDefault();
        var column = new ChunkColumn(new ChunkPos(0, 0));

        var grass = registries.Blocks.GetDefaultState(BuiltInBlocks.GrassBlock);
        var stone = registries.Blocks.GetDefaultState(BuiltInBlocks.Stone);
        var water = registries.Blocks.GetDefaultState(BuiltInBlocks.Water);
        var plainsId = (ushort)registries.Biomes.GetId(BuiltInBiomes.Plains);
        var desertId = (ushort)registries.Biomes.GetId(BuiltInBiomes.Desert);

        // Bright plains surface at (2, 64, 2) over grass_block at (2, 63, 2).
        _ = column.SetBlockStateRaw(2, 63, 2, grass);
        column.GetSection(ChunkColumn.GetSectionIndex(64)).SetBiomeId(2 >> 2, (64 & 15) >> 2, 2 >> 2, plainsId);
        column.SetSkyLight(2, 64, 2, 15);

        // Dark underground cave at (6, 20, 6) over stone at (6, 19, 6).
        _ = column.SetBlockStateRaw(6, 19, 6, stone);
        column.GetSection(ChunkColumn.GetSectionIndex(20)).SetBiomeId(6 >> 2, (20 & 15) >> 2, 6 >> 2, plainsId);

        // Desert spot at (10, 64, 10) and underwater spot at (12, 64, 12).
        _ = column.SetBlockStateRaw(10, 63, 10, grass);
        column.GetSection(ChunkColumn.GetSectionIndex(64)).SetBiomeId(10 >> 2, (64 & 15) >> 2, 10 >> 2, desertId);
        column.SetSkyLight(10, 64, 10, 15);

        _ = column.SetBlockStateRaw(12, 63, 12, stone);
        _ = column.SetBlockStateRaw(12, 64, 12, water);

        var snapshot = column.CreateSnapshot();

        SpawnRuleEvaluator.CanSpawnAt(snapshot, registries, MobCategory.Passive, 2, 64, 2).Should().BeTrue();
        SpawnRuleEvaluator.CanSpawnAt(snapshot, registries, MobCategory.Hostile, 2, 64, 2).Should().BeFalse();

        SpawnRuleEvaluator.CanSpawnAt(snapshot, registries, MobCategory.Hostile, 6, 20, 6).Should().BeTrue();
        SpawnRuleEvaluator.CanSpawnAt(snapshot, registries, MobCategory.Passive, 6, 20, 6).Should().BeFalse();

        SpawnRuleEvaluator.CanSpawnAt(snapshot, registries, MobCategory.Passive, 10, 64, 10).Should().BeFalse();
        SpawnRuleEvaluator.CanSpawnAt(snapshot, registries, MobCategory.Hostile, 12, 64, 12).Should().BeFalse();
    }

    [Fact]
    public void GameplaySubsystem_ApplyDamage_DestroysEntityAtZeroHealth()
    {
        using var gameplay = new GameplaySubsystem(worldSeed: 123L);
        var id = gameplay.SpawnEntity(
            BuiltInEntities.NightStalker,
            new Vec3d(0.0, 70.0, 0.0),
            Vec3d.Zero,
            maxHealth: 20.0f);

        gameplay.ApplyDamage(id, 12.0f).Should().BeFalse();
        gameplay.EntityCount.Should().Be(1);

        gameplay.ApplyDamage(id, 8.0f).Should().BeTrue();
        gameplay.EntityCount.Should().Be(0);
    }

    [Fact]
    public async Task ParallelWorldPhysicsGameplay_100Runs_ProduceIdenticalStateHashAndTerrainLandingAsync()
    {
        const int runCount = 100;
        const int entityCount = 48;
        const int tickCount = 25;
        const long seed = 0x243F6A8885A308D3L;

        // Pre-generate the immutable 3x3 chunk region once via OverworldGenerator + ChunkManager,
        // then run 100 independent parallel ECS + Physics simulations over the world collision reader!
        var registries = CoreRegistries.CreateDefault();
        var generator = new OverworldGenerator(seed, registries);
        var storage = new InMemoryChunkStorage(registries);
        using var chunkManager = new ChunkManager(registries, generator, storage);

        chunkManager.UpsertTicket(new ChunkTicket(OwnerId: 1L, ChunkTicketType.Player, new ChunkPos(0, 0), Radius: 1));
        _ = await chunkManager.ProcessTicketsAsync(16, CancellationToken.None).ConfigureAwait(false);

        var collisionReader = new WorldCollisionReader(chunkManager, registries.Blocks);
        var physics = new PhysicsSubsystem();

        ulong? baselineHash = null;

        for (var run = 0; run < runCount; run++)
        {
            using var gameplay = new GameplaySubsystem(seed, collisionReader, physics, registries);
            var spawnRng = new Xoroshiro128PlusPlus(unchecked((ulong)(seed ^ 0x13579BDF2468ACE0L)));

            long firstEntityId = 0;
            for (var i = 0; i < entityCount; i++)
            {
                var x = (spawnRng.NextDouble() * 24.0) - 12.0;
                var z = (spawnRng.NextDouble() * 24.0) - 12.0;
                var colPos = new BlockPos((int)Math.Floor(x), 64, (int)Math.Floor(z));
                var surfaceY = chunkManager.TryGetSnapshot(colPos.ToChunkPos(), out var snap)
                    ? snap.GetHeight(HeightmapType.MotionBlocking, colPos.SectionLocalX, colPos.SectionLocalZ)
                    : 80;

                var pos = new Vec3d(x, surfaceY + 2.0, z);
                var brain = (i & 1) == 0
                    ? new AiBrainComponent(WanderSpeed: 3.5, DecisionIntervalTicks: 5)
                    : (AiBrainComponent?)null;

                var id = gameplay.SpawnEntity(
                    (i & 1) == 0 ? BuiltInEntities.PassiveGrazer : BuiltInEntities.NightStalker,
                    pos,
                    Vec3d.Zero,
                    maxHealth: 20.0f,
                    aiBrain: brain);

                if (i == 0)
                {
                    firstEntityId = id;
                }
            }

            for (var tick = 0; tick < tickCount; tick++)
            {
                await gameplay.TickAsync(tick, CancellationToken.None).ConfigureAwait(false);
            }

            // Confirm entities stay supported above solid terrain rather than falling into the void.
            var finalFirstPos = gameplay.TryGetEntityPosition(firstEntityId);
            finalFirstPos.Should().NotBeNull();
            finalFirstPos!.Value.Y.Should().BeGreaterThan(EngineConstants.WorldMinY + 20);

            var runHash = gameplay.ComputeStateHash();
            if (baselineHash is null)
            {
                baselineHash = runHash;
            }
            else
            {
                runHash.Should().Be(baselineHash.Value, $"simulation run #{run} must match run #0 bit-for-bit");
            }
        }
    }

    private sealed class SimpleGridWorld : IVoxelCollisionReader
    {
        private readonly HashSet<BlockPos> _solids = [];

        public void SetSolid(BlockPos pos) => _ = _solids.Add(pos);

        public bool IsColumnLoaded(BlockPos pos) => true;

        public bool IsSolidBlock(BlockPos pos) => _solids.Contains(pos);

        public int GetFluidLevelOrNegative(BlockPos pos) => -1;
    }
}
