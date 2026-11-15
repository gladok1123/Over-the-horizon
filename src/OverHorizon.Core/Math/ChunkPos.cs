namespace OverHorizon.Core.Math;

/// <summary>
/// Immutable 2D horizontal chunk coordinate (each chunk spans 16x16 blocks in X and Z).
/// </summary>
/// <param name="X">Chunk X index.</param>
/// <param name="Z">Chunk Z index.</param>
public readonly record struct ChunkPos(int X, int Z) : IComparable<ChunkPos>
{
    /// <summary>
    /// Gets the minimum world block X coordinate inside this chunk.
    /// </summary>
    public int MinBlockX => X << 4;

    /// <summary>
    /// Gets the minimum world block Z coordinate inside this chunk.
    /// </summary>
    public int MinBlockZ => Z << 4;

    /// <summary>
    /// Gets the maximum world block X coordinate (inclusive) inside this chunk.
    /// </summary>
    public int MaxBlockX => (X << 4) + 15;

    /// <summary>
    /// Gets the maximum world block Z coordinate (inclusive) inside this chunk.
    /// </summary>
    public int MaxBlockZ => (Z << 4) + 15;

    /// <summary>
    /// Unpacks a 64-bit integer previously created by <see cref="PackInt64"/>.
    /// </summary>
    /// <param name="packed">Packed 64-bit chunk key.</param>
    /// <returns>Unpacked <see cref="ChunkPos"/>.</returns>
    public static ChunkPos UnpackInt64(long packed) =>
        new((int)(uint)packed, (int)(uint)(packed >> 32));

    /// <summary>
    /// Compares two <see cref="ChunkPos"/> values deterministically (Z, then X).
    /// </summary>
    /// <param name="left">Left position.</param>
    /// <param name="right">Right position.</param>
    /// <returns>Signed comparison result.</returns>
    public static int Compare(ChunkPos left, ChunkPos right) => left.CompareTo(right);

    /// <summary>Determines whether <paramref name="left"/> precedes <paramref name="right"/>.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> is less than <paramref name="right"/>.</returns>
    public static bool operator <(ChunkPos left, ChunkPos right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether <paramref name="left"/> precedes or equals <paramref name="right"/>.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> is less than or equal to <paramref name="right"/>.</returns>
    public static bool operator <=(ChunkPos left, ChunkPos right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether <paramref name="left"/> follows <paramref name="right"/>.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> is greater than <paramref name="right"/>.</returns>
    public static bool operator >(ChunkPos left, ChunkPos right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether <paramref name="left"/> follows or equals <paramref name="right"/>.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> is greater than or equal to <paramref name="right"/>.</returns>
    public static bool operator >=(ChunkPos left, ChunkPos right) => left.CompareTo(right) >= 0;

    /// <summary>
    /// Packs X and Z into a single 64-bit integer key for dictionary lookups.
    /// </summary>
    /// <returns>Packed 64-bit chunk key.</returns>
    public long PackInt64() => ((long)(uint)X) | ((long)(uint)Z << 32);

    /// <summary>
    /// Resolves a chunk-local coordinate (<c>0..15</c>) and world Y to a world <see cref="BlockPos"/>.
    /// </summary>
    /// <param name="localX">Local X in <c>[0, 15]</c>.</param>
    /// <param name="worldY">World Y coordinate.</param>
    /// <param name="localZ">Local Z in <c>[0, 15]</c>.</param>
    /// <returns>World <see cref="BlockPos"/>.</returns>
    public BlockPos GetBlockPos(int localX, int worldY, int localZ) =>
        new((X << 4) + (localX & 15), worldY, (Z << 4) + (localZ & 15));

    /// <summary>
    /// Computes the Chebyshev (chessboard) chunk distance to <paramref name="other"/>.
    /// </summary>
    /// <param name="other">Target chunk position.</param>
    /// <returns>Maximum of <c>|dX|</c> and <c>|dZ|</c>.</returns>
    public long ChessboardDistance(ChunkPos other) =>
        System.Math.Max(System.Math.Abs((long)X - other.X), System.Math.Abs((long)Z - other.Z));

    /// <inheritdoc />
    public int CompareTo(ChunkPos other)
    {
        var cmpZ = Z.CompareTo(other.Z);
        return cmpZ != 0 ? cmpZ : X.CompareTo(other.X);
    }
}
