using OverHorizon.Core;
using OverHorizon.Core.Blocks;

namespace OverHorizon.World.Storage;

/// <summary>
/// Represents a 16x16x16 cubic section of blocks (4096 cells), 4x4x4 biomes (64 cells), and packed 4-bit light layers.
/// </summary>
public sealed class ChunkSection
{
    /// <summary>Total number of voxel blocks per section (4096).</summary>
    public const int BlockCapacity =
        EngineConstants.ChunkSectionSize * EngineConstants.ChunkSectionSize * EngineConstants.ChunkSectionSize;

    /// <summary>Total number of 4x4x4 biome cells per section (64).</summary>
    public const int BiomeCapacity = 4 * 4 * 4;

    /// <summary>Byte length of a packed 4-bit nibble array covering 4096 blocks (2048 bytes).</summary>
    public const int LightNibbleByteCount = BlockCapacity / 2;

    private readonly PalettedContainer<BlockState> _blocks;
    private readonly PalettedContainer<ushort> _biomes;
    private byte[]? _skyLightNibbles;
    private byte[]? _blockLightNibbles;

    /// <summary>
    /// Initializes a new air-filled instance of the <see cref="ChunkSection"/> class.
    /// </summary>
    /// <param name="sectionY">Vertical section coordinate (e.g. <c>-4..19</c>).</param>
    /// <param name="defaultBiomeId">Initial uniform biome raw ID.</param>
    public ChunkSection(int sectionY, ushort defaultBiomeId = 0)
    {
        SectionY = sectionY;
        _blocks = new PalettedContainer<BlockState>(
            capacity: BlockCapacity,
            initialValue: BlockState.Air,
            minIndirectBits: 4,
            maxIndirectBits: 8,
            directBits: 16,
            toGlobalId: static s => s.Id,
            fromGlobalId: static id => new BlockState((ushort)id));

        _biomes = new PalettedContainer<ushort>(
            capacity: BiomeCapacity,
            initialValue: defaultBiomeId,
            minIndirectBits: 1,
            maxIndirectBits: 3,
            directBits: 8,
            toGlobalId: static b => b,
            fromGlobalId: static id => (ushort)id);
    }

    /// <summary>Gets the vertical section index (<c>worldY &gt;&gt; 4</c>).</summary>
    public int SectionY { get; }

    /// <summary>Gets the count of non-air blocks in this section.</summary>
    public ushort NonEmptyBlockCount { get; private set; }

    /// <summary>Gets a value indicating whether all 4096 blocks in this section are air.</summary>
    public bool IsOnlyAir => NonEmptyBlockCount == 0;

    /// <summary>Gets the current bit width per block entry (<c>0</c> when uniform).</summary>
    public int BlockBitsPerEntry => _blocks.BitsPerEntry;

    /// <summary>
    /// Computes the Y-Z-X linear index in <c>[0, 4095]</c> for section-local coordinates.
    /// </summary>
    /// <param name="localX">Local X in <c>[0, 15]</c>.</param>
    /// <param name="localY">Local Y in <c>[0, 15]</c>.</param>
    /// <param name="localZ">Local Z in <c>[0, 15]</c>.</param>
    /// <returns>Linear index in <c>[0, 4095]</c>.</returns>
    public static int GetBlockIndex(int localX, int localY, int localZ)
    {
        if ((uint)localX >= 16U || (uint)localY >= 16U || (uint)localZ >= 16U)
        {
            throw new ArgumentOutOfRangeException(nameof(localX), "Section-local block coordinates must be in [0, 15].");
        }

        return (localY << 8) | (localZ << 4) | localX;
    }

    /// <summary>
    /// Computes the linear index in <c>[0, 63]</c> for 4x4x4 biome quartet coordinates.
    /// </summary>
    /// <param name="quartX">Quartet X in <c>[0, 3]</c>.</param>
    /// <param name="quartY">Quartet Y in <c>[0, 3]</c>.</param>
    /// <param name="quartZ">Quartet Z in <c>[0, 3]</c>.</param>
    /// <returns>Linear index in <c>[0, 63]</c>.</returns>
    public static int GetBiomeIndex(int quartX, int quartY, int quartZ)
    {
        if ((uint)quartX >= 4U || (uint)quartY >= 4U || (uint)quartZ >= 4U)
        {
            throw new ArgumentOutOfRangeException(nameof(quartX), "Section quartet coordinates must be in [0, 3].");
        }

        return (quartY << 4) | (quartZ << 2) | quartX;
    }

