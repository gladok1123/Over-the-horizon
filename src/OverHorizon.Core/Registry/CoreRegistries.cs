using OverHorizon.Core.Biomes;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Entities;
using OverHorizon.Core.Items;

namespace OverHorizon.Core.Registry;

/// <summary>
/// Aggregates all frozen domain registries for injection across server and client subsystems.
/// </summary>
public sealed class CoreRegistries
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CoreRegistries"/> class.
    /// </summary>
    /// <param name="blocks">Frozen block registry.</param>
    /// <param name="items">Frozen item registry.</param>
    /// <param name="entities">Frozen entity type registry.</param>
    /// <param name="biomes">Frozen biome registry.</param>
    public CoreRegistries(
        IBlockRegistry blocks,
        IRegistry<ItemDefinition> items,
        IRegistry<EntityTypeDefinition> entities,
        IRegistry<BiomeDefinition> biomes)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(biomes);

        Blocks = blocks;
        Items = items;
        Entities = entities;
        Biomes = biomes;
    }

    /// <summary>Gets the frozen block and block-state registry.</summary>
    public IBlockRegistry Blocks { get; }

    /// <summary>Gets the frozen item registry.</summary>
    public IRegistry<ItemDefinition> Items { get; }

    /// <summary>Gets the frozen entity type registry.</summary>
    public IRegistry<EntityTypeDefinition> Entities { get; }

    /// <summary>Gets the frozen biome registry.</summary>
    public IRegistry<BiomeDefinition> Biomes { get; }

    /// <summary>
    /// Builds the standard frozen built-in MVP registries.
    /// </summary>
    /// <returns>Initialized <see cref="CoreRegistries"/> container.</returns>
    public static CoreRegistries CreateDefault()
    {
        var blocks = BuiltInBlocks.CreateFrozen();
        var items = BuiltInItems.CreateFrozen(blocks);
        var entities = BuiltInEntities.CreateFrozen();
        var biomes = BuiltInBiomes.CreateFrozen(blocks);
        return new CoreRegistries(blocks, items, entities, biomes);
    }
}
