using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Shared.Hashing;

namespace OverHorizon.World.Storage;

/// <summary>
/// Full 16x384x16 vertical chunk column composed of 24 <see cref="ChunkSection"/>s and three heightmaps.
/// Supports double-buffered immutable <see cref="ChunkSnapshot"/> creation for Horizon-style parallel access.
/// </summary>
public sealed class ChunkColumn
{
    /// <summary>Number of 16-block vertical sections per chunk column (24).</summary>
    public const int SectionCount = EngineConstants.WorldHeight / EngineConstants.ChunkSectionSize;

    /// <summary>Minimum vertical section index (-4).</summary>
    public const int MinSectionY = EngineConstants.WorldMinY >> 4;

    private readonly ChunkSection[] _sections;
    private readonly short[] _worldSurfaceHeights = new short[256];
    private readonly short[] _motionBlockingHeights = new short[256];
    private readonly short[] _lightBlockingHeights = new short[256];
    private int _dirtyFlag;

    /// <summary>
    /// Initializes a new air-filled instance of the <see cref="ChunkColumn"/> class.
    /// </summary>
    /// <param name="position">Horizontal chunk coordinate.</param>
    public ChunkColumn(ChunkPos position)
    {
        Position = position;
        _sections = new ChunkSection[SectionCount];
        for (var i = 0; i < SectionCount; i++)
        {
            _sections[i] = new ChunkSection(MinSectionY + i);
        }

        Array.Fill(_worldSurfaceHeights, (short)EngineConstants.WorldMinY);
        Array.Fill(_motionBlockingHeights, (short)EngineConstants.WorldMinY);
        Array.Fill(_lightBlockingHeights, (short)EngineConstants.WorldMinY);
    }

    /// <summary>Gets the horizontal <see cref="ChunkPos"/> of this column.</summary>
    public ChunkPos Position { get; }

    /// <summary>Gets or sets the simulation tick at which this chunk was last modified.</summary>
    public long LastModifiedTick { get; set; }

    /// <summary>Gets a value indicating whether unsaved modifications exist.</summary>
    public bool IsDirty => Volatile.Read(ref _dirtyFlag) != 0;

    /// <summary>
    /// Converts a world Y coordinate in <c>[-64, 319]</c> to a section array index in <c>[0, 23]</c>.
    /// </summary>
    /// <param name="worldY">World Y coordinate.</param>
    /// <returns>Zero-based section array index.</returns>
    public static int GetSectionIndex(int worldY)
    {
        if (worldY < EngineConstants.WorldMinY || worldY >= EngineConstants.WorldMaxY)
        {
            throw new ArgumentOutOfRangeException(
                nameof(worldY),
                $"World Y {worldY} is outside [{EngineConstants.WorldMinY}, {EngineConstants.WorldMaxY}).");
        }

        return (worldY - EngineConstants.WorldMinY) >> 4;
    }

    /// <summary>
    /// Returns the <see cref="ChunkSection"/> at zero-based <paramref name="sectionIndex"/> (<c>0..23</c>).
    /// </summary>
    /// <param name="sectionIndex">Zero-based section index.</param>
    /// <returns>Mutable <see cref="ChunkSection"/>.</returns>
    public ChunkSection GetSection(int sectionIndex)
    {
        if ((uint)sectionIndex >= SectionCount)
        {
            throw new ArgumentOutOfRangeException(nameof(sectionIndex));
        }

        return _sections[sectionIndex];
    }

    /// <summary>
    /// Reads the <see cref="BlockState"/> at chunk-local X/Z (<c>0..15</c>) and world Y (<c>-64..319</c>).
    /// </summary>
    /// <param name="localX">Local X in <c>[0, 15]</c>.</param>
    /// <param name="worldY">World Y in <c>[-64, 319]</c>.</param>
    /// <param name="localZ">Local Z in <c>[0, 15]</c>.</param>
    /// <returns>Stored <see cref="BlockState"/>.</returns>
    public BlockState GetBlockState(int localX, int worldY, int localZ) =>
        _sections[GetSectionIndex(worldY)].GetBlockState(localX, worldY & 15, localZ);

    /// <summary>
    /// Sets a block without recomputing heightmaps (used during bulk worldgen passes before <see cref="RecomputeHeightmaps"/>).
    /// </summary>
    /// <param name="localX">Local X in <c>[0, 15]</c>.</param>
    /// <param name="worldY">World Y in <c>[-64, 319]</c>.</param>
    /// <param name="localZ">Local Z in <c>[0, 15]</c>.</param>
    /// <param name="state">New block state.</param>
    /// <returns>Previous <see cref="BlockState"/>.</returns>
    public BlockState SetBlockStateRaw(int localX, int worldY, int localZ, BlockState state)
    {
        var previous = _sections[GetSectionIndex(worldY)].SetBlockState(localX, worldY & 15, localZ, state);
        if (previous != state)
        {
            Volatile.Write(ref _dirtyFlag, 1);
        }

        return previous;
    }

