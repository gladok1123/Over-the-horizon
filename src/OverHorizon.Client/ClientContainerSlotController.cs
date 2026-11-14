using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
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

    private readonly Dictionary<BlockPos, ContainerBlockEntity> _containers = [];

    private ItemStack _cursorStack = ItemStack.Empty;
    private BlockPos? _openedFurnacePos;
    private BlockPos? _openedContainerPos;
    private UiOverlayMode _openedContainerMode = UiOverlayMode.InGame;

    public ClientContainerSlotController(IRegistry<ItemDefinition> items, IRegistry<ICraftingRecipe> recipes)
    {
        _items = items;
        _recipes = recipes;
        _furnaceRecipes = new FurnaceRecipes(items);
    }

    public CraftingGrid Grid2x2 { get; } = new(2, 2);
    public CraftingGrid Grid3x3 { get; } = new(3, 3);
    public ItemStack CursorStack => _cursorStack;

    public void ClearCursor() => _cursorStack = ItemStack.Empty;

    public void PickOrPlaceCreativeCatalogItem(int catalogIndex, bool fullStack, ClientHotbar hotbar, GameMode mode)
    {
        var entry = hotbar.GetDetailedCatalogEntry(catalogIndex);
        if (!_items.TryGet(entry.Key, out var itemDef))
        {
            hotbar.EquipPaletteBlock(catalogIndex);
            return;
        }

        var rawId = _items.GetId(entry.Key);
        if (rawId <= 0)
        {
            return;
        }

        var itemId = (ushort)rawId;
        var count = fullStack ? itemDef.MaxStackSize : 1;
        if (_cursorStack.IsEmpty || _cursorStack.ItemId != itemId)
        {
            _cursorStack = new ItemStack(itemId, count);
        }
        else if (!fullStack)
        {
            _cursorStack = new ItemStack(itemId, Math.Min(itemDef.MaxStackSize, _cursorStack.Count + 1));
        }
        else
        {
            _cursorStack = new ItemStack(itemId, itemDef.MaxStackSize);
        }

        hotbar.RefreshSlotVisuals(mode);
    }

    public FurnaceBlockEntity? ActiveFurnace =>
        _openedFurnacePos is { } fp && _furnaces.TryGetValue(fp, out var fe) ? fe : null;

    /// <summary>Gets the <c>27</c>-slot Barrel or Chest currently open on screen, or <see langword="null"/> when none.</summary>
    public ContainerBlockEntity? ActiveContainer =>
        _openedContainerPos is { } cp && _containers.TryGetValue(cp, out var ce) ? ce : null;

    /// <summary>Gets the screen mode (<see cref="UiOverlayMode.BarrelScreen"/> / <see cref="UiOverlayMode.ChestScreen"/>) of the open container.</summary>
    public UiOverlayMode OpenedContainerMode => _openedContainerPos is null ? UiOverlayMode.InGame : _openedContainerMode;

    /// <summary>
    /// Opens (or closes) the <c>27</c>-slot Barrel / Chest container at <paramref name="pos"/>, maintaining the
    /// viewer count that drives the original <c>open</c> block-state property and open/close sound cues.
    /// </summary>
    /// <param name="mode">Container screen being opened.</param>
    /// <param name="pos">Container block position, or <see langword="null"/> to close any open container.</param>
    /// <returns><see langword="true"/> when this call flipped the container's open state.</returns>
    public bool OpenContainer(UiOverlayMode mode, BlockPos? pos)
    {
        CloseContainer();

        if (pos is not { } cp)
        {
            return false;
        }

        if (!_containers.TryGetValue(cp, out var entity))
        {
            entity = new ContainerBlockEntity(cp);
            _containers[cp] = entity;
        }

        entity.AddViewer();
        _openedContainerPos = cp;
        _openedContainerMode = mode;
        return entity.ViewerCount == 1;
    }

    /// <summary>Closes the currently open Barrel / Chest (if any).</summary>
    /// <returns>The container whose last viewer just closed it, or <see langword="null"/> when nothing changed.</returns>
    public ContainerBlockEntity? CloseContainer()
    {
        if (_openedContainerPos is not { } cp || !_containers.TryGetValue(cp, out var entity))
        {
            _openedContainerPos = null;
            return null;
        }

        _openedContainerPos = null;
        return entity.RemoveViewer() ? entity : null;
    }

    /// <summary>Looks up the persistent Barrel / Chest container at <paramref name="pos"/> without opening it.</summary>
    /// <param name="pos">Container block position.</param>
    /// <returns>The stored container, or <see langword="null"/> when none exists yet.</returns>
    public ContainerBlockEntity? PeekContainer(BlockPos pos) => _containers.GetValueOrDefault(pos);

    /// <summary>
    /// Removes the Barrel / Chest container at <paramref name="pos"/> and spills every stored stack into
    /// <paramref name="inv"/> (vanilla behaviour when the block is broken).
    /// </summary>
    /// <param name="pos">Container block position.</param>
    /// <param name="inv">Receiving player inventory.</param>
    /// <returns><see langword="true"/> when that container was the one currently open on screen.</returns>
    public bool RemoveContainerAndDropContents(BlockPos pos, PlayerInventory inv)
    {
        if (_containers.Remove(pos, out var entity))
        {
            entity.DropAllContents(s => _ = inv.AddItem(s, _items));
        }

        if (_openedContainerPos == pos)
        {
            _openedContainerPos = null;
            return true;
        }

        return false;
    }

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
        _ = CloseContainer();
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

        if (ActiveContainer is { } container &&
            cellId is >= ClientInventoryScreenBuilder.ContainerCellBase and < ClientInventoryScreenBuilder.ContainerCellBase + ContainerBlockEntity.SlotCount)
        {
            var slotIndex = cellId - ClientInventoryScreenBuilder.ContainerCellBase;
            var containerStack = container.GetSlot(slotIndex);
            SlotClickInteraction.ClickSlot(ref containerStack, ref _cursorStack, isRightClick, _items);
            container.SetSlot(slotIndex, containerStack);
            return "click_slot";
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
            if (player.Mode == GameMode.Creative && cellId < PlayerInventory.HotbarSlotCount)
            {
                player.Hotbar.SyncCreativeSlotFromInventory(cellId);
            }

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
