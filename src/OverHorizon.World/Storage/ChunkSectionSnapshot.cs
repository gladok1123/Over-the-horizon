using OverHorizon.Core.Blocks;

namespace OverHorizon.World.Storage;

/// <summary>
/// Thread-safe, immutable snapshot of a 16x16x16 <see cref="ChunkSection"/> for lock-free concurrent reads.
/// </summary>
public sealed class ChunkSectionSnapshot
{
    private readonly PalettedContainer<BlockState> _blocks;
    private readonly PalettedContainer<ushort> _biomes;
    private readonly byte[]? _skyLightNibbles;
    private readonly byte[]? _blockLightNibbles;

    internal ChunkSectionSnapshot(
        int sectionY,
        ushort nonEmptyBlockCount,
        PalettedContainer<BlockState> blocks,
        PalettedContainer<ushort> biomes,
        byte[]? skyLightNibbles,
        byte[]? blockLightNibbles)
    {
        SectionY = sectionY;
        NonEmptyBlockCount = nonEmptyBlockCount;
        _blocks = blocks;
        _biomes = biomes;
        _skyLightNibbles = skyLightNibbles;
        _blockLightNibbles = blockLightNibbles;
    }

    /// <summary>Gets the vertical section coordinate (e.g. <c>-4..19</c>).</summary>
    public int SectionY { get; }

    /// <summary>Gets the count of non-air blocks in this section.</summary>
    public ushort NonEmptyBlockCount { get; }

    /// <summary>Gets a value indicating whether every block in this section is air.</summary>
    public bool IsOnlyAir => NonEmptyBlockCount == 0;

    internal ReadOnlySpan<byte> SkyLightSpanOrEmpty => _skyLightNibbles;

    internal ReadOnlySpan<byte> BlockLightSpanOrEmpty => _blockLightNibbles;

    /// <summary>
    /// Reads the <see cref="BlockState"/> at section-local coordinates (<c>0..15</c>).
    /// </summary>
    /// <param name="localX">Local X in <c>[0, 15]</c>.</param>
    /// <param name="localY">Local Y in <c>[0, 15]</c>.</param>
    /// <param name="localZ">Local Z in <c>[0, 15]</c>.</param>
    /// <returns>Block state at that cell.</returns>
    public BlockState GetBlockState(int localX, int localY, int localZ) =>
        _blocks.Get(ChunkSection.GetBlockIndex(localX, localY, localZ));

    /// <summary>
    /// Reads the biome registry ID at 4x4x4 quartet coordinates (<c>0..3</c>).
    /// </summary>
    /// <param name="quartX">Quartet X in <c>[0, 3]</c>.</param>
    /// <param name="quartY">Quartet Y in <c>[0, 3]</c>.</param>
    /// <param name="quartZ">Quartet Z in <c>[0, 3]</c>.</param>
    /// <returns>Dense biome registry ID.</returns>
    public ushort GetBiomeId(int quartX, int quartY, int quartZ) =>
        _biomes.Get(ChunkSection.GetBiomeIndex(quartX, quartY, quartZ));

    /// <summary>
    /// Reads the 4-bit sky light level (<c>0..15</c>) at section-local coordinates.
    /// </summary>
    /// <param name="localX">Local X in <c>[0, 15]</c>.</param>
    /// <param name="localY">Local Y in <c>[0, 15]</c>.</param>
    /// <param name="localZ">Local Z in <c>[0, 15]</c>.</param>
    /// <returns>Sky light level in <c>[0, 15]</c>.</returns>
    public byte GetSkyLight(int localX, int localY, int localZ) =>
        ChunkSection.ReadNibble(_skyLightNibbles, ChunkSection.GetBlockIndex(localX, localY, localZ));

    /// <summary>
    /// Reads the 4-bit block light level (<c>0..15</c>) at section-local coordinates.
    /// </summary>
    /// <param name="localX">Local X in <c>[0, 15]</c>.</param>
    /// <param name="localY">Local Y in <c>[0, 15]</c>.</param>
    /// <param name="localZ">Local Z in <c>[0, 15]</c>.</param>
    /// <returns>Block light level in <c>[0, 15]</c>.</returns>
    public byte GetBlockLight(int localX, int localY, int localZ) =>
        ChunkSection.ReadNibble(_blockLightNibbles, ChunkSection.GetBlockIndex(localX, localY, localZ));
}
