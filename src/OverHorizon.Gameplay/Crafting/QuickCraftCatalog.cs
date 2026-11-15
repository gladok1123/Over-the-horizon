using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay.Inventory;

namespace OverHorizon.Gameplay.Crafting;

/// <summary>
/// Provides one-click Survival crafting and field-smelting recipes directly against <see cref="PlayerInventory"/>.
/// </summary>
public static class QuickCraftCatalog
{
    private static readonly QuickCraftEntry[] AllEntries =
    [
        new("4x Oak Planks (1 Log)", BuiltInBlocks.OakPlanks, 4, [(BuiltInBlocks.OakLog, 1)]),
        new("4x Sticks (2 Planks)", BuiltInItems.Stick, 4, [(BuiltInBlocks.OakPlanks, 2)]),
        new("Crafting Table (4 Planks)", BuiltInBlocks.CraftingTable, 1, [(BuiltInBlocks.OakPlanks, 4)]),
        new("Wooden Pickaxe (3 Planks + 2 Sticks)", BuiltInItems.WoodenPickaxe, 1, [(BuiltInBlocks.OakPlanks, 3), (BuiltInItems.Stick, 2)]),
        new("Wooden Sword (2 Planks + 1 Stick)", BuiltInItems.WoodenSword, 1, [(BuiltInBlocks.OakPlanks, 2), (BuiltInItems.Stick, 1)]),
        new("Stone Pickaxe (3 Cobble + 2 Sticks)", BuiltInItems.StonePickaxe, 1, [(BuiltInBlocks.Cobblestone, 3), (BuiltInItems.Stick, 2)]),
        new("Stone Sword (2 Cobble + 1 Stick)", BuiltInItems.StoneSword, 1, [(BuiltInBlocks.Cobblestone, 2), (BuiltInItems.Stick, 1)]),
        new("Smelt Iron Ingot (Iron Ore + Coal)", BuiltInItems.IronIngot, 1, [(BuiltInBlocks.IronOre, 1), (BuiltInItems.Coal, 1)]),
        new("Smelt Gold Ingot (Gold Ore + Coal)", BuiltInItems.GoldIngot, 1, [(BuiltInBlocks.GoldOre, 1), (BuiltInItems.Coal, 1)]),
        new("Iron Pickaxe (3 Iron + 2 Sticks)", BuiltInItems.IronPickaxe, 1, [(BuiltInItems.IronIngot, 3), (BuiltInItems.Stick, 2)]),
        new("Iron Sword (2 Iron + 1 Stick)", BuiltInItems.IronSword, 1, [(BuiltInItems.IronIngot, 2), (BuiltInItems.Stick, 1)]),
        new("Iron Axe (3 Iron + 2 Sticks)", BuiltInItems.IronAxe, 1, [(BuiltInItems.IronIngot, 3), (BuiltInItems.Stick, 2)]),
        new("Iron Shovel (1 Iron + 2 Sticks)", BuiltInItems.IronShovel, 1, [(BuiltInItems.IronIngot, 1), (BuiltInItems.Stick, 2)]),
        new("Bucket (3 Iron Ingots)", BuiltInItems.Bucket, 1, [(BuiltInItems.IronIngot, 3)]),
        new("Shears (2 Iron Ingots)", BuiltInItems.Shears, 1, [(BuiltInItems.IronIngot, 2)]),
        new("Flint & Steel (Iron + Gravel)", BuiltInItems.FlintAndSteel, 1, [(BuiltInItems.IronIngot, 1), (BuiltInBlocks.Gravel, 1)]),
        new("Diamond Sword (2 Diamond + 1 Stick)", BuiltInItems.DiamondSword, 1, [(BuiltInItems.Diamond, 2), (BuiltInItems.Stick, 1)]),
        new("Diamond Pickaxe (3 Diamond + 2 Sticks)", BuiltInItems.DiamondPickaxe, 1, [(BuiltInItems.Diamond, 3), (BuiltInItems.Stick, 2)]),
        new("Diamond Axe (3 Diamond + 2 Sticks)", BuiltInItems.DiamondAxe, 1, [(BuiltInItems.Diamond, 3), (BuiltInItems.Stick, 2)]),
        new("Golden Apple (4 Gold + 1 Apple)", BuiltInItems.GoldenApple, 1, [(BuiltInItems.GoldIngot, 4), (BuiltInItems.Apple, 1)]),
        new("4x Stone Bricks (4 Cobblestone)", BuiltInBlocks.StoneBricks, 4, [(BuiltInBlocks.Cobblestone, 4)]),
        new("2x TNT (4 Sand + 2 Coal)", BuiltInBlocks.Tnt, 2, [(BuiltInBlocks.Sand, 4), (BuiltInItems.Coal, 2)]),
    ];

    /// <summary>Gets all available Survival crafting entries.</summary>
    public static IReadOnlyList<QuickCraftEntry> Entries => AllEntries;

    /// <summary>
    /// Checks whether <paramref name="inventory"/> currently holds all required ingredients for <paramref name="entry"/>.
    /// </summary>
    public static bool CanCraft(PlayerInventory inventory, QuickCraftEntry entry, IRegistry<ItemDefinition> items)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(items);

        for (var i = 0; i < entry.Ingredients.Count; i++)
        {
            var (key, needed) = entry.Ingredients[i];
            var id = items.GetId(key);
            if (id <= 0 || inventory.CountItem((ushort)id) < needed)
            {
                return false;
            }
        }

        return items.GetId(entry.OutputItemKey) > 0;
    }

    /// <summary>
    /// Consumes ingredients for <paramref name="entry"/> from <paramref name="inventory"/> and inserts the crafted output.
    /// </summary>
    public static bool TryCraft(PlayerInventory inventory, QuickCraftEntry entry, IRegistry<ItemDefinition> items)
    {
        if (!CanCraft(inventory, entry, items))
        {
            return false;
        }

        var outId = (ushort)items.GetId(entry.OutputItemKey);
        for (var i = 0; i < entry.Ingredients.Count; i++)
        {
            var (key, needed) = entry.Ingredients[i];
            _ = inventory.RemoveItem((ushort)items.GetId(key), needed);
        }

        _ = inventory.AddItem(new ItemStack(outId, entry.OutputCount), items);
        return true;
    }
}