    /// <summary>
    /// Reads the <see cref="BlockState"/> at section-local coordinates (<c>0..15</c>).
    /// </summary>
    /// <param name="localX">Local X in <c>[0, 15]</c>.</param>
    /// <param name="localY">Local Y in <c>[0, 15]</c>.</param>
    /// <param name="localZ">Local Z in <c>[0, 15]</c>.</param>
    /// <returns>Stored <see cref="BlockState"/>.</returns>
    public BlockState GetBlockState(int localX, int localY, int localZ) =>
        _blocks.Get(GetBlockIndex(localX, localY, localZ));

    /// <summary>
    /// Sets the <see cref="BlockState"/> at section-local coordinates (<c>0..15</c>), updating <see cref="NonEmptyBlockCount"/>.
    /// </summary>
    /// <param name="localX">Local X in <c>[0, 15]</c>.</param>
    /// <param name="localY">Local Y in <c>[0, 15]</c>.</param>
    /// <param name="localZ">Local Z in <c>[0, 15]</c>.</param>
    /// <param name="newState">New block state.</param>
    /// <returns>Previous <see cref="BlockState"/>.</returns>
    public BlockState SetBlockState(int localX, int localY, int localZ, BlockState newState)
    {
        var previous = _blocks.GetAndSet(GetBlockIndex(localX, localY, localZ), newState);
        if (!previous.IsAir)
        {
            NonEmptyBlockCount--;
        }

        if (!newState.IsAir)
        {
            NonEmptyBlockCount++;
        }

        return previous;
    }

    /// <summary>Reads the biome raw ID at 4x4x4 quartet coordinates (<c>0..3</c>).</summary>
    /// <param name="quartX">Quartet X in <c>[0, 3]</c>.</param>
    /// <param name="quartY">Quartet Y in <c>[0, 3]</c>.</param>
    /// <param name="quartZ">Quartet Z in <c>[0, 3]</c>.</param>
    /// <returns>Biome raw ID.</returns>
    public ushort GetBiomeId(int quartX, int quartY, int quartZ) =>
        _biomes.Get(GetBiomeIndex(quartX, quartY, quartZ));

    /// <summary>Sets the biome raw ID at 4x4x4 quartet coordinates (<c>0..3</c>).</summary>
    /// <param name="quartX">Quartet X in <c>[0, 3]</c>.</param>
    /// <param name="quartY">Quartet Y in <c>[0, 3]</c>.</param>
    /// <param name="quartZ">Quartet Z in <c>[0, 3]</c>.</param>
    /// <param name="biomeId">Biome raw ID.</param>
    public void SetBiomeId(int quartX, int quartY, int quartZ, ushort biomeId) =>
        _biomes.Set(GetBiomeIndex(quartX, quartY, quartZ), biomeId);

    /// <summary>Reads the 4-bit sky light level (<c>0..15</c>) at section-local coordinates.</summary>
    /// <param name="localX">Local X in <c>[0, 15]</c>.</param>
    /// <param name="localY">Local Y in <c>[0, 15]</c>.</param>
    /// <param name="localZ">Local Z in <c>[0, 15]</c>.</param>
    /// <returns>Sky light level in <c>[0, 15]</c>.</returns>
    public byte GetSkyLight(int localX, int localY, int localZ) =>
        ReadNibble(_skyLightNibbles, GetBlockIndex(localX, localY, localZ));

    /// <summary>Sets the 4-bit sky light level (<c>0..15</c>) at section-local coordinates.</summary>
    /// <param name="localX">Local X in <c>[0, 15]</c>.</param>
    /// <param name="localY">Local Y in <c>[0, 15]</c>.</param>
    /// <param name="localZ">Local Z in <c>[0, 15]</c>.</param>
    /// <param name="level">Light level in <c>[0, 15]</c>.</param>
    public void SetSkyLight(int localX, int localY, int localZ, byte level)
    {
        if (level == 0 && _skyLightNibbles is null)
        {
            return;
        }

        _skyLightNibbles ??= new byte[LightNibbleByteCount];
        WriteNibble(_skyLightNibbles, GetBlockIndex(localX, localY, localZ), level);
    }

