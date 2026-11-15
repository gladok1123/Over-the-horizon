namespace OverHorizon.Core;

/// <summary>
/// Identifies one of the playable world dimensions of Over-the-Horizon.
/// </summary>
public enum DimensionId : byte
{
    /// <summary>
    /// The default surface world: full skylight, weather, a day/night cycle, and Overworld terrain.
    /// </summary>
    Overworld = 0,

    /// <summary>
    /// The ultra-warm Nether: no skylight, a bedrock ceiling, lava seas, and <c>8:1</c> coordinate compression
    /// relative to the <see cref="Overworld"/>.
    /// </summary>
    Nether = 1,
}
