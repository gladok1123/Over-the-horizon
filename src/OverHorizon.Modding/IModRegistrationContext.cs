using OverHorizon.Core.Biomes;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Entities;
using OverHorizon.Core.Items;
using OverHorizon.Core.Registry;

namespace OverHorizon.Modding;

/// <summary>
/// Exposes pre-freeze content registration endpoints to mods and JSON datapacks during startup.
/// </summary>
public interface IModRegistrationContext
{
    /// <summary>
    /// Registers a custom <see cref="BlockDefinition"/> (and optionally a matching placeable block-item).
    /// </summary>
    /// <param name="definition">Block definition.</param>
    /// <param name="registerBlockItem">When <see langword="true"/>, also registers a matching 64-stack <see cref="ItemDefinition"/>.</param>
    /// <returns>Default interned <see cref="BlockState"/> of the registered block.</returns>
    BlockState RegisterBlock(BlockDefinition definition, bool registerBlockItem = true);

    /// <summary>
    /// Registers a custom <see cref="ItemDefinition"/> and returns its assigned raw item ID.
    /// </summary>
    /// <param name="definition">Item definition.</param>
    /// <returns>Assigned dense item ID.</returns>
    int RegisterItem(ItemDefinition definition);

    /// <summary>
    /// Registers a custom <see cref="EntityTypeDefinition"/> and returns its assigned raw entity type ID.
    /// </summary>
    /// <param name="definition">Entity type definition.</param>
    /// <returns>Assigned dense entity type ID.</returns>
    int RegisterEntity(EntityTypeDefinition definition);

    /// <summary>
    /// Registers a custom <see cref="BiomeDefinition"/> and returns its assigned raw biome ID.
    /// </summary>
    /// <param name="definition">Biome definition.</param>
    /// <returns>Assigned dense biome ID.</returns>
    int RegisterBiome(BiomeDefinition definition);

    /// <summary>
    /// Looks up the default <see cref="BlockState"/> of an already-registered block (built-in or from an earlier dependency mod).
    /// </summary>
    /// <param name="blockKey">Block resource key.</param>
    /// <returns>Default <see cref="BlockState"/>.</returns>
    BlockState GetDefaultBlockState(ResourceKey blockKey);
}
