using BenchmarkDotNet.Attributes;
using OverHorizon.Core.Entities;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Ai;
using OverHorizon.Gameplay.Ecs.Components;
using OverHorizon.Physics;
using OverHorizon.World.ChunkManagement;
using OverHorizon.World.Generation;
using OverHorizon.World.Serialization;
using OverHorizon.World.Storage;

namespace OverHorizon.Benchmarks;

[MemoryDiagnoser]
public class PhysicsAndGameplayBenchmarks
{
    private CoreRegistries _registries = null!;
    private ChunkManager _chunkManager = null!;
    private WorldCollisionReader _collisionReader = null!;
    private PhysicsSubsystem _physics = null!;
    private GameplaySubsystem _gameplay = null!;
    private BlockPos _pathStart;
    private BlockPos _pathGoal;

    [GlobalSetup]
    public async Task SetupAsync()
    {
        const long seed = 0x5F3759DF42424242L;
        _registries = CoreRegistries.CreateDefault();
        var generator = new OverworldGenerator(seed, _registries);
        var storage = new InMemoryChunkStorage(_registries);
        _chunkManager = new ChunkManager(_registries, generator, storage);
        _chunkManager.UpsertTicket(new ChunkTicket(OwnerId: 1L, ChunkTicketType.Player, new ChunkPos(0, 0), Radius: 2));
        _ = await _chunkManager.ProcessTicketsAsync(32, CancellationToken.None).ConfigureAwait(false);

        _collisionReader = new WorldCollisionReader(_chunkManager, _registries.Blocks);
        _physics = new PhysicsSubsystem();
        _gameplay = new GameplaySubsystem(seed, _collisionReader, _physics, _registries);

        var startY = SampleHeight(_chunkManager, 0, 0);
        var goalY = SampleHeight(_chunkManager, 12, 12);
        _pathStart = new BlockPos(0, startY, 0);
        _pathGoal = new BlockPos(12, goalY, 12);

        var brain = new AiBrainComponent(WanderSpeed: 3.5, DecisionIntervalTicks: 10);
        for (var i = 0; i < 500; i++)
        {
            var x = ((i % 25) - 12) * 1.5;
            var z = (((i / 25) % 20) - 10) * 1.5;
            var y = SampleHeight(_chunkManager, (int)Math.Floor(x), (int)Math.Floor(z)) + 1.5;
            _ = _gameplay.SpawnEntity(
                BuiltInEntities.NightStalker,
                new Vec3d(x, y, z),
                Vec3d.Zero,
                aiBrain: brain);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _gameplay.Dispose();
        _chunkManager.Dispose();
    }

    [Benchmark]
    public PhysicsStepResult SweptVoxelStepSingleEntity() =>
        _physics.StepEntity(_collisionReader, new Vec3d(0.5, 80.2, 0.5), new Vec3d(4.3, -6.0, 2.1), 0.6, 1.8, 0.05);

    [Benchmark]
    public int VoxelAStarPathfind12Blocks() =>
        VoxelPathfinder.FindPath(_collisionReader, _pathStart, _pathGoal).Count;

    [Benchmark]
    public async Task ParallelPhysicsGameplayTick500EntitiesAsync() =>
        await _gameplay.TickAsync(1, CancellationToken.None).ConfigureAwait(false);

    private static int SampleHeight(ChunkManager chunks, int blockX, int blockZ)
    {
        var pos = new BlockPos(blockX, 64, blockZ);
        return chunks.TryGetSnapshot(pos.ToChunkPos(), out var snap)
            ? snap.GetHeight(HeightmapType.MotionBlocking, pos.SectionLocalX, pos.SectionLocalZ)
            : 70;
    }
}