    /// <summary>
    /// Sets a block and updates all three column heightmaps using <paramref name="blocks"/>.
    /// </summary>
    /// <param name="localX">Local X in <c>[0, 15]</c>.</param>
    /// <param name="worldY">World Y in <c>[-64, 319]</c>.</param>
    /// <param name="localZ">Local Z in <c>[0, 15]</c>.</param>
    /// <param name="state">New block state.</param>
    /// <param name="blocks">Block registry for solidity/opacity metadata.</param>
    /// <returns>Previous <see cref="BlockState"/>.</returns>
    public BlockState SetBlockState(int localX, int worldY, int localZ, BlockState state, IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        var previous = SetBlockStateRaw(localX, worldY, localZ, state);
        if (previous != state)
        {
            UpdateColumnHeightmaps(localX, localZ, blocks);
        }

        return previous;
    }

    /// <summary>Reads the sky light level (<c>0..15</c>) at chunk-local X/Z and world Y.</summary>
    /// <param name="localX">Local X in <c>[0, 15]</c>.</param>
    /// <param name="worldY">World Y in <c>[-64, 319]</c>.</param>
    /// <param name="localZ">Local Z in <c>[0, 15]</c>.</param>
    /// <returns>Sky light in <c>[0, 15]</c>.</returns>
    public byte GetSkyLight(int localX, int worldY, int localZ) =>
        _sections[GetSectionIndex(worldY)].GetSkyLight(localX, worldY & 15, localZ);

    /// <summary>Sets the sky light level (<c>0..15</c>) at chunk-local X/Z and world Y.</summary>
    /// <param name="localX">Local X in <c>[0, 15]</c>.</param>
    /// <param name="worldY">World Y in <c>[-64, 319]</c>.</param>
    /// <param name="localZ">Local Z in <c>[0, 15]</c>.</param>
    /// <param name="level">Light level in <c>[0, 15]</c>.</param>
    public void SetSkyLight(int localX, int worldY, int localZ, byte level) =>
        _sections[GetSectionIndex(worldY)].SetSkyLight(localX, worldY & 15, localZ, level);

    /// <summary>Reads the block light level (<c>0..15</c>) at chunk-local X/Z and world Y.</summary>
    /// <param name="localX">Local X in <c>[0, 15]</c>.</param>
    /// <param name="worldY">World Y in <c>[-64, 319]</c>.</param>
    /// <param name="localZ">Local Z in <c>[0, 15]</c>.</param>
    /// <returns>Block light in <c>[0, 15]</c>.</returns>
    public byte GetBlockLight(int localX, int worldY, int localZ) =>
        _sections[GetSectionIndex(worldY)].GetBlockLight(localX, worldY & 15, localZ);

    /// <summary>Sets the block light level (<c>0..15</c>) at chunk-local X/Z and world Y.</summary>
    /// <param name="localX">Local X in <c>[0, 15]</c>.</param>
    /// <param name="worldY">World Y in <c>[-64, 319]</c>.</param>
    /// <param name="localZ">Local Z in <c>[0, 15]</c>.</param>
    /// <param name="level">Light level in <c>[0, 15]</c>.</param>
    public void SetBlockLight(int localX, int worldY, int localZ, byte level) =>
        _sections[GetSectionIndex(worldY)].SetBlockLight(localX, worldY & 15, localZ, level);

    /// <summary>Reads the heightmap value at chunk-local X/Z (<c>0..15</c>).</summary>
    /// <param name="type">Heightmap category.</param>
    /// <param name="localX">Local X in <c>[0, 15]</c>.</param>
    /// <param name="localZ">Local Z in <c>[0, 15]</c>.</param>
    /// <returns>Height Y (<see cref="EngineConstants.WorldMinY"/> if empty).</returns>
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

    /// <summary>Recomputes all three heightmaps across all 256 (X, Z) columns.</summary>
    /// <param name="blocks">Frozen block registry.</param>
    public void RecomputeHeightmaps(IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        for (var z = 0; z < 16; z++)
        {
            for (var x = 0; x < 16; x++)
            {
                UpdateColumnHeightmaps(x, z, blocks);
            }
        }
    }