    /// <summary>Reads the 4-bit block light level (<c>0..15</c>) at section-local coordinates.</summary>
    /// <param name="localX">Local X in <c>[0, 15]</c>.</param>
    /// <param name="localY">Local Y in <c>[0, 15]</c>.</param>
    /// <param name="localZ">Local Z in <c>[0, 15]</c>.</param>
    /// <returns>Block light level in <c>[0, 15]</c>.</returns>
    public byte GetBlockLight(int localX, int localY, int localZ) =>
        ReadNibble(_blockLightNibbles, GetBlockIndex(localX, localY, localZ));

    /// <summary>Sets the 4-bit block light level (<c>0..15</c>) at section-local coordinates.</summary>
    /// <param name="localX">Local X in <c>[0, 15]</c>.</param>
    /// <param name="localY">Local Y in <c>[0, 15]</c>.</param>
    /// <param name="localZ">Local Z in <c>[0, 15]</c>.</param>
    /// <param name="level">Light level in <c>[0, 15]</c>.</param>
    public void SetBlockLight(int localX, int localY, int localZ, byte level)
    {
        if (level == 0 && _blockLightNibbles is null)
        {
            return;
        }

        _blockLightNibbles ??= new byte[LightNibbleByteCount];
        WriteNibble(_blockLightNibbles, GetBlockIndex(localX, localY, localZ), level);
    }

    /// <summary>Clears both sky and block light buffers back to zero.</summary>
    public void ClearLight()
    {
        _skyLightNibbles = null;
        _blockLightNibbles = null;
    }

    /// <summary>
    /// Creates an immutable Copy-on-Write <see cref="ChunkSectionSnapshot"/> for concurrent readers.
    /// </summary>
    /// <returns>Immutable <see cref="ChunkSectionSnapshot"/>.</returns>
    public ChunkSectionSnapshot CreateSnapshot() => new(
        SectionY,
        NonEmptyBlockCount,
        _blocks.Clone(),
        _biomes.Clone(),
        _skyLightNibbles is null ? null : [.. _skyLightNibbles],
        _blockLightNibbles is null ? null : [.. _blockLightNibbles]);

    internal static byte ReadNibble(byte[]? nibbles, int blockIndex)
    {
        if (nibbles is null)
        {
            return 0;
        }

        var b = nibbles[blockIndex >> 1];
        return (blockIndex & 1) == 0 ? (byte)(b & 0x0F) : (byte)((b >> 4) & 0x0F);
    }

    internal ReadOnlySpan<byte> GetSkyLightSpanOrEmpty() =>
        _skyLightNibbles is null ? ReadOnlySpan<byte>.Empty : _skyLightNibbles;

    internal ReadOnlySpan<byte> GetBlockLightSpanOrEmpty() =>
        _blockLightNibbles is null ? ReadOnlySpan<byte>.Empty : _blockLightNibbles;

    internal void LoadLightNibbleArrays(ReadOnlySpan<byte> skyLight, ReadOnlySpan<byte> blockLight)
    {
        if (!skyLight.IsEmpty)
        {
            if (skyLight.Length != LightNibbleByteCount)
            {
                throw new ArgumentException($"Sky light array must be {LightNibbleByteCount} bytes.", nameof(skyLight));
            }

            _skyLightNibbles = skyLight.ToArray();
        }
        else
        {
            _skyLightNibbles = null;
        }

        if (!blockLight.IsEmpty)
        {
            if (blockLight.Length != LightNibbleByteCount)
            {
                throw new ArgumentException($"Block light array must be {LightNibbleByteCount} bytes.", nameof(blockLight));
            }

            _blockLightNibbles = blockLight.ToArray();
        }
        else
        {
            _blockLightNibbles = null;
        }
    }

    private static void WriteNibble(byte[] nibbles, int blockIndex, byte level)
    {
        if (level > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(level), "Light level must be in [0, 15].");
        }

        var byteIdx = blockIndex >> 1;
        if ((blockIndex & 1) == 0)
        {
            nibbles[byteIdx] = (byte)((nibbles[byteIdx] & 0xF0) | level);
        }
        else
        {
            nibbles[byteIdx] = (byte)((nibbles[byteIdx] & 0x0F) | (level << 4));
        }
    }
}
