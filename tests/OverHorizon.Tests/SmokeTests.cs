using System.Globalization;
using FluentAssertions;
using OverHorizon.Core;
using OverHorizon.Server;
using OverHorizon.Shared;
using Xunit;

namespace OverHorizon.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void EngineConstants_HaveConsistentChunkAndTickDimensions()
    {
        EngineConstants.TicksPerSecond.Should().Be(20);
        EngineConstants.TickDurationMilliseconds.Should().Be(50);
        EngineConstants.WorldHeight.Should().Be(384);
        (EngineConstants.WorldHeight % EngineConstants.ChunkSectionSize).Should().Be(0);
    }

    [Fact]
    public void MonotonicClock_MeasuresNonNegativeElapsedTime()
    {
        var clock = new MonotonicClock();
        var start = clock.GetTimestamp();
        var elapsed = clock.GetElapsedTime(start);

        elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void ParseOptions_HandlesInvalidAndEmptyInputsGracefully()
    {
        ServerBootstrap.ParseOptions([]).MaxTicks.Should().BeNull();
        ServerBootstrap.ParseOptions(["--max-ticks"]).MaxTicks.Should().BeNull();
        ServerBootstrap.ParseOptions(["--max-ticks", "-5"]).MaxTicks.Should().BeNull();
        ServerBootstrap.ParseOptions(["--max-ticks", "not-a-number"]).MaxTicks.Should().BeNull();
    }

    [FsCheck.Xunit.Property]
    public void ParseOptions_RoundTripsNonNegativeMaxTicks(uint ticks)
    {
        var parsed = ServerBootstrap.ParseOptions(["--max-ticks", ticks.ToString(CultureInfo.InvariantCulture)]);
        parsed.MaxTicks.Should().Be(ticks);
    }
}
