using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay.Crafting;
using OverHorizon.Gameplay.Inventory;
using OverHorizon.Gameplay.Smelting;
using OverHorizon.Rendering.Hud;

namespace OverHorizon.Client;

/// <summary>
/// Manages the floating cursor <see cref="ItemStack"/>, <c>2x2</c> Survival crafting grid, <c>3x3</c> Crafting Table grid,
/// and per-position <see cref="FurnaceBlockEntity"/> states for <see cref="ClientMenuController"/>.
/// </summary>
internal sealed class ClientContainerSlotController
{
    private readonly IRegistry<ItemDefinition> _items;
    private readonly IRegistry<ICraftingRecipe> _recipes;
    private readonly FurnaceRecipes _furnaceRecipes;
    private readonly Dictionary<BlockPos, FurnaceBlockEntity> _furnaces = [];

    private ItemStack _cursorStack = ItemStack.Empty;
    private BlockPos? _openedFurnacePos;

    public ClientContainerSlotController(IRegistry<ItemDefinition> items, IRegistry<ICraftingRecipe> recipes)
    {
        _items = items;
        _recipes = recipes;
        _furnaceRecipes = new FurnaceRecipes(items);
    }

    public CraftingGrid Grid2x2 { get; } = new(2, 2);
    public CraftingGrid Grid3x3 { get; } = new(3, 3);
    public ItemStack CursorStack => _cursorStack;

    public FurnaceBlockEntity? ActiveFurnace =>
        _openedFurnacePos is { } fp && _furnaces.TryGetValue(fp, out var fe) ? fe : null;

    public FurnaceBlockEntity GetOrCreateFurnace(BlockPos pos)
    {
        if (!_furnaces.TryGetValue(pos, out var entity))
        {
            entity = new FurnaceBlockEntity(pos);
            _furnaces[pos] = entity;
        }

        return entity;
    }

    public void OpenFurnace(BlockPos? pos)
    {
        if (pos is { } fp)
        {
            _ = GetOrCreateFurnace(fp);
            _openedFurnacePos = fp;
        }
        else
        {
            _openedFurnacePos = null;
        }
    }

    public bool RemoveFurnaceAndDropContents(BlockPos pos, PlayerInventory inv)
    {
        if (_furnaces.Remove(pos, out var entity))
        {
            if (!entity.InputSlot.IsEmpty) _ = inv.AddItem(entity.InputSlot, _items);
            if (!entity.FuelSlot.IsEmpty) _ = inv.AddItem(entity.FuelSlot, _items);
            if (!entity.OutputSlot.IsEmpty) _ = inv.AddItem(entity.OutputSlot, _items);
        }

        if (_openedFurnacePos == pos)
        {
            _openedFurnacePos = null;
            return true;
        }

        return false;
    }

    public void TickFurnaces(float dt)
    {
        foreach (var (_, furnace) in _furnaces)
        {
            _ = furnace.Tick(dt, _furnaceRecipes, _items);
        }
    }

    public byte ComputeNearbyLitFurnaceLight(Vec3d eyePos)
    {
        byte best = 0;
        foreach (var (pos, furnace) in _furnaces)
        {
            if (!furnace.IsLit) continue;
            var dist = eyePos.DistanceTo(new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5));
            if (dist < 13.0)
            {
                var atten = (byte)Math.Clamp((int)Math.Round(13.0 - dist), 0, 13);
                if (atten > best) best = atten;
            }
        }

