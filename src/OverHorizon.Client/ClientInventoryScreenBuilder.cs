using System.Numerics;
using OverHorizon.Core.Items;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay.Crafting;
using OverHorizon.Gameplay.Inventory;
using OverHorizon.Gameplay.Smelting;
using OverHorizon.Rendering.Hud;

namespace OverHorizon.Client;

/// <summary>
/// Builds interactive buttons, draggable sliders (<c>2..64</c> chunk Render Distance), and pixel-exact <c>16x16</c>
/// container slot cells aligned to <c>23w13a__or__b</c> <c>inventory.png</c> (<c>2x2</c>), <c>crafting_table.png</c> (<c>3x3</c>),
/// and <c>furnace.png</c> (<c>3</c>-slot smelting).
/// </summary>
internal static class ClientInventoryScreenBuilder
{
    public const float ContainerGuiScale = 3.0f;
    public const int CreativeCols = 9;
    public const int CreativeVisibleRows = 4;
    public const int CraftGridCellBase = 300;
    public const int CraftOutputCellId = 399;
    public const int FurnaceInputCellId = 400;
    public const int FurnaceFuelCellId = 401;
    public const int FurnaceOutputCellId = 402;
    public const int CreativeHotbarCellOffset = 2000;

    private static readonly (string Id, string Label, ItemCategory? Filter)[] CreativeTabs =
    [
        ("tab_0", "ALL", null),
        ("tab_1", "BUILDING", ItemCategory.BuildingBlocks),
        ("tab_2", "NATURAL", ItemCategory.NaturalBlocks),
        ("tab_3", "FUNCTIONAL", ItemCategory.FunctionalBlocks),
        ("tab_4", "COMBAT & TOOLS", ItemCategory.CombatAndTools),
        ("tab_5", "FOOD & MATS", ItemCategory.FoodAndMaterials),
    ];

    public static (float PanelX, float PanelY, float PanelW, float PanelH) GetPanelBounds(float width, float height)
    {
        const float panelW = 760.0f;
        var panelH = MathF.Min(540.0f, height - 90.0f);
        var panelX = (width - panelW) * 0.5f;
        var panelY = MathF.Max(34.0f, (height - panelH) * 0.44f);
        return (panelX, panelY, panelW, panelH);
    }

    public static (float WinX, float WinY, float WinW, float WinH) GetContainerWindowBounds(float width, float height)
    {
        const float winW = 176.0f * ContainerGuiScale;
        const float winH = 166.0f * ContainerGuiScale;
        var winX = MathF.Round((width - winW) * 0.5f);
        var winY = MathF.Max(22.0f, MathF.Round((height - winH) * 0.44f));
        return (winX, winY, winW, winH);
    }

