using OverHorizon.Core.Biomes;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Entities;
using OverHorizon.Core.Items;
using OverHorizon.Core.Registry;

namespace OverHorizon.Modding;

internal sealed class ModRegistrationContext : IModRegistrationContext
{
    private readonly BlockRegistry _blocks = new();
    private readonly MappedRegistry<ItemDefinition> _items = new();
    private readonly MappedRegistry<EntityTypeDefinition> _entities = new();
    private readonly MappedRegistry<BiomeDefinition> _biomes = new();

    public ModRegistrationContext()
    {
        BuiltInBlocks.PopulateBuiltIns(_blocks);
        BuiltInItems.PopulateBuiltIns(_items, _blocks);
        BuiltInEntities.PopulateBuiltIns(_entities);
        BuiltInBiomes.PopulateBuiltIns(_biomes, _blocks);
    }

    public BlockState RegisterBlock(BlockDefinition definition, bool registerBlockItem = true)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var defaultState = _blocks.Register(definition);

        if (registerBlockItem && !definition.IsFluid && _items.GetId(definition.Key) < 0)
        {
            _ = _items.Register(
                definition.Key,
                new ItemDefinition(definition.Key, MaxStackSize: 64, PlacedBlockKey: definition.Key));
        }

        return defaultState;
    }

    public int RegisterItem(ItemDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return _items.Register(definition.Key, definition);
    }

    public int RegisterEntity(EntityTypeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return _entities.Register(definition.Key, definition);
    }

    public int RegisterBiome(BiomeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return _biomes.Register(definition.Key, definition);
    }

    public BlockState GetDefaultBlockState(ResourceKey blockKey) =>
        _blocks.GetDefaultState(blockKey);

    public CoreRegistries FreezeAndBuild()
    {
        _blocks.Freeze();
        _items.Freeze();
        _entities.Freeze();
        _biomes.Freeze();
        return new CoreRegistries(_blocks, _items, _entities, _biomes);
    }
}
