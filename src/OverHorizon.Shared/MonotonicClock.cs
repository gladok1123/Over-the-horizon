using System.Diagnostics;

namespace OverHorizon.Shared;

/// <summary>
/// Allocation-free monotonic clock backed by <see cref="Stopwatch"/>.
/// </summary>
public sealed class MonotonicClock : IClock
{
    /// <inheritdoc />
    public long GetTimestamp() => Stopwatch.GetTimestamp();

    /// <inheritdoc />
    public TimeSpan GetElapsedTime(long startingTimestamp) => Stopwatch.GetElapsedTime(startingTimestamp);
}
