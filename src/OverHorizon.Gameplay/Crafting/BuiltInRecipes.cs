using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Registry;

namespace OverHorizon.Gameplay.Crafting;

/// <summary>
/// Built-in clean-room crafting recipes and crafting execution helpers for the OverHorizon MVP.
/// </summary>
public static class BuiltInRecipes
{
    /// <summary>Recipe key: <c>1 oak_log -&gt; 4 oak_planks</c> (shapeless).</summary>
    public static readonly ResourceKey OakPlanksFromLog = ResourceKey.OverHorizon("oak_planks_from_log");

    /// <summary>Recipe key: <c>2 oak_planks (1x2) -&gt; 4 stick</c> (shaped).</summary>
    public static readonly ResourceKey SticksFromPlanks = ResourceKey.OverHorizon("sticks_from_planks");

    /// <summary>Recipe key: <c>4 oak_planks (2x2) -&gt; 1 crafting_table</c> (shaped).</summary>
    public static readonly ResourceKey CraftingTableFromPlanks = ResourceKey.OverHorizon("crafting_table");

    /// <summary>Recipe key: <c>3 oak_planks + 2 stick (3x3) -&gt; 1 wooden_pickaxe</c> (shaped).</summary>
    public static readonly ResourceKey WoodenPickaxe = ResourceKey.OverHorizon("wooden_pickaxe");

    /// <summary>
    /// Registers all built-in MVP crafting recipes into an unfrozen <paramref name="registry"/>.
    /// </summary>
    /// <param name="registry">Target unfrozen recipe registry.</param>
    /// <param name="items">Item registry providing raw item IDs.</param>
    public static void PopulateBuiltIns(MappedRegistry<ICraftingRecipe> registry, IRegistry<ItemDefinition> items)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(items);

        var logId = (ushort)items.GetId(BuiltInBlocks.OakLog);
        var planksId = (ushort)items.GetId(BuiltInBlocks.OakPlanks);
        var stickId = (ushort)items.GetId(BuiltInItems.Stick);
        var tableId = (ushort)items.GetId(BuiltInBlocks.CraftingTable);
        var pickaxeId = (ushort)items.GetId(BuiltInItems.WoodenPickaxe);

        _ = registry.Register(
            OakPlanksFromLog,
            new ShapelessRecipe(
                OakPlanksFromLog,
                [logId],
                new ItemStack(planksId, 4)));

        _ = registry.Register(
            SticksFromPlanks,
            new ShapedRecipe(
                SticksFromPlanks,
                patternWidth: 1,
                patternHeight: 2,
                pattern: [planksId, planksId],
                output: new ItemStack(stickId, 4)));

        _ = registry.Register(
            CraftingTableFromPlanks,
            new ShapedRecipe(
                CraftingTableFromPlanks,
                patternWidth: 2,
                patternHeight: 2,
                pattern: [planksId, planksId, planksId, planksId],
                output: new ItemStack(tableId, 1)));

        _ = registry.Register(
            WoodenPickaxe,
            new ShapedRecipe(
                WoodenPickaxe,
                patternWidth: 3,
                patternHeight: 3,
                pattern:
                [
                    planksId, planksId, planksId,
                    0, stickId, 0,
                    0, stickId, 0,
                ],
                output: new ItemStack(pickaxeId, 1)));
    }

    /// <summary>
    /// Creates, populates, and freezes the default MVP crafting recipe registry.
    /// </summary>
    /// <param name="items">Frozen item registry.</param>
    /// <returns>Frozen <see cref="MappedRegistry{T}"/> of <see cref="ICraftingRecipe"/>.</returns>
    public static MappedRegistry<ICraftingRecipe> CreateFrozen(IRegistry<ItemDefinition> items)
    {
        var registry = new MappedRegistry<ICraftingRecipe>();
        PopulateBuiltIns(registry, items);
        registry.Freeze();
        return registry;
    }

    /// <summary>
    /// Finds the first matching recipe in <paramref name="recipes"/> for <paramref name="grid"/>, or <see langword="null"/>.
    /// </summary>
    /// <param name="grid">Input crafting grid.</param>
    /// <param name="recipes">Recipe registry.</param>
    /// <returns>Matched <see cref="ICraftingRecipe"/>, or <see langword="null"/>.</returns>
    public static ICraftingRecipe? FindMatch(CraftingGrid grid, IRegistry<ICraftingRecipe> recipes)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(recipes);

        var entries = recipes.Entries;
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].Matches(grid))
            {
                return entries[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Attempts to craft one output batch from <paramref name="grid"/>, consuming 1 item per occupied input slot on success.
    /// </summary>
    /// <param name="grid">Input crafting grid.</param>
    /// <param name="recipes">Recipe registry.</param>
    /// <returns>Crafted <see cref="ItemStack"/> (<see cref="ItemStack.Empty"/> if no recipe matched).</returns>
    public static ItemStack CraftOnce(CraftingGrid grid, IRegistry<ICraftingRecipe> recipes)
    {
        var match = FindMatch(grid, recipes);
        if (match is null)
        {
            return ItemStack.Empty;
        }

        grid.ConsumeOnePerOccupiedSlot();
        return match.Output;
    }
}
