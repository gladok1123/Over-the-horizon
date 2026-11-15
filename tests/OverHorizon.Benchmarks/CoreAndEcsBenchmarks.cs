using System.Buffers;
using BenchmarkDotNet.Attributes;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Entities;
using OverHorizon.Core.Math;
using OverHorizon.Core.Nbt;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Ecs.Components;

namespace OverHorizon.Benchmarks;

[MemoryDiagnoser]
public class CoreAndEcsBenchmarks
{
    private BlockRegistry _blocks = null!;
    private BlockState _waterDefault;
    private NbtCompoundTag _sampleCompound = null!;
    private ArrayBufferWriter<byte> _nbtBuffer = null!;
    private GameplaySubsystem _gameplay = null!;
    private long _tickCounter;

    [GlobalSetup]
    public void Setup()
    {
        _blocks = BuiltInBlocks.CreateFrozen();
        _waterDefault = _blocks.GetDefaultState(BuiltInBlocks.Water);
        _nbtBuffer = new ArrayBufferWriter<byte>(512);
        _sampleCompound = new NbtCompoundTag()
            .SetInt32("xPos", 12)
            .SetInt32("zPos", -34)
            .SetInt64("LastUpdate", 987654L)
            .SetString("Status", "full");

        _gameplay = new GameplaySubsystem(worldSeed: 42L);
        var brain = new AiBrainComponent(WanderSpeed: 2.0, DecisionIntervalTicks: 4);
        for (var i = 0; i < 256; i++)
        {
            _ = _gameplay.SpawnEntity(
                BuiltInEntities.PassiveGrazer,
                new Vec3d(i, 64.0, i),
                Vec3d.Zero,
                maxHealth: 10.0f,
                aiBrain: brain);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _gameplay.Dispose();
    }

    [Benchmark]
    public BlockStateMetadata BlockStatePropertyTransitionAndLookup()
    {
        var next = _blocks.WithProperty(_waterDefault, "level", "8");
        return _blocks.GetStateMetadata(next);
    }

    [Benchmark]
    public int NbtRoundTrip()
    {
        _nbtBuffer.Clear();
        NbtWriter.WriteRoot(_nbtBuffer, "Chunk", _sampleCompound);
        var (_, root) = NbtReader.ReadRoot(_nbtBuffer.WrittenSpan);
        return root.Count;
    }

    [Benchmark]
    public async Task<ulong> EcsParallelTick256EntitiesAsync()
    {
        await _gameplay.TickAsync(_tickCounter++, CancellationToken.None).ConfigureAwait(false);
        return _gameplay.ComputeStateHash();
    }
}
