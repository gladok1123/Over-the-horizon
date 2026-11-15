namespace OverHorizon.Core;

/// <summary>
/// Immutable domain constants shared across the voxel engine.
/// </summary>
public static class EngineConstants
{
    /// <summary>
    /// Target simulation rate in ticks per second (20 TPS).
    /// </summary>
    public const int TicksPerSecond = 20;

    /// <summary>
    /// Target duration of a single simulation tick in milliseconds (50 ms).
    /// </summary>
    public const int TickDurationMilliseconds = 1000 / TicksPerSecond;

    /// <summary>
    /// Width of a chunk along the X axis in blocks.
    /// </summary>
    public const int ChunkSizeX = 16;

    /// <summary>
    /// Width of a chunk along the Z axis in blocks.
    /// </summary>
    public const int ChunkSizeZ = 16;

    /// <summary>
    /// Cubic edge length of a single chunk section (16x16x16).
    /// </summary>
    public const int ChunkSectionSize = 16;

    /// <summary>
    /// Minimum world build height (inclusive).
    /// </summary>
    public const int WorldMinY = -64;

    /// <summary>
    /// Maximum world build height (exclusive).
    /// </summary>
    public const int WorldMaxY = 320;

    /// <summary>
    /// Total vertical world height in blocks (384).
    /// </summary>
    public const int WorldHeight = WorldMaxY - WorldMinY;
}