    public static void BuildCreativeCatalog(
        float width,
        float height,
        Vector2 mousePos,
        ClientPlayerController player,
        int activeTabIndex,
        string searchQuery,
        int scrollRow,
        List<UiButtonItem> buttons,
        List<UiInventoryCell> cells)
    {
        var (panelX, panelY, panelW, panelH) = GetPanelBounds(width, height);

        var tabY = panelY + 46.0f;
        const float tabGap = 6.0f;
        var tabW = (panelW - 36.0f - ((CreativeTabs.Length - 1) * tabGap)) / CreativeTabs.Length;
        for (var t = 0; t < CreativeTabs.Length; t++)
        {
            var tx = panelX + 18.0f + (t * (tabW + tabGap));
            var hov = Contains(mousePos, tx, tabY, tabW, 28.0f);
            buttons.Add(new UiButtonItem(CreativeTabs[t].Id, CreativeTabs[t].Label, tx, tabY, tabW, 28.0f, hov, IsInputField: false, IsFocused: t == activeTabIndex));
        }

        var filtered = GetFilteredCatalogIndices(player.Hotbar, activeTabIndex, searchQuery);
        var totalRows = Math.Max(1, (filtered.Count + CreativeCols - 1) / CreativeCols);
        var clampedRow = Math.Clamp(scrollRow, 0, Math.Max(0, totalRows - CreativeVisibleRows));

        const float cellSize = 52.0f;
        const float gap = 8.0f;
        var gridW = (CreativeCols * cellSize) + ((CreativeCols - 1) * gap);
        var startX = panelX + ((panelW - gridW) * 0.5f);
        var startY = tabY + 40.0f;

        var startItem = clampedRow * CreativeCols;
        var endItem = Math.Min(filtered.Count, startItem + (CreativeVisibleRows * CreativeCols));
        for (var idx = startItem; idx < endItem; idx++)
        {
            var local = idx - startItem;
            var col = local % CreativeCols;
            var row = local / CreativeCols;
            var x = startX + (col * (cellSize + gap));
            var y = startY + (row * (cellSize + gap));
            var entry = player.Hotbar.GetDetailedCatalogEntry(filtered[idx]);
            cells.Add(new UiInventoryCell(
                entry.Index,
                entry.DisplayName,
                entry.Color,
                x,
                y,
                cellSize,
                Contains(mousePos, x, y, cellSize, cellSize),
                entry.AtlasTile,
                StackCount: 1,
                entry.TooltipDetail));
        }

        var hotbarY = startY + (CreativeVisibleRows * (cellSize + gap)) + 12.0f;
        var tiles = player.Hotbar.ActiveSlotTiles.Span;
        var colors = player.Hotbar.ActiveSlotColors.Span;
        for (var s = 0; s < PlayerInventory.HotbarSlotCount; s++)
        {
            var hx = startX + (s * (cellSize + gap));
            cells.Add(new UiInventoryCell(
                CreativeHotbarCellOffset + s,
                $"Hotbar Slot {s + 1} (Click to select target slot)",
                colors[s],
                hx,
                hotbarY,
                cellSize,
                Contains(mousePos, hx, hotbarY, cellSize, cellSize),
                tiles[s],
                StackCount: 1,
                "Hover any item above and press 1-9 to assign directly!",
                IsSelected: s == player.Hotbar.SelectedSlot));
        }

        if (totalRows > CreativeVisibleRows)
        {
            var scrollX = startX + gridW + 10.0f;
            buttons.Add(new UiButtonItem("scroll_up", "^", scrollX, startY, 28.0f, 32.0f, Contains(mousePos, scrollX, startY, 28.0f, 32.0f)));
            var dnY = startY + (CreativeVisibleRows * (cellSize + gap)) - 40.0f;
            buttons.Add(new UiButtonItem("scroll_down", "v", scrollX, dnY, 28.0f, 32.0f, Contains(mousePos, scrollX, dnY, 28.0f, 32.0f)));
        }

        var doneY = panelY + panelH - 36.0f;
        buttons.Add(new UiButtonItem("switch_to_survival", "Survival Mode", panelX + 18.0f, doneY, 175.0f, 28.0f, Contains(mousePos, panelX + 18.0f, doneY, 175.0f, 28.0f)));
        buttons.Add(new UiButtonItem("resume", "Close (Esc)", panelX + panelW - 178.0f, doneY, 160.0f, 28.0f, Contains(mousePos, panelX + panelW - 178.0f, doneY, 160.0f, 28.0f)));
    }