        return best;
    }

    public void RefundCursorAndGrids(ClientPlayerController player)
    {
        var inv = player.Hotbar.SurvivalInventory;
        if (!_cursorStack.IsEmpty)
        {
            _ = inv.AddItem(_cursorStack, _items);
            _cursorStack = ItemStack.Empty;
        }

        RefundGrid(Grid2x2, inv);
        RefundGrid(Grid3x3, inv);
        _openedFurnacePos = null;
        player.Hotbar.RefreshSlotVisuals(player.Mode);
    }

    public string HandleCellClick(UiOverlayMode mode, int cellId, bool isRightClick, ClientPlayerController player)
    {
        var inv = player.Hotbar.SurvivalInventory;

        if (cellId == ClientInventoryScreenBuilder.CraftOutputCellId)
        {
            var grid = mode == UiOverlayMode.CraftingTableScreen ? Grid3x3 : Grid2x2;
            var crafted = SlotClickInteraction.TryTakeCraftingOutput(grid, _recipes, _items, inv, ref _cursorStack, craftAllToInventory: isRightClick);
            player.Hotbar.RefreshSlotVisuals(player.Mode);
            return crafted ? "crafted_item" : "craft_failed";
        }

        if (cellId is >= ClientInventoryScreenBuilder.CraftGridCellBase and < ClientInventoryScreenBuilder.CraftOutputCellId)
        {
            var local = cellId - ClientInventoryScreenBuilder.CraftGridCellBase;
            var grid = mode == UiOverlayMode.CraftingTableScreen ? Grid3x3 : Grid2x2;
            var gx = local % grid.Width;
            var gy = local / grid.Width;
            var slotStack = grid.GetSlot(gx, gy);
            SlotClickInteraction.ClickSlot(ref slotStack, ref _cursorStack, isRightClick, _items);
            grid.SetSlot(gx, gy, slotStack);
            return "click_slot";
        }

        if (mode == UiOverlayMode.FurnaceScreen && ActiveFurnace is { } furnace)
        {
            if (cellId == ClientInventoryScreenBuilder.FurnaceInputCellId)
            {
                var st = furnace.InputSlot;
                SlotClickInteraction.ClickSlot(ref st, ref _cursorStack, isRightClick, _items);
                furnace.InputSlot = st;
                return "click_slot";
            }

            if (cellId == ClientInventoryScreenBuilder.FurnaceFuelCellId)
            {
                var st = furnace.FuelSlot;
                SlotClickInteraction.ClickSlot(ref st, ref _cursorStack, isRightClick, _items, s => _furnaceRecipes.GetFuelBurnSeconds(s.ItemId) > 0.0f);
                furnace.FuelSlot = st;
                return "click_slot";
            }

            if (cellId == ClientInventoryScreenBuilder.FurnaceOutputCellId)
            {
                var st = furnace.OutputSlot;
                SlotClickInteraction.ClickSlot(ref st, ref _cursorStack, isRightClick, _items, static _ => false);
                furnace.OutputSlot = st;
                return "crafted_item";
            }
        }

        if (cellId is >= 0 and < PlayerInventory.TotalSlotCount)
        {
            var st = inv.GetSlot(cellId);
            Func<ItemStack, bool>? filter = null;
            if (cellId >= PlayerInventory.ArmorSlotStart && cellId < PlayerInventory.ArmorSlotStart + 4)
            {
                var reqSub = cellId - PlayerInventory.ArmorSlotStart;
                filter = stack => MatchesArmorSubSlot(_items.GetById(stack.ItemId).Key.Path, reqSub);
            }

            SlotClickInteraction.ClickSlot(ref st, ref _cursorStack, isRightClick, _items, filter);
            inv.SetSlot(cellId, st);
            player.Hotbar.RefreshSlotVisuals(player.Mode);
        }

        return "click_slot";
    }

    private static bool MatchesArmorSubSlot(string path, int sub) => sub switch
    {
        0 => path.EndsWith("helmet", StringComparison.Ordinal),
        1 => path.EndsWith("chestplate", StringComparison.Ordinal),
        2 => path.EndsWith("leggings", StringComparison.Ordinal),
        3 => path.EndsWith("boots", StringComparison.Ordinal),
        _ => false,
    };

    private void RefundGrid(CraftingGrid grid, PlayerInventory inv)
    {
        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                var st = grid.GetSlot(x, y);
                if (!st.IsEmpty)
                {
                    _ = inv.AddItem(st, _items);
                    grid.SetSlot(x, y, ItemStack.Empty);
                }
            }
        }
    }
}
