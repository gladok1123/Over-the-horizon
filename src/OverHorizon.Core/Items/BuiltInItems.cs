using OverHorizon.Core.Blocks;
using OverHorizon.Core.Registry;

namespace OverHorizon.Core.Items;

/// <summary>
/// Clean-room built-in item definitions for the OverHorizon MVP.
/// </summary>
public static class BuiltInItems
{
    /// <summary>Key for <c>overhorizon:air</c> (empty item ID 0).</summary>
    public static readonly ResourceKey Air = BuiltInBlocks.Air;

    /// <summary>Key for <c>overhorizon:stick</c>.</summary>
    public static readonly ResourceKey Stick = ResourceKey.OverHorizon("stick");

    /// <summary>Key for <c>overhorizon:wooden_pickaxe</c>.</summary>
    public static readonly ResourceKey WoodenPickaxe = ResourceKey.OverHorizon("wooden_pickaxe");

    /// <summary>
    /// Registers air, block-items for all non-fluid blocks in <paramref name="blocks"/>, and built-in items into <paramref name="registry"/>.
    /// </summary>
    /// <param name="registry">Target unfrozen item registry.</param>
    /// <param name="blocks">Block registry providing block-item entries.</param>
    public static void PopulateBuiltIns(MappedRegistry<ItemDefinition> registry, IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(blocks);

        _ = registry.Register(Air, new ItemDefinition(Air, MaxStackSize: 0));

        foreach (var block in blocks.Entries)
        {
            if (block.Key == BuiltInBlocks.Air || block.IsFluid)
            {
                continue;
            }

            if (registry.GetId(block.Key) < 0)
            {
                _ = registry.Register(
                    block.Key,
                    new ItemDefinition(block.Key, MaxStackSize: 64, PlacedBlockKey: block.Key));
            }
        }

        _ = registry.Register(Stick, new ItemDefinition(Stick, MaxStackSize: 64));
        _ = registry.Register(WoodenPickaxe, new ItemDefinition(WoodenPickaxe, MaxStackSize: 1));
    }

    /// <summary>
    /// Creates, populates, and freezes the default MVP item registry.
    /// </summary>
    /// <param name="blocks">Registered block registry used to create block-items.</param>
    /// <returns>Frozen <see cref="MappedRegistry{T}"/> of <see cref="ItemDefinition"/>.</returns>
    public static MappedRegistry<ItemDefinition> CreateFrozen(IBlockRegistry blocks)
    {
        var registry = new MappedRegistry<ItemDefinition>();
        PopulateBuiltIns(registry, blocks);
        registry.Freeze();
        return registry;
    }
}