    /// <summary>Marks the chunk as clean (persisted to storage).</summary>
    public void MarkClean() => Volatile.Write(ref _dirtyFlag, 0);

    /// <summary>Marks the chunk as dirty (requiring persistence).</summary>
    public void MarkDirty() => Volatile.Write(ref _dirtyFlag, 1);

    /// <summary>
    /// Captures an immutable <see cref="ChunkSnapshot"/> for lock-free concurrent access.
    /// </summary>
    /// <returns>Immutable <see cref="ChunkSnapshot"/>.</returns>
    public ChunkSnapshot CreateSnapshot()
    {
        var sectionSnapshots = new ChunkSectionSnapshot[SectionCount];
        for (var i = 0; i < SectionCount; i++)
        {
            sectionSnapshots[i] = _sections[i].CreateSnapshot();
        }

        return new ChunkSnapshot(
            Position,
            LastModifiedTick,
            sectionSnapshots,
            [.. _worldSurfaceHeights],
            [.. _motionBlockingHeights],
            [.. _lightBlockingHeights]);
    }

    /// <summary>
    /// Computes a deterministic 64-bit FNV-1a hash of all blocks, biomes, heightmaps, and light values.
    /// </summary>
    /// <returns>64-bit state hash.</returns>
    public ulong ComputeDeterministicHash()
    {
        var hasher = Fnv1a64.Create();
        hasher.AppendInt32(Position.X);
        hasher.AppendInt32(Position.Z);

        for (var i = 0; i < 256; i++)
        {
            hasher.AppendInt32(_worldSurfaceHeights[i]);
            hasher.AppendInt32(_motionBlockingHeights[i]);
            hasher.AppendInt32(_lightBlockingHeights[i]);
        }

        for (var s = 0; s < SectionCount; s++)
        {
            var sec = _sections[s];
            hasher.AppendInt32(sec.NonEmptyBlockCount);

            for (var qy = 0; qy < 4; qy++)
            {
                for (var qz = 0; qz < 4; qz++)
                {
                    for (var qx = 0; qx < 4; qx++)
                    {
                        hasher.AppendInt32(sec.GetBiomeId(qx, qy, qz));
                    }
                }
            }

            if (!sec.IsOnlyAir)
            {
                for (var y = 0; y < 16; y++)
                {
                    for (var z = 0; z < 16; z++)
                    {
                        for (var x = 0; x < 16; x++)
                        {
                            hasher.AppendInt32(sec.GetBlockState(x, y, z).Id);
                        }
                    }
                }
            }

            hasher.AppendBytes(sec.GetSkyLightSpanOrEmpty());
            hasher.AppendBytes(sec.GetBlockLightSpanOrEmpty());
        }

        return hasher.Value;
    }

    private void UpdateColumnHeightmaps(int localX, int localZ, IBlockRegistry blocks)
    {
        var (surface, motion, light) = ScanColumnTopDown(localX, localZ, blocks);
        var colIdx = (localZ << 4) | localX;
        _worldSurfaceHeights[colIdx] = surface;
        _motionBlockingHeights[colIdx] = motion;
        _lightBlockingHeights[colIdx] = light;
    }

    private (short Surface, short Motion, short Light) ScanColumnTopDown(
        int localX,
        int localZ,
        IBlockRegistry blocks)
    {
        short surface = EngineConstants.WorldMinY;
        short motion = EngineConstants.WorldMinY;
        short light = EngineConstants.WorldMinY;

        var foundSurface = false;
        var foundMotion = false;
        var foundLight = false;

        for (var secIdx = SectionCount - 1; secIdx >= 0; secIdx--)
        {
            var sec = _sections[secIdx];
            if (sec.IsOnlyAir)
            {
                continue;
            }

            var baseY = EngineConstants.WorldMinY + (secIdx << 4);
            for (var ly = 15; ly >= 0; ly--)
            {
                var state = sec.GetBlockState(localX, ly, localZ);
                if (state.IsAir)
                {
                    continue;
                }

                var candidateHeight = (short)(baseY + ly + 1);
                if (!foundSurface)
                {
                    surface = candidateHeight;
                    foundSurface = true;
                }

                var meta = blocks.GetStateMetadata(state);
                if (!foundMotion && (meta.IsSolid || meta.IsFluid))
                {
                    motion = candidateHeight;
                    foundMotion = true;
                }

                if (!foundLight && meta.IsOpaque)
                {
                    light = candidateHeight;
                    foundLight = true;
                }

                if (foundSurface && foundMotion && foundLight)
                {
                    return (surface, motion, light);
                }
            }
        }

        return (surface, motion, light);
    }
}
