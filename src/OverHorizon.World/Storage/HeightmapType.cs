namespace OverHorizon.World.Storage;

/// <summary>
/// Categories of per-column vertical heightmaps tracked inside a <see cref="ChunkColumn"/>.
/// </summary>
public enum HeightmapType : byte
{
    /// <summary>One above the highest non-air block in the column.</summary>
    WorldSurface = 0,

    /// <summary>One above the highest solid or fluid block in the column.</summary>
    MotionBlocking = 1,

    /// <summary>One above the highest light-occluding (opaque) block in the column.</summary>
    LightBlocking = 2,
}
