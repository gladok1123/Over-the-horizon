using FluentAssertions;
using OverHorizon.Core.Entities;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Ecs.Components;
using Xunit;

namespace OverHorizon.Tests;

public sealed class EcsDeterminismTests
{
    [Fact]
    public async Task ParallelEcsTick_ProducesIdenticalWorldStateHashAcross100RunsAsync()
    {
        const long seed = 0x2026_1112_CAFE_BABEL;
        const int runCount = 100;
        const int entityCount = 64;
        const int tickCount = 20;

        ulong? baselineHash = null;

        for (var run = 0; run < runCount; run++)
        {
            using var gameplay = new GameplaySubsystem(seed);

            for (var e = 0; e < entityCount; e++)
            {
                var startPos = new Vec3d(e * 1.5, 64.0, (e % 8) * -2.0);
                var startVel = new Vec3d(0.1, 0.0, -0.1);

                if (e % 3 == 0)
                {
                    _ = gameplay.SpawnEntity(BuiltInEntities.DroppedItem, startPos, startVel, maxHealth: 5.0f);
                }
                else
                {
                    var brain = new AiBrainComponent(WanderSpeed: 2.5, DecisionIntervalTicks: (e % 5) + 1);
                    _ = gameplay.SpawnEntity(BuiltInEntities.PassiveGrazer, startPos, startVel, maxHealth: 10.0f, brain);
                }
            }

            var initialHash = gameplay.ComputeStateHash();

            for (var tick = 0; tick < tickCount; tick++)
            {
                await gameplay.TickAsync(tick, CancellationToken.None).ConfigureAwait(false);
            }

            var finalHash = gameplay.ComputeStateHash();
            finalHash.Should().NotBe(initialHash, "entities must advance across simulation ticks");

            if (baselineHash is null)
            {
                baselineHash = finalHash;
            }
            else
            {
                finalHash.Should().Be(baselineHash.Value, $"run #{run} must match baseline world hash bit-for-bit");
            }
        }
    }

    [Fact]
    public async Task ParallelEcsTick_DifferentSeeds_ProduceDistinctStateHashesAsync()
    {
        using var simA = new GameplaySubsystem(worldSeed: 1001L);
        using var simB = new GameplaySubsystem(worldSeed: 2002L);

        var brain = new AiBrainComponent(WanderSpeed: 3.0, DecisionIntervalTicks: 1);
        _ = simA.SpawnEntity(BuiltInEntities.NightStalker, Vec3d.Zero, Vec3d.Zero, 20.0f, brain);
        _ = simB.SpawnEntity(BuiltInEntities.NightStalker, Vec3d.Zero, Vec3d.Zero, 20.0f, brain);

        await simA.TickAsync(0, CancellationToken.None).ConfigureAwait(false);
        await simB.TickAsync(0, CancellationToken.None).ConfigureAwait(false);

        simA.ComputeStateHash().Should().NotBe(simB.ComputeStateHash());
    }
}
