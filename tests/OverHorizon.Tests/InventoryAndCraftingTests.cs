using FluentAssertions;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay.Crafting;
using OverHorizon.Gameplay.Inventory;
using Xunit;

namespace OverHorizon.Tests;

public sealed class InventoryAndCraftingTests
{
    [Fact]
    public void PlayerInventory_MergesPartialStacksOverflowsAndSplitsDeterministically()
    {
        var registries = CoreRegistries.CreateDefault();
        var inv = new PlayerInventory();

        var dirtId = (ushort)registries.Items.GetId(BuiltInBlocks.Dirt);
        var pickaxeId = (ushort)registries.Items.GetId(BuiltInItems.WoodenPickaxe);

        inv.AddItem(new ItemStack(dirtId, 40), registries.Items).Should().Be(ItemStack.Empty);
        inv.AddItem(new ItemStack(dirtId, 50), registries.Items).Should().Be(ItemStack.Empty);

        // Slot 0 capped at 64, Slot 1 receives remaining 26.
        inv.GetSlot(0).Should().Be(new ItemStack(dirtId, 64));
        inv.GetSlot(1).Should().Be(new ItemStack(dirtId, 26));

        // Non-stackable tool (MaxStackSize = 1) occupies its own slot.
        inv.AddItem(new ItemStack(pickaxeId, 1), registries.Items).Should().Be(ItemStack.Empty);
        inv.GetSlot(2).Should().Be(new ItemStack(pickaxeId, 1));

        // Remove 65 dirt -> 25 remains in slot 1; split 13 out -> 12 stays.
        inv.RemoveItem(dirtId, 65).Should().Be(65);
        inv.GetSlot(1).Count.Should().Be(25);
        var splitTaken = inv.SplitSlot(1, 13);
        splitTaken.Should().Be(new ItemStack(dirtId, 13));
        inv.GetSlot(1).Should().Be(new ItemStack(dirtId, 12));

        // Swap hotbar slot with offhand and verify deterministic hash stability.
        inv.SwapSlots(2, PlayerInventory.OffhandSlotIndex);
        inv.OffhandItem.Should().Be(new ItemStack(pickaxeId, 1));

        var invCopy = new PlayerInventory();
        invCopy.SetSlot(1, new ItemStack(dirtId, 12));
        invCopy.SetSlot(PlayerInventory.OffhandSlotIndex, new ItemStack(pickaxeId, 1));

        inv.ComputeDeterministicHash().Should().Be(invCopy.ComputeDeterministicHash());
    }

    [Fact]
    public void Crafting_ProgressionFromOakLogToWoodenPickaxe_SucceedsAcross2x2And3x3Grids()
    {
        var registries = CoreRegistries.CreateDefault();
        var recipes = BuiltInRecipes.CreateFrozen(registries.Items);

        var logId = (ushort)registries.Items.GetId(BuiltInBlocks.OakLog);
        var planksId = (ushort)registries.Items.GetId(BuiltInBlocks.OakPlanks);
        var stickId = (ushort)registries.Items.GetId(BuiltInItems.Stick);
        var tableId = (ushort)registries.Items.GetId(BuiltInBlocks.CraftingTable);
        var pickaxeId = (ushort)registries.Items.GetId(BuiltInItems.WoodenPickaxe);

        // 1. Shapeless: 1 oak_log at bottom-right of 2x2 -> 4 oak_planks.
        var grid2x2 = CraftingGrid.Create2x2();
        grid2x2.SetSlot(1, 1, new ItemStack(logId, 2));
        var planks = BuiltInRecipes.CraftOnce(grid2x2, recipes);
        planks.Should().Be(new ItemStack(planksId, 4));
        grid2x2.GetSlot(1, 1).Count.Should().Be(1);

        // 2. Shaped 1x2: 2 oak_planks vertically in column 1 -> 4 stick (horizontal must NOT match).
        grid2x2.Clear();
        grid2x2.SetSlot(0, 0, new ItemStack(planksId, 1));
        grid2x2.SetSlot(1, 0, new ItemStack(planksId, 1));
        BuiltInRecipes.FindMatch(grid2x2, recipes).Should().BeNull();

        grid2x2.Clear();
        grid2x2.SetSlot(1, 0, new ItemStack(planksId, 1));
        grid2x2.SetSlot(1, 1, new ItemStack(planksId, 1));
        BuiltInRecipes.CraftOnce(grid2x2, recipes).Should().Be(new ItemStack(stickId, 4));

        // 3. Shaped 2x2 shifted into bottom-right corner of 3x3 -> 1 crafting_table.
        var grid3x3 = CraftingGrid.Create3x3();
        grid3x3.SetSlot(1, 1, new ItemStack(planksId, 1));
        grid3x3.SetSlot(2, 1, new ItemStack(planksId, 1));
        grid3x3.SetSlot(1, 2, new ItemStack(planksId, 1));
        grid3x3.SetSlot(2, 2, new ItemStack(planksId, 1));
        BuiltInRecipes.CraftOnce(grid3x3, recipes).Should().Be(new ItemStack(tableId, 1));

        // 4. Shaped 3x3: wooden pickaxe.
        grid3x3.Clear();
        grid3x3.SetSlot(0, 0, new ItemStack(planksId, 1));
        grid3x3.SetSlot(1, 0, new ItemStack(planksId, 1));
        grid3x3.SetSlot(2, 0, new ItemStack(planksId, 1));
        grid3x3.SetSlot(1, 1, new ItemStack(stickId, 1));
        grid3x3.SetSlot(1, 2, new ItemStack(stickId, 1));
        BuiltInRecipes.CraftOnce(grid3x3, recipes).Should().Be(new ItemStack(pickaxeId, 1));
    }

    [Fact]
    public void ShapedRecipe_SupportsHorizontalMirrorToggle()
    {
        var mirroredAxe = new ShapedRecipe(
            ResourceKey.OverHorizon("test_axe_mirrored"),
            patternWidth: 2,
            patternHeight: 3,
            pattern:
            [
                10, 10,
                10, 20,
                0, 20,
            ],
            output: new ItemStack(99, 1),
            allowHorizontalMirror: true);

        var strictAxe = new ShapedRecipe(
            ResourceKey.OverHorizon("test_axe_strict"),
            patternWidth: 2,
            patternHeight: 3,
            pattern:
            [
                10, 10,
                10, 20,
                0, 20,
            ],
            output: new ItemStack(99, 1),
            allowHorizontalMirror: false);

        var grid = CraftingGrid.Create3x3();

        // Place horizontally mirrored pattern on right side of 3x3 grid.
        grid.SetSlot(1, 0, new ItemStack(10, 1));
        grid.SetSlot(2, 0, new ItemStack(10, 1));
        grid.SetSlot(1, 1, new ItemStack(20, 1));
        grid.SetSlot(2, 1, new ItemStack(10, 1));
        grid.SetSlot(1, 2, new ItemStack(20, 1));

        mirroredAxe.Matches(grid).Should().BeTrue();
        strictAxe.Matches(grid).Should().BeFalse();
    }
}