    public static void BuildContainerScreen(
        UiOverlayMode mode,
        float width,
        float height,
        Vector2 mousePos,
        ClientPlayerController player,
        IRegistry<ItemDefinition> items,
        IRegistry<ICraftingRecipe> recipes,
        CraftingGrid grid2x2,
        CraftingGrid grid3x3,
        FurnaceBlockEntity? activeFurnace,
        List<UiButtonItem> buttons,
        List<UiInventoryCell> cells)
    {
        var (winX, winY, _, _) = GetContainerWindowBounds(width, height);
        const float s = ContainerGuiScale;
        const float slotSize = 16.0f * s;
        var inv = player.Hotbar.SurvivalInventory;

        // 3x9 Main Inventory Storage at (8, 84) and 1x9 Hotbar at (8, 142) — identical across Inventory, Crafting Table & Furnace!
        for (var i = 9; i < PlayerInventory.MainSlotCount; i++)
        {
            var rel = i - 9;
            var col = rel % 9;
            var row = rel / 9;
            AddStackCell(cells, inv.GetSlot(i), i, winX + ((8.0f + (col * 18.0f)) * s), winY + ((84.0f + (row * 18.0f)) * s), slotSize, mousePos, items, player.Hotbar, string.Empty);
        }

        for (var i = 0; i < PlayerInventory.HotbarSlotCount; i++)
        {
            AddStackCell(cells, inv.GetSlot(i), i, winX + ((8.0f + (i * 18.0f)) * s), winY + (142.0f * s), slotSize, mousePos, items, player.Hotbar, string.Empty);
        }

        if (mode == UiOverlayMode.SurvivalInventory)
        {
            string[] armorNames = ["Helmet Slot", "Chestplate Slot", "Leggings Slot", "Boots Slot"];
            for (var a = 0; a < 4; a++)
            {
                var slotIdx = PlayerInventory.ArmorSlotOffset + a;
                AddStackCell(cells, inv.GetSlot(slotIdx), slotIdx, winX + (8.0f * s), winY + ((8.0f + (a * 18.0f)) * s), slotSize, mousePos, items, player.Hotbar, armorNames[a]);
            }

            AddStackCell(cells, inv.GetSlot(PlayerInventory.OffhandSlotIndex), PlayerInventory.OffhandSlotIndex, winX + (77.0f * s), winY + (62.0f * s), slotSize, mousePos, items, player.Hotbar, "Offhand Slot");

            for (var r = 0; r < 2; r++)
            {
                for (var c = 0; c < 2; c++)
                {
                    var cellId = CraftGridCellBase + (r * 2) + c;
                    AddStackCell(cells, grid2x2.GetSlot(c, r), cellId, winX + ((98.0f + (c * 18.0f)) * s), winY + ((18.0f + (r * 18.0f)) * s), slotSize, mousePos, items, player.Hotbar, "2x2 Crafting Input");
                }
            }

            var preview2 = BuiltInRecipes.FindMatch(grid2x2, recipes)?.Output ?? ItemStack.Empty;
            AddStackCell(cells, preview2, CraftOutputCellId, winX + (154.0f * s), winY + (28.0f * s), slotSize, mousePos, items, player.Hotbar, "Crafted Output (Left-Click: Take 1 | Shift/Right-Click: Craft All)");
        }
        else if (mode == UiOverlayMode.CraftingTableScreen)
        {
            for (var r = 0; r < 3; r++)
            {
                for (var c = 0; c < 3; c++)
                {
                    var cellId = CraftGridCellBase + (r * 3) + c;
                    AddStackCell(cells, grid3x3.GetSlot(c, r), cellId, winX + ((30.0f + (c * 18.0f)) * s), winY + ((17.0f + (r * 18.0f)) * s), slotSize, mousePos, items, player.Hotbar, "3x3 Crafting Table Input");
                }
            }

            var preview3 = BuiltInRecipes.FindMatch(grid3x3, recipes)?.Output ?? ItemStack.Empty;
            AddStackCell(cells, preview3, CraftOutputCellId, winX + (124.0f * s), winY + (35.0f * s), slotSize, mousePos, items, player.Hotbar, "Crafted Output (Left-Click: Take 1 | Right-Click: Craft Stack)");
        }
        else if (mode == UiOverlayMode.FurnaceScreen && activeFurnace is not null)
        {
            AddStackCell(cells, activeFurnace.InputSlot, FurnaceInputCellId, winX + (56.0f * s), winY + (17.0f * s), slotSize, mousePos, items, player.Hotbar, "Smelting Input (Raw Ore, Sand, Clay, Raw Meat, Log)");
            AddStackCell(cells, activeFurnace.FuelSlot, FurnaceFuelCellId, winX + (56.0f * s), winY + (53.0f * s), slotSize, mousePos, items, player.Hotbar, "Fuel Slot (Coal, Charcoal, Planks, Logs, Sticks)");
            AddStackCell(cells, activeFurnace.OutputSlot, FurnaceOutputCellId, winX + (116.0f * s), winY + (35.0f * s), slotSize, mousePos, items, player.Hotbar, "Smelted Output");
        }

        _ = buttons;
    }

    public static List<int> GetFilteredCatalogIndices(ClientHotbar hotbar, int tabIndex, string searchQuery)
    {
        var filterCat = CreativeTabs[Math.Clamp(tabIndex, 0, CreativeTabs.Length - 1)].Filter;
        var q = searchQuery.Trim();
        var result = new List<int>(ClientHotbar.PaletteCount);

        for (var i = 0; i < ClientHotbar.PaletteCount; i++)
        {
            var e = hotbar.GetDetailedCatalogEntry(i);
            if (filterCat is not null && e.Category != filterCat)
            {
                continue;
            }

            if (q.Length > 0 &&
                !e.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) &&
                !e.Key.Path.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(i);
        }

        return result;
    }

