using System.Numerics;
using OverHorizon.Core.Items;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Crafting;
using OverHorizon.Gameplay.Inventory;
using OverHorizon.Rendering.Hud;

namespace OverHorizon.Client;

/// <summary>
/// Builds interactive buttons and <c>16x16</c> sprite cells for the Redesigned Tabbed Creative Catalog
/// and the 36-slot Survival Inventory &amp; Quick-Crafting window.
/// </summary>
internal static class ClientInventoryScreenBuilder
{
    public const int CreativeCols = 9;
    public const int CreativeVisibleRows = 4;
    public const int SurvivalRecipeOffset = 1000;
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

        // Interactive 9-slot Hotbar strip inside Creative panel
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

    public static void BuildSurvivalInventoryAndCrafting(
        float width,
        float height,
        Vector2 mousePos,
        ClientPlayerController player,
        IRegistry<ItemDefinition> items,
        int selectedSwapSlot,
        int recipeScrollRow,
        List<UiButtonItem> buttons,
        List<UiInventoryCell> cells)
    {
        var (panelX, panelY, panelW, panelH) = GetPanelBounds(width, height);
        var inv = player.Hotbar.SurvivalInventory;

        const float slotSize = 44.0f;
        const float gap = 5.0f;
        var invStartX = panelX + 20.0f;
        var backpackStartY = panelY + 68.0f;

        for (var i = 9; i < PlayerInventory.MainSlotCount; i++)
        {
            var rel = i - 9;
            var col = rel % 9;
            var row = rel / 9;
            var sx = invStartX + (col * (slotSize + gap));
            var sy = backpackStartY + (row * (slotSize + gap));
            AddSurvivalSlotCell(cells, inv, items, player.Hotbar, i, sx, sy, slotSize, mousePos, selectedSwapSlot);
        }

        var hotbarY = backpackStartY + (3 * (slotSize + gap)) + 16.0f;
        for (var i = 0; i < PlayerInventory.HotbarSlotCount; i++)
        {
            var sx = invStartX + (i * (slotSize + gap));
            AddSurvivalSlotCell(cells, inv, items, player.Hotbar, i, sx, hotbarY, slotSize, mousePos, selectedSwapSlot);
        }

        const int craftCols = 4;
        const int craftRows = 4;
        const float craftCell = 50.0f;
        var craftStartX = panelX + 478.0f;
        var craftStartY = panelY + 68.0f;

        var entries = QuickCraftCatalog.Entries;
        var maxRecipeRow = Math.Max(0, ((entries.Count + craftCols - 1) / craftCols) - craftRows);
        var rowOffset = Math.Clamp(recipeScrollRow, 0, maxRecipeRow);
        var firstIdx = rowOffset * craftCols;
        var lastIdx = Math.Min(entries.Count, firstIdx + (craftCols * craftRows));

        for (var rIdx = firstIdx; rIdx < lastIdx; rIdx++)
        {
            var local = rIdx - firstIdx;
            var col = local % craftCols;
            var row = local / craftCols;
            var cx = craftStartX + (col * (craftCell + 6.0f));
            var cy = craftStartY + (row * (craftCell + 6.0f));
            var recipe = entries[rIdx];
            var canCraft = QuickCraftCatalog.CanCraft(inv, recipe, items);

            cells.Add(new UiInventoryCell(
                SurvivalRecipeOffset + rIdx,
                (canCraft ? "[CRAFT] " : "[NEED] ") + recipe.Label,
                ClientHotbar.ResolveFallbackColor(recipe.OutputItemKey),
                cx,
                cy,
                craftCell,
                Contains(mousePos, cx, cy, craftCell, craftCell),
                player.Hotbar.ResolveAtlasTile(recipe.OutputItemKey),
                recipe.OutputCount,
                canCraft ? "Click to craft directly into your inventory!" : "Mine blocks & ores to gather required materials",
                IsSelected: canCraft));
        }

        buttons.Add(new UiButtonItem("scroll_up", "Recipes ^", craftStartX, craftStartY + 232.0f, 106.0f, 28.0f, Contains(mousePos, craftStartX, craftStartY + 232.0f, 106.0f, 28.0f)));
        buttons.Add(new UiButtonItem("scroll_down", "Recipes v", craftStartX + 114.0f, craftStartY + 232.0f, 106.0f, 28.0f, Contains(mousePos, craftStartX + 114.0f, craftStartY + 232.0f, 106.0f, 28.0f)));

        var doneY = panelY + panelH - 36.0f;
        buttons.Add(new UiButtonItem("switch_to_creative", "Creative Mode", panelX + 20.0f, doneY, 175.0f, 28.0f, Contains(mousePos, panelX + 20.0f, doneY, 175.0f, 28.0f)));
        buttons.Add(new UiButtonItem("resume", "Close (Esc / E)", panelX + panelW - 188.0f, doneY, 170.0f, 28.0f, Contains(mousePos, panelX + panelW - 188.0f, doneY, 170.0f, 28.0f)));
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

    private static void AddSurvivalSlotCell(
        List<UiInventoryCell> cells,
        PlayerInventory inv,
        IRegistry<ItemDefinition> items,
        ClientHotbar hotbar,
        int slotIdx,
        float x,
        float y,
        float size,
        Vector2 mousePos,
        int selectedSwapSlot)
    {
        var stack = inv.GetSlot(slotIdx);
        if (stack.IsEmpty)
        {
            cells.Add(new UiInventoryCell(
                slotIdx,
                $"Empty Slot {slotIdx + 1}",
                new Vector3(0.20f, 0.21f, 0.24f),
                x,
                y,
                size,
                Contains(mousePos, x, y, size, size),
                AtlasTileIndex: -1,
                StackCount: 0,
                "Click an item slot then click here to move it",
                IsSelected: slotIdx == selectedSwapSlot));
            return;
        }

        var def = items.GetById(stack.ItemId);
        cells.Add(new UiInventoryCell(
            slotIdx,
            $"{def.EffectiveName} x{stack.Count}",
            ClientHotbar.ResolveFallbackColor(def.Key),
            x,
            y,
            size,
            Contains(mousePos, x, y, size, size),
            hotbar.ResolveAtlasTile(def.Key),
            stack.Count,
            hotbar.BuildTooltipDetail(def.Key),
            IsSelected: slotIdx == selectedSwapSlot));
    }

    private static bool Contains(Vector2 pt, float x, float y, float w, float h) =>
        pt.X >= x && pt.X <= x + w && pt.Y >= y && pt.Y <= y + h;

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
        const float bh = 44.0f;
        var bx = cx - (bw * 0.5f);

        void AddBtn(string id, string label, float x, float y, float w = bw, bool isInput = false, bool focused = false) =>
            buttons.Add(new UiButtonItem(id, label, x, y, w, bh, Contains(mousePos, x, y, w, bh), isInput, focused));

        switch (mode)
        {
            case UiOverlayMode.TitleMenu:
                var ty = MathF.Max(205.0f, height * 0.38f);
                AddBtn("singleplayer", "Singleplayer (Creative)", bx, ty, 204.0f);
                AddBtn("singleplayer_survival", "Singleplayer (Survival)", bx + 216.0f, ty, 204.0f);
                AddBtn("open_multiplayer", "Multiplayer (PvP & Friends)", bx, ty + 56.0f);
                AddBtn("open_options", "Options...", bx, ty + 112.0f, 204.0f);
                AddBtn("quit_game", "Quit Game", bx + 216.0f, ty + 112.0f, 204.0f);
                break;

            case UiOverlayMode.MultiplayerMenu:
                var my = MathF.Max(165.0f, height * 0.28f);
                AddBtn("focus_endpoint", "Server: " + serverEndpointInput, bx, my, bw, isInput: true, focused: focusedInputField == 0);
                AddBtn("focus_username", "Name:   " + usernameInput, bx, my + 56.0f, bw, isInput: true, focused: focusedInputField == 1);
                AddBtn("join_server", "Join Friend's Server", bx, my + 122.0f);
                AddBtn("host_and_join", "Host Local Server (25565) & Play", bx, my + 178.0f);
                AddBtn("back_to_title", "Back to Title Screen", bx, my + 242.0f);
                break;

            case UiOverlayMode.OptionsMenu:
                var oy = MathF.Max(160.0f, height * 0.25f);
                AddBtn("toggle_gamemode", $"Game Mode: {player.Mode} (F4)", bx, oy);
                AddBtn("cycle_fov", $"FOV: {fovDegrees:F0} deg", bx, oy + 52.0f);
                AddBtn("cycle_view_dist", $"Render Distance: {activeViewDistance} chunks", bx, oy + 104.0f);
                AddBtn("cycle_volume", $"Sound Volume: {(int)(player.MasterVolume * 100.0f)}%", bx, oy + 156.0f);
                AddBtn("cycle_sensitivity", $"Mouse Sensitivity: {(int)(player.MouseSensitivity * 1000.0f)}", bx, oy + 208.0f);
                AddBtn("options_done", "Done", bx, oy + 266.0f);
                break;

            case UiOverlayMode.PauseMenu:
                var py = MathF.Max(175.0f, height * 0.28f);
                AddBtn("resume", "Back to Game", bx, py);
                AddBtn("toggle_gamemode", $"Switch Mode (Current: {player.Mode})", bx, py + 54.0f);
                var invLabel = player.Mode == GameMode.Survival ? "Survival Inventory & Crafting (E)" : "Creative Catalog (E)";
                AddBtn("open_inventory", invLabel, bx, py + 108.0f);
                AddBtn("open_options", "Options...", bx, py + 162.0f);
                AddBtn("save_and_title", "Save & Quit to Title", bx, py + 220.0f);
                break;

            case UiOverlayMode.DeathScreen:
                var dy = MathF.Max(260.0f, height * 0.48f);
                AddBtn("respawn_player", "Respawn", bx, dy);
                AddBtn("save_and_title", "Title Screen", bx, dy + 58.0f);
                break;
        }
    }
}
