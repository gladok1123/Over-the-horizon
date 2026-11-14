using BenchmarkDotNet.Attributes;
using OverHorizon.Shared;

namespace OverHorizon.Benchmarks;

[MemoryDiagnoser]
public class ClockBenchmarks
{
    private readonly MonotonicClock _clock = new();

    [Benchmark]
    public TimeSpan MeasureElapsed()
    {
        var start = _clock.GetTimestamp();
        return _clock.GetElapsedTime(start);
    }
}
