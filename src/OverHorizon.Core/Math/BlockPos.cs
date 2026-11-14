namespace OverHorizon.Core.Math;

/// <summary>
/// Immutable integer 3D block coordinate with deterministic Y-Z-X ordering and 64-bit packing.
/// </summary>
/// <param name="X">Block X coordinate.</param>
/// <param name="Y">Block Y coordinate.</param>
/// <param name="Z">Block Z coordinate.</param>
public readonly record struct BlockPos(int X, int Y, int Z) : IComparable<BlockPos>
{
    /// <summary>
    /// Gets the origin block position (0, 0, 0).
    /// </summary>
    public static BlockPos Zero => default;

    /// <summary>
    /// Gets the chunk-local X coordinate in <c>[0, 15]</c>.
    /// </summary>
    public int SectionLocalX => X & 15;

    /// <summary>
    /// Gets the section-local Y coordinate in <c>[0, 15]</c>.
    /// </summary>
    public int SectionLocalY => Y & 15;

    /// <summary>
    /// Gets the chunk-local Z coordinate in <c>[0, 15]</c>.
    /// </summary>
    public int SectionLocalZ => Z & 15;

    /// <summary>
    /// Unpacks a 64-bit integer previously created by <see cref="PackInt64"/> (26-bit X, 26-bit Z, 12-bit Y).
    /// </summary>
    /// <param name="packed">Packed 64-bit coordinate.</param>
    /// <returns>Unpacked <see cref="BlockPos"/>.</returns>
    public static BlockPos UnpackInt64(long packed)
    {
        var x = (int)(packed >> 38);
        var z = (int)((packed << 26) >> 38);
        var y = (int)((packed << 52) >> 52);
        return new BlockPos(x, y, z);
    }

    /// <summary>
    /// Compares two <see cref="BlockPos"/> instances deterministically (Y, then Z, then X).
    /// </summary>
    /// <param name="left">Left position.</param>
    /// <param name="right">Right position.</param>
    /// <returns>Signed comparison result.</returns>
    public static int Compare(BlockPos left, BlockPos right) => left.CompareTo(right);

    /// <summary>Determines whether <paramref name="left"/> precedes <paramref name="right"/>.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> is less than <paramref name="right"/>.</returns>
    public static bool operator <(BlockPos left, BlockPos right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether <paramref name="left"/> precedes or equals <paramref name="right"/>.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> is less than or equal to <paramref name="right"/>.</returns>
    public static bool operator <=(BlockPos left, BlockPos right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether <paramref name="left"/> follows <paramref name="right"/>.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> is greater than <paramref name="right"/>.</returns>
    public static bool operator >(BlockPos left, BlockPos right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether <paramref name="left"/> follows or equals <paramref name="right"/>.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> is greater than or equal to <paramref name="right"/>.</returns>
    public static bool operator >=(BlockPos left, BlockPos right) => left.CompareTo(right) >= 0;

    /// <summary>
    /// Packs X (signed 26-bit), Z (signed 26-bit), and Y (signed 12-bit) into a single 64-bit integer.
    /// </summary>
    /// <returns>Packed 64-bit representation.</returns>
    public long PackInt64() =>
        (((long)X & 0x3FFFFFFL) << 38) |
        (((long)Z & 0x3FFFFFFL) << 12) |
        ((long)Y & 0xFFFL);

    /// <summary>
    /// Returns a position offset by <paramref name="distance"/> blocks in <paramref name="direction"/>.
    /// </summary>
    /// <param name="direction">Cardinal direction.</param>
    /// <param name="distance">Number of blocks to offset.</param>
    /// <returns>Offset <see cref="BlockPos"/>.</returns>
    public BlockPos Offset(Direction direction, int distance = 1)
    {
        var n = direction.Normal();
        return new BlockPos(X + (n.X * distance), Y + (n.Y * distance), Z + (n.Z * distance));
    }

    /// <summary>
    /// Returns a position offset by explicit integer deltas.
    /// </summary>
    /// <param name="dx">Delta X.</param>
    /// <param name="dy">Delta Y.</param>
    /// <param name="dz">Delta Z.</param>
    /// <returns>Offset <see cref="BlockPos"/>.</returns>
    public BlockPos Offset(int dx, int dy, int dz) => new(X + dx, Y + dy, Z + dz);

    /// <summary>
    /// Returns the horizontal <see cref="ChunkPos"/> containing this block.
    /// </summary>
    /// <returns>Containing <see cref="ChunkPos"/>.</returns>
    public ChunkPos ToChunkPos() => new(X >> 4, Z >> 4);

    /// <summary>
    /// Returns the 3D 16x16x16 <see cref="SectionPos"/> containing this block.
    /// </summary>
    /// <returns>Containing <see cref="SectionPos"/>.</returns>
    public SectionPos ToSectionPos() => new(X >> 4, Y >> 4, Z >> 4);

    /// <summary>
    /// Returns the geometric center of this voxel block in world coordinates.
    /// </summary>
    /// <returns>Block center <see cref="Vec3d"/>.</returns>
    public Vec3d Center() => new(X + 0.5, Y + 0.5, Z + 0.5);

    /// <inheritdoc />
    public int CompareTo(BlockPos other)
    {
        var cmpY = Y.CompareTo(other.Y);
        if (cmpY != 0)
        {
            return cmpY;
        }

        var cmpZ = Z.CompareTo(other.Z);
        return cmpZ != 0 ? cmpZ : X.CompareTo(other.X);
    }
}
