namespace OverHorizon.Core.Blocks;

/// <summary>
/// Compact 16-bit interned identifier representing an immutable block state (ADR 0006).
/// Occupies 2 bytes in memory; metadata is resolved via <see cref="IBlockRegistry"/> flyweight table.
/// </summary>
/// <param name="Id">Dense interned state identifier (<c>0</c> is always <c>overhorizon:air</c>).</param>
public readonly record struct BlockState(ushort Id) : IComparable<BlockState>
{
    /// <summary>
    /// Gets the canonical air block state (<c>Id == 0</c>).
    /// </summary>
    public static BlockState Air => default;

    /// <summary>
    /// Gets a value indicating whether this state represents air (<c>Id == 0</c>).
    /// </summary>
    public bool IsAir => Id == 0;

    /// <summary>
    /// Compares two <see cref="BlockState"/> values by their interned ID.
    /// </summary>
    /// <param name="left">Left state.</param>
    /// <param name="right">Right state.</param>
    /// <returns>Signed comparison result.</returns>
    public static int Compare(BlockState left, BlockState right) => left.CompareTo(right);

    /// <summary>Determines whether <paramref name="left"/> precedes <paramref name="right"/>.</summary>
    /// <param name="left">Left state.</param>
    /// <param name="right">Right state.</param>
    /// <returns><see langword="true"/> if less than.</returns>
    public static bool operator <(BlockState left, BlockState right) => left.Id < right.Id;

    /// <summary>Determines whether <paramref name="left"/> precedes or equals <paramref name="right"/>.</summary>
    /// <param name="left">Left state.</param>
    /// <param name="right">Right state.</param>
    /// <returns><see langword="true"/> if less than or equal to.</returns>
    public static bool operator <=(BlockState left, BlockState right) => left.Id <= right.Id;

    /// <summary>Determines whether <paramref name="left"/> follows <paramref name="right"/>.</summary>
    /// <param name="left">Left state.</param>
    /// <param name="right">Right state.</param>
    /// <returns><see langword="true"/> if greater than.</returns>
    public static bool operator >(BlockState left, BlockState right) => left.Id > right.Id;

    /// <summary>Determines whether <paramref name="left"/> follows or equals <paramref name="right"/>.</summary>
    /// <param name="left">Left state.</param>
    /// <param name="right">Right state.</param>
    /// <returns><see langword="true"/> if greater than or equal to.</returns>
    public static bool operator >=(BlockState left, BlockState right) => left.Id >= right.Id;

    /// <inheritdoc />
    public int CompareTo(BlockState other) => Id.CompareTo(other.Id);
}
