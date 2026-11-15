using System.Collections.Immutable;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;

namespace OverHorizon.World.Storage;

/// <summary>
/// Thread-safe, immutable snapshot of a full 16x384x16 <see cref="ChunkColumn"/> for parallel readers.
/// </summary>
public sealed class ChunkSnapshot
{
    private readonly ChunkSectionSnapshot[] _sections;
    private readonly short[] _worldSurfaceHeights;
    private readonly short[] _motionBlockingHeights;
    private readonly short[] _lightBlockingHeights;

    internal ChunkSnapshot(
        ChunkPos position,
        long versionTick,
        ChunkSectionSnapshot[] sections,
        short[] worldSurfaceHeights,
        short[] motionBlockingHeights,
        short[] lightBlockingHeights)
    {
        Position = position;
        VersionTick = versionTick;
        _sections = sections;
        _worldSurfaceHeights = worldSurfaceHeights;
        _motionBlockingHeights = motionBlockingHeights;
        _lightBlockingHeights = lightBlockingHeights;
    }

    /// <summary>Gets the horizontal chunk coordinate.</summary>
    public ChunkPos Position { get; }

    /// <summary>Gets the simulation tick at which this snapshot was captured.</summary>
    public long VersionTick { get; }

    /// <summary>Gets all 24 immutable vertical section snapshots.</summary>
    public ImmutableArray<ChunkSectionSnapshot> Sections => [.. _sections];

    /// <summary>
    /// Reads the <see cref="BlockState"/> at chunk-local X/Z (<c>0..15</c>) and world Y (<c>-64..319</c>).
    /// </summary>
    /// <param name="localX">Local X in <c>[0, 15]</c>.</param>
    /// <param name="worldY">World Y in <c>[-64, 319]</c>.</param>
    /// <param name="localZ">Local Z in <c>[0, 15]</c>.</param>
    /// <returns>Block state at that coordinate.</returns>
    public BlockState GetBlockState(int localX, int worldY, int localZ)
    {
        var sectionIndex = ChunkColumn.GetSectionIndex(worldY);
        return _sections[sectionIndex].GetBlockState(localX, worldY & 15, localZ);
    }

    /// <summary>
    /// Reads the sky light level (<c>0..15</c>) at chunk-local X/Z and world Y.
    /// </summary>
    /// <param name="localX">Local X in <c>[0, 15]</c>.</param>
    /// <param name="worldY">World Y in <c>[-64, 319]</c>.</param>
    /// <param name="localZ">Local Z in <c>[0, 15]</c>.</param>
    /// <returns>Sky light level in <c>[0, 15]</c>.</returns>
    public byte GetSkyLight(int localX, int worldY, int localZ) =>
        _sections[ChunkColumn.GetSectionIndex(worldY)].GetSkyLight(localX, worldY & 15, localZ);

    /// <summary>
    /// Reads the block light level (<c>0..15</c>) at chunk-local X/Z and world Y.
    /// </summary>
    /// <param name="localX">Local X in <c>[0, 15]</c>.</param>
    /// <param name="worldY">World Y in <c>[-64, 319]</c>.</param>
    /// <param name="localZ">Local Z in <c>[0, 15]</c>.</param>
    /// <returns>Block light level in <c>[0, 15]</c>.</returns>
    public byte GetBlockLight(int localX, int worldY, int localZ) =>
        _sections[ChunkColumn.GetSectionIndex(worldY)].GetBlockLight(localX, worldY & 15, localZ);

    /// <summary>
    /// Reads the column height for <paramref name="type"/> at chunk-local X/Z (<c>0..15</c>).
    /// </summary>
    /// <param name="type">Heightmap category.</param>
    /// <param name="localX">Local X in <c>[0, 15]</c>.</param>
    /// <param name="localZ">Local Z in <c>[0, 15]</c>.</param>
    /// <returns>World Y height (<see cref="EngineConstants.WorldMinY"/> if column is empty).</returns>
    public int GetHeight(HeightmapType type, int localX, int localZ)
    {
        if ((uint)localX >= 16U || (uint)localZ >= 16U)
        {
            throw new ArgumentOutOfRangeException(nameof(localX), "Local X/Z must be in [0, 15].");
        }

        var idx = (localZ << 4) | localX;
        return type switch
        {
            HeightmapType.WorldSurface => _worldSurfaceHeights[idx],
            HeightmapType.MotionBlocking => _motionBlockingHeights[idx],
            HeightmapType.LightBlocking => _lightBlockingHeights[idx],
            _ => EngineConstants.WorldMinY,
        };
    }
}
