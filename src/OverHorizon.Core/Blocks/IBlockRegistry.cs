using OverHorizon.Core.Registry;

namespace OverHorizon.Core.Blocks;

/// <summary>
/// Specialized block registry providing O(1) flyweight access to interned <see cref="BlockState"/> metadata.
/// </summary>
public interface IBlockRegistry : IRegistry<BlockDefinition>
{
    /// <summary>
    /// Gets the total number of interned <see cref="BlockState"/> permutations across all registered blocks.
    /// </summary>
    int StateCount { get; }

    /// <summary>
    /// Returns the default <see cref="BlockState"/> for <paramref name="blockKey"/>.
    /// </summary>
    /// <param name="blockKey">Registered block key.</param>
    /// <returns>Default <see cref="BlockState"/>.</returns>
    BlockState GetDefaultState(ResourceKey blockKey);

    /// <summary>
    /// Resolves the flyweight <see cref="BlockStateMetadata"/> for an interned <paramref name="state"/>.
    /// </summary>
    /// <param name="state">Interned block state.</param>
    /// <returns>Flyweight <see cref="BlockStateMetadata"/>.</returns>
    BlockStateMetadata GetStateMetadata(BlockState state);

    /// <summary>
    /// Returns the interned <see cref="BlockState"/> resulting from setting <paramref name="propertyName"/> to <paramref name="propertyValue"/>.
    /// </summary>
    /// <param name="state">Source block state.</param>
    /// <param name="propertyName">Property name defined on the block.</param>
    /// <param name="propertyValue">Allowed value for that property.</param>
    /// <returns>Resulting interned <see cref="BlockState"/>.</returns>
    BlockState WithProperty(BlockState state, string propertyName, string propertyValue);
}
