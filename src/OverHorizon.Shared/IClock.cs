namespace OverHorizon.Shared;

/// <summary>
/// Provides monotonic elapsed time measurements independent of wall-clock adjustments.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Gets the current high-resolution timestamp value.
    /// </summary>
    long GetTimestamp();

    /// <summary>
    /// Computes the elapsed duration since a previously captured timestamp.
    /// </summary>
    /// <param name="startingTimestamp">Timestamp obtained from <see cref="GetTimestamp"/>.</param>
    /// <returns>The elapsed <see cref="TimeSpan"/>.</returns>
    TimeSpan GetElapsedTime(long startingTimestamp);
}
