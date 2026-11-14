namespace OverHorizon.Core.Math;

/// <summary>
/// Immutable 3D coordinate of a 16x16x16 cubic chunk section.
/// </summary>
/// <param name="X">Section X index.</param>
/// <param name="Y">Section Y index.</param>
/// <param name="Z">Section Z index.</param>
public readonly record struct SectionPos(int X, int Y, int Z)
{
    /// <summary>
    /// Returns the horizontal <see cref="ChunkPos"/> containing this section.
    /// </summary>
    /// <returns>Corresponding <see cref="ChunkPos"/>.</returns>
    public ChunkPos ToChunkPos() => new(X, Z);

    /// <summary>
    /// Returns the minimum corner <see cref="BlockPos"/> of this 16x16x16 section.
    /// </summary>
    /// <returns>Origin <see cref="BlockPos"/>.</returns>
    public BlockPos OriginBlockPos() => new(X << 4, Y << 4, Z << 4);
}
