using OverHorizon.Core.Items;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay.Crafting;

namespace OverHorizon.Gameplay.Inventory;

/// <summary>
/// Authentic Minecraft cursor-stack slot click mechanics (left-click pickup/place/merge/swap,
/// right-click half-split/place-one, and crafting grid output extraction).
/// </summary>
public static class SlotClickInteraction
{
    /// <summary>
    /// Applies a standard left-click or right-click between <paramref name="slotStack"/> and <paramref name="cursorStack"/>.
    /// </summary>
    public static void ClickSlot(
        ref ItemStack slotStack,
        ref ItemStack cursorStack,
        bool isRightClick,
        IRegistry<ItemDefinition> items,
        Func<ItemStack, bool>? canPlaceFilter = null)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (cursorStack.IsEmpty)
        {
            if (slotStack.IsEmpty)
            {
                return;
            }

            if (isRightClick && slotStack.Count > 1)
            {
                var half = (slotStack.Count + 1) / 2;
                var (taken, rem) = slotStack.Split(half);
                cursorStack = taken;
                slotStack = rem;
            }
            else
            {
                cursorStack = slotStack;
                slotStack = ItemStack.Empty;
            }

            return;
        }

        if (canPlaceFilter is not null && !canPlaceFilter(cursorStack))
        {
            return;
        }

        var maxStack = GetMaxStack(cursorStack.ItemId, items);
        if (slotStack.IsEmpty)
        {
            var placeCount = isRightClick ? 1 : Math.Min(maxStack, cursorStack.Count);
            slotStack = new ItemStack(cursorStack.ItemId, placeCount);
            cursorStack = cursorStack.WithCount(cursorStack.Count - placeCount);
            return;
        }

        if (slotStack.CanMergeWith(cursorStack))
        {
            var room = maxStack - slotStack.Count;
            if (room <= 0)
            {
                return;
            }

            var transfer = isRightClick ? 1 : Math.Min(room, cursorStack.Count);
            slotStack = slotStack.WithCount(slotStack.Count + transfer);
            cursorStack = cursorStack.WithCount(cursorStack.Count - transfer);
            return;
        }

        if (!isRightClick && cursorStack.Count <= GetMaxStack(cursorStack.ItemId, items))
        {
            (slotStack, cursorStack) = (cursorStack, slotStack);
        }
    }

    /// <summary>
    /// Attempts to take crafted output from <paramref name="grid"/> into <paramref name="cursorStack"/>
    /// (or shift-transfer all craftable batches directly into <paramref name="inventory"/>).
    /// </summary>
    public static bool TryTakeCraftingOutput(
        CraftingGrid grid,
        IRegistry<ICraftingRecipe> recipes,
        IRegistry<ItemDefinition> items,
        PlayerInventory inventory,
        ref ItemStack cursorStack,
        bool craftAllToInventory = false)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(recipes);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(inventory);

        var match = BuiltInRecipes.FindMatch(grid, recipes);
        if (match is null)
        {
            return false;
        }

        var result = match.Output;
        var maxStack = GetMaxStack(result.ItemId, items);

        if (craftAllToInventory)
        {
            var craftedAny = false;
            for (var guard = 0; guard < 64; guard++)
            {
                var nextMatch = BuiltInRecipes.FindMatch(grid, recipes);
                if (nextMatch is null || nextMatch.Output.ItemId != result.ItemId)
                {
                    break;
                }

                var leftover = inventory.AddItem(nextMatch.Output, items);
                if (!leftover.IsEmpty)
                {
                    // Refund if partially/fully blocked when starting batch
                    if (leftover.Count == nextMatch.Output.Count)
                    {
                        break;
                    }

                    grid.ConsumeOnePerOccupiedSlot();
                    craftedAny = true;
                    break;
                }

                grid.ConsumeOnePerOccupiedSlot();
                craftedAny = true;
            }

            return craftedAny;
        }

        if (cursorStack.IsEmpty)
        {
            grid.ConsumeOnePerOccupiedSlot();
            cursorStack = result;
            return true;
        }

        if (cursorStack.CanMergeWith(result) && cursorStack.Count + result.Count <= maxStack)
        {
            grid.ConsumeOnePerOccupiedSlot();
            cursorStack = cursorStack.WithCount(cursorStack.Count + result.Count);
            return true;
        }

        return false;
    }

    private static int GetMaxStack(ushort itemId, IRegistry<ItemDefinition> items) =>
        items.TryGetById(itemId, out var def) ? Math.Max(1, def.MaxStackSize) : 64;
}