    public static void BuildStandardMenuButtons(
        UiOverlayMode mode,
        float width,
        float height,
        Vector2 mousePos,
        ClientPlayerController player,
        string serverEndpointInput,
        string usernameInput,
        int focusedInputField,
        float fovDegrees,
        int activeViewDistance,
        List<UiButtonItem> buttons)
    {
        var cx = width * 0.5f;
        const float bw = 420.0f;
        const float bh = 42.0f;
        var bx = cx - (bw * 0.5f);

        void AddBtn(string id, string label, float x, float y, float w = bw, bool isInput = false, bool focused = false, bool isSlider = false, float sliderVal = 0.0f) =>
            buttons.Add(new UiButtonItem(id, label, x, y, w, bh, Contains(mousePos, x, y, w, bh), isInput, focused)
            {
                IsSlider = isSlider,
                SliderNormalizedValue = sliderVal,
            });

        switch (mode)
        {
            case UiOverlayMode.TitleMenu:
                var ty = MathF.Max(205.0f, height * 0.38f);
                AddBtn("singleplayer", "Singleplayer (Creative)", bx, ty, 204.0f);
                AddBtn("singleplayer_survival", "Singleplayer (Survival)", bx + 216.0f, ty, 204.0f);
                AddBtn("open_multiplayer", "Multiplayer (PvP & Friends)", bx, ty + 54.0f);
                AddBtn("open_options", "Options...", bx, ty + 108.0f, 204.0f);
                AddBtn("quit_game", "Quit Game", bx + 216.0f, ty + 108.0f, 204.0f);
                break;

            case UiOverlayMode.MultiplayerMenu:
                var my = MathF.Max(165.0f, height * 0.28f);
                AddBtn("focus_endpoint", "Server: " + serverEndpointInput, bx, my, bw, isInput: true, focused: focusedInputField == 0);
                AddBtn("focus_username", "Name:   " + usernameInput, bx, my + 54.0f, bw, isInput: true, focused: focusedInputField == 1);
                AddBtn("join_server", "Join Friend's Server", bx, my + 118.0f);
                AddBtn("host_and_join", "Host Local Server (25565) & Play", bx, my + 172.0f);
                AddBtn("back_to_title", "Back to Title Screen", bx, my + 234.0f);
                break;

            case UiOverlayMode.OptionsMenu:
                var oy = MathF.Max(150.0f, height * 0.24f);
                var vdNorm = Math.Clamp((activeViewDistance - 2.0f) / 62.0f, 0.0f, 1.0f);
                var fovNorm = Math.Clamp((fovDegrees - 70.0f) / 40.0f, 0.0f, 1.0f);
                var volNorm = Math.Clamp(player.MasterVolume, 0.0f, 1.0f);
                var sensNorm = Math.Clamp((player.MouseSensitivity - 0.03f) / 0.22f, 0.0f, 1.0f);

                AddBtn("toggle_gamemode", $"Game Mode: {player.Mode} (F4)", bx, oy);
                AddBtn("slider_view_dist", $"Render Distance: {activeViewDistance} Chunks", bx, oy + 50.0f, bw, isSlider: true, sliderVal: vdNorm);
                AddBtn("slider_fov", $"FOV: {fovDegrees:F0} deg", bx, oy + 100.0f, bw, isSlider: true, sliderVal: fovNorm);
                AddBtn("slider_volume", $"Sound Volume: {(int)MathF.Round(player.MasterVolume * 100.0f)}%", bx, oy + 150.0f, bw, isSlider: true, sliderVal: volNorm);
                AddBtn("slider_sensitivity", $"Mouse Sensitivity: {(int)MathF.Round(player.MouseSensitivity * 1000.0f)}", bx, oy + 200.0f, bw, isSlider: true, sliderVal: sensNorm);
                AddBtn("options_done", "Done", bx, oy + 258.0f);
                break;

            case UiOverlayMode.PauseMenu:
                var py = MathF.Max(175.0f, height * 0.28f);
                AddBtn("resume", "Back to Game", bx, py);
                AddBtn("toggle_gamemode", $"Switch Mode (Current: {player.Mode})", bx, py + 52.0f);
                var invLabel = player.Mode == GameMode.Survival ? "Survival Inventory & 2x2 Crafting (E)" : "Creative Catalog (E)";
                AddBtn("open_inventory", invLabel, bx, py + 104.0f);
                AddBtn("open_options", "Options...", bx, py + 156.0f);
                AddBtn("save_and_title", "Save & Quit to Title", bx, py + 212.0f);
                break;

            case UiOverlayMode.DeathScreen:
                var dy = MathF.Max(260.0f, height * 0.48f);
                AddBtn("respawn_player", "Respawn", bx, dy);
                AddBtn("save_and_title", "Title Screen", bx, dy + 56.0f);
                break;
        }
    }

    private static void AddStackCell(
        List<UiInventoryCell> cells,
        ItemStack stack,
        int cellId,
        float x,
        float y,
        float size,
        Vector2 mousePos,
        IRegistry<ItemDefinition> items,
        ClientHotbar hotbar,
        string emptyHint)
    {
        var hovered = Contains(mousePos, x, y, size, size);
        if (stack.IsEmpty)
        {
            cells.Add(new UiInventoryCell(cellId, emptyHint, new Vector3(0.35f, 0.35f, 0.37f), x, y, size, hovered, AtlasTileIndex: -1, StackCount: 0));
            return;
        }

        var def = items.GetById(stack.ItemId);
        var title = stack.Count > 1 ? $"{def.EffectiveName} (x{stack.Count})" : def.EffectiveName;
        cells.Add(new UiInventoryCell(
            cellId,
            title,
            ClientHotbar.ResolveFallbackColor(def.Key),
            x,
            y,
            size,
            hovered,
            hotbar.ResolveAtlasTile(def.Key),
            stack.Count,
            hotbar.BuildTooltipDetail(def.Key)));
    }

    private static bool Contains(Vector2 pt, float x, float y, float w, float h) =>
        pt.X >= x && pt.X <= x + w && pt.Y >= y && pt.Y <= y + h;
}
