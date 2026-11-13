using System.Globalization;
using System.Numerics;
using OverHorizon.Core.Items;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Crafting;
using OverHorizon.Gameplay.Inventory;
using OverHorizon.Gameplay.Smelting;
using OverHorizon.Rendering.Hud;

namespace OverHorizon.Client;

/// <summary>
/// Builds interactive buttons, World Select / Create World controls, pixel-exact <c>195x136</c> Creative Inventory tabs &amp; slots,
/// and <c>176x166</c> Survival / Crafting Table / Furnace container slot cells.
/// </summary>
internal static class ClientInventoryScreenBuilder
{
    public const float ContainerGuiScale = 3.0f;
    public const float CreativeGuiScale = 3.0f;
    public const int CreativeCols = 9;
    public const int CreativeVisibleRows = 5;
    public const int CraftGridCellBase = 300;
    public const int CraftOutputCellId = 399;
    public const int FurnaceInputCellId = 400;
    public const int FurnaceFuelCellId = 401;
    public const int FurnaceOutputCellId = 402;
    public const int CreativeDeleteTrashCellId = 499;
    public const int CreativeCatalogCellOffset = 1000;

    private static readonly (string Id, string Title, ItemCategory? Filter)[] CreativeTabs =
    [
        ("tab_0", "Building Blocks", ItemCategory.BuildingBlocks),
        ("tab_1", "Natural Blocks", ItemCategory.NaturalBlocks),
        ("tab_2", "Functional Blocks", ItemCategory.FunctionalBlocks),
        ("tab_3", "Redstone & Pistons", ItemCategory.Redstone),
        ("tab_4", "Combat & Tools", ItemCategory.CombatAndTools),
        ("tab_5", "Search All Items", null),
        ("tab_6", "Survival Inventory", null),
    ];

    public static string GetCreativeTabTitle(int tabIndex) =>
        CreativeTabs[Math.Clamp(tabIndex, 0, CreativeTabs.Length - 1)].Title;

    public static (float WinX, float WinY, float WinW, float WinH) GetContainerWindowBounds(float width, float height)
    {
        const float winW = 176.0f * ContainerGuiScale;
        const float winH = 166.0f * ContainerGuiScale;
        var winX = MathF.Round((width - winW) * 0.5f);
        var winY = MathF.Max(22.0f, MathF.Round((height - winH) * 0.44f));
        return (winX, winY, winW, winH);
    }

    public static (float WinX, float WinY, float WinW, float WinH) GetCreativeWindowBounds(float width, float height)
    {
        const float winW = 195.0f * CreativeGuiScale;
        const float winH = 136.0f * CreativeGuiScale;
        var winX = MathF.Round((width - winW) * 0.5f);
        var winY = MathF.Max(48.0f, MathF.Round((height - winH) * 0.50f));
        return (winX, winY, winW, winH);
    }

    public static float BuildCreativeCatalog(
        float width,
        float height,
        Vector2 mousePos,
        ClientPlayerController player,
        IRegistry<ItemDefinition> items,
        int activeTabIndex,
        string searchQuery,
        int scrollRow,
        List<UiInventoryCell> cells)
    {
        var (winX, winY, _, _) = GetCreativeWindowBounds(width, height);
        const float s = CreativeGuiScale;
        const float slotSize = 16.0f * s;
        var inv = player.Hotbar.SurvivalInventory;
        var selTab = Math.Clamp(activeTabIndex, 0, 6);

        // 1x9 Live Hotbar at (9, 112) in Creative sheet (slots 0..8)
        for (var c = 0; c < PlayerInventory.HotbarSlotCount; c++)
        {
            AddStackCell(cells, inv.GetSlot(c), c, winX + ((9.0f + (c * 18.0f)) * s), winY + (112.0f * s), slotSize, mousePos, items, player.Hotbar, $"Hotbar Slot {c + 1}", isSelected: c == player.Hotbar.SelectedSlot);
        }

        if (selTab == 6)
        {
            // Creative Survival Inventory Tab (tab_inventory.png): 3x9 storage + 4 Armor + Offhand + Delete Item (X)
            for (var i = 9; i < PlayerInventory.MainSlotCount; i++)
            {
                var rel = i - 9;
                var col = rel % 9;
                var row = rel / 9;
                AddStackCell(cells, inv.GetSlot(i), i, winX + ((9.0f + (col * 18.0f)) * s), winY + ((54.0f + (row * 18.0f)) * s), slotSize, mousePos, items, player.Hotbar, string.Empty);
            }

            AddStackCell(cells, inv.GetSlot(PlayerInventory.ArmorSlotStart + 0), PlayerInventory.ArmorSlotStart + 0, winX + (54.0f * s), winY + (6.0f * s), slotSize, mousePos, items, player.Hotbar, "Helmet Slot");
            AddStackCell(cells, inv.GetSlot(PlayerInventory.ArmorSlotStart + 1), PlayerInventory.ArmorSlotStart + 1, winX + (54.0f * s), winY + (33.0f * s), slotSize, mousePos, items, player.Hotbar, "Chestplate Slot");
            AddStackCell(cells, inv.GetSlot(PlayerInventory.ArmorSlotStart + 2), PlayerInventory.ArmorSlotStart + 2, winX + (108.0f * s), winY + (6.0f * s), slotSize, mousePos, items, player.Hotbar, "Leggings Slot");
            AddStackCell(cells, inv.GetSlot(PlayerInventory.ArmorSlotStart + 3), PlayerInventory.ArmorSlotStart + 3, winX + (108.0f * s), winY + (33.0f * s), slotSize, mousePos, items, player.Hotbar, "Boots Slot");
            AddStackCell(cells, inv.GetSlot(PlayerInventory.OffhandSlotIndex), PlayerInventory.OffhandSlotIndex, winX + (35.0f * s), winY + (20.0f * s), slotSize, mousePos, items, player.Hotbar, "Offhand Slot");

            var tx = winX + (173.0f * s);
            var ty = winY + (112.0f * s);
            cells.Add(new UiInventoryCell(CreativeDeleteTrashCellId, "Destroy Item", new Vector3(0.85f, 0.20f, 0.20f), tx, ty, slotSize, Contains(mousePos, tx, ty, slotSize, slotSize), AtlasTileIndex: -1, StackCount: 0, "Click to destroy held item | Right-Click to clear all"));
            return 0.0f;
        }

        var filtered = GetFilteredCatalogIndices(player.Hotbar, selTab, searchQuery);
        var totalRows = Math.Max(1, (filtered.Count + CreativeCols - 1) / CreativeCols);
        var maxScroll = Math.Max(0, totalRows - CreativeVisibleRows);
        var clampedRow = Math.Clamp(scrollRow, 0, maxScroll);
        var startItem = clampedRow * CreativeCols;
        var endItem = Math.Min(filtered.Count, startItem + (CreativeVisibleRows * CreativeCols));

        for (var idx = startItem; idx < endItem; idx++)
        {
            var local = idx - startItem;
            var col = local % CreativeCols;
            var row = local / CreativeCols;
            var x = winX + ((9.0f + (col * 18.0f)) * s);
            var y = winY + ((18.0f + (row * 18.0f)) * s);
            var entry = player.Hotbar.GetDetailedCatalogEntry(filtered[idx]);
            cells.Add(new UiInventoryCell(
                CreativeCatalogCellOffset + entry.Index,
                entry.DisplayName,
                entry.Color,
                x,
                y,
                slotSize,
                Contains(mousePos, x, y, slotSize, slotSize),
                entry.AtlasTile,
                StackCount: 1,
                string.IsNullOrEmpty(entry.TooltipDetail) ? "Click to pick up | Right-Click: Stack | 1-9: Hotbar" : entry.TooltipDetail));
        }

        return maxScroll > 0 ? (float)clampedRow / maxScroll : 0.0f;
    }

    public static int HitTestCreativeTab(float width, float height, Vector2 mousePos)
    {
        var (winX, winY, _, _) = GetCreativeWindowBounds(width, height);
        const float s = CreativeGuiScale;
        var ty = winY - (28.0f * s);
        for (var t = 0; t < 7; t++)
        {
            var tx = t == 6 ? winX + ((195.0f - 26.0f) * s) : winX + (t * 27.0f * s);
            if (Contains(mousePos, tx, ty, 26.0f * s, 30.0f * s))
            {
                return t;
            }
        }

        return -1;
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
        List<UiInventoryCell> cells)
    {
        var (winX, winY, _, _) = GetContainerWindowBounds(width, height);
        const float s = ContainerGuiScale;
        const float slotSize = 16.0f * s;
        var inv = player.Hotbar.SurvivalInventory;

        for (var i = 9; i < PlayerInventory.MainSlotCount; i++)
        {
            var rel = i - 9;
            var col = rel % 9;
            var row = rel / 9;
            AddStackCell(cells, inv.GetSlot(i), i, winX + ((8.0f + (col * 18.0f)) * s), winY + ((84.0f + (row * 18.0f)) * s), slotSize, mousePos, items, player.Hotbar, string.Empty);
        }

        for (var i = 0; i < PlayerInventory.HotbarSlotCount; i++)
        {
            AddStackCell(cells, inv.GetSlot(i), i, winX + ((8.0f + (i * 18.0f)) * s), winY + (142.0f * s), slotSize, mousePos, items, player.Hotbar, string.Empty, isSelected: i == player.Hotbar.SelectedSlot);
        }

        if (mode == UiOverlayMode.SurvivalInventory)
        {
            string[] armorNames = ["Helmet Slot", "Chestplate Slot", "Leggings Slot", "Boots Slot"];
            for (var a = 0; a < 4; a++)
            {
                var slotIdx = PlayerInventory.ArmorSlotStart + a;
                AddStackCell(cells, inv.GetSlot(slotIdx), slotIdx, winX + (8.0f * s), winY + ((8.0f + (a * 18.0f)) * s), slotSize, mousePos, items, player.Hotbar, armorNames[a]);
            }

            AddStackCell(cells, inv.GetSlot(PlayerInventory.OffhandSlotIndex), PlayerInventory.OffhandSlotIndex, winX + (77.0f * s), winY + (62.0f * s), slotSize, mousePos, items, player.Hotbar, "Offhand Slot (F)");

            for (var r = 0; r < 2; r++)
            {
                for (var c = 0; c < 2; c++)
                {
                    var cellId = CraftGridCellBase + (r * 2) + c;
                    AddStackCell(cells, grid2x2.GetSlot(c, r), cellId, winX + ((98.0f + (c * 18.0f)) * s), winY + ((18.0f + (r * 18.0f)) * s), slotSize, mousePos, items, player.Hotbar, "2x2 Crafting Input");
                }
            }

            var preview2 = BuiltInRecipes.FindMatch(grid2x2, recipes)?.Output ?? ItemStack.Empty;
            AddStackCell(cells, preview2, CraftOutputCellId, winX + (154.0f * s), winY + (28.0f * s), slotSize, mousePos, items, player.Hotbar, "Crafted Output (Left-Click: Take 1 | Right-Click: Craft Stack)");
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
            AddStackCell(cells, activeFurnace.InputSlot, FurnaceInputCellId, winX + (56.0f * s), winY + (17.0f * s), slotSize, mousePos, items, player.Hotbar, "Smelting Input");
            AddStackCell(cells, activeFurnace.FuelSlot, FurnaceFuelCellId, winX + (56.0f * s), winY + (53.0f * s), slotSize, mousePos, items, player.Hotbar, "Fuel Slot");
            AddStackCell(cells, activeFurnace.OutputSlot, FurnaceOutputCellId, winX + (116.0f * s), winY + (35.0f * s), slotSize, mousePos, items, player.Hotbar, "Smelted Output");
        }
    }

    public static List<int> GetFilteredCatalogIndices(ClientHotbar hotbar, int tabIndex, string searchQuery)
    {
        var q = searchQuery.Trim();
        var filterCat = q.Length > 0 ? null : CreativeTabs[Math.Clamp(tabIndex, 0, CreativeTabs.Length - 1)].Filter;
        var result = new List<int>(ClientHotbar.PaletteCount);

        for (var i = 0; i < ClientHotbar.PaletteCount; i++)
        {
            var e = hotbar.GetDetailedCatalogEntry(i);
            if (filterCat is not null)
            {
                var matches = e.Category == filterCat || (filterCat == ItemCategory.CombatAndTools && e.Category == ItemCategory.FoodAndMaterials);
                if (!matches) continue;
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
        IReadOnlyList<SavedWorldMetadata> savedWorlds,
        int selectedWorldIndex,
        string newWorldName,
        GameMode newWorldGameMode,
        string newWorldSeed,
        List<UiButtonItem> buttons)
    {
        var cx = width * 0.5f;
        const float bw = 420.0f;
        const float bh = 42.0f;
        var bx = cx - (bw * 0.5f);

        void AddBtn(string id, string label, float x, float y, float w = bw, float h = bh, bool isInput = false, bool focused = false, bool isSlider = false, float sliderVal = 0.0f) =>
            buttons.Add(new UiButtonItem(id, label, x, y, w, h, Contains(mousePos, x, y, w, h), isInput, focused)
            {
                IsSlider = isSlider,
                SliderNormalizedValue = sliderVal,
            });

        switch (mode)
        {
            case UiOverlayMode.TitleMenu:
                var ty = MathF.Max(205.0f, height * 0.38f);
                AddBtn("open_world_select", "Singleplayer", bx, ty, bw);
                AddBtn("open_multiplayer", "Multiplayer", bx, ty + 52.0f, bw);
                AddBtn("open_options", "Options...", bx, ty + 112.0f, 204.0f);
                AddBtn("quit_game", "Quit Game", bx + 216.0f, ty + 112.0f, 204.0f);
                break;

            case UiOverlayMode.WorldSelectMenu:
                const float listW = 540.0f;
                var lx = cx - (listW * 0.5f);
                var ly = 96.0f;
                var maxRows = Math.Min(savedWorlds.Count, 5);
                for (var i = 0; i < maxRows; i++)
                {
                    var w = savedWorlds[i];
                    var prefix = i == selectedWorldIndex ? "> " : "  ";
                    var label = $"{prefix}{w.WorldName}  [{w.DefaultGameMode}]  Seed: {w.Seed.ToString(CultureInfo.InvariantCulture)}";
                    AddBtn($"select_world_{i.ToString(CultureInfo.InvariantCulture)}", label, lx, ly + (i * 48.0f), listW, 42.0f, focused: i == selectedWorldIndex);
                }

                var botY = height - 80.0f;
                AddBtn("play_selected_world", savedWorlds.Count > 0 ? "Play Selected World" : "Create First World", lx, botY, 264.0f, 38.0f);
                AddBtn("open_create_world", "Create New World", lx + 276.0f, botY, 264.0f, 38.0f);
                AddBtn("delete_selected_world", "Delete Selected", lx, botY + 42.0f, 264.0f, 32.0f);
                AddBtn("back_to_title", "Cancel", lx + 276.0f, botY + 42.0f, 264.0f, 32.0f);
                break;

            case UiOverlayMode.CreateWorldMenu:
                var cy = MathF.Max(132.0f, height * 0.24f);
                AddBtn("focus_world_name", "World Name: " + newWorldName, bx, cy, bw, bh, isInput: true, focused: focusedInputField == 0);
                AddBtn("toggle_create_gamemode", $"Game Mode: {newWorldGameMode}", bx, cy + 58.0f, bw, bh);
                var seedHint = string.IsNullOrEmpty(newWorldSeed) ? "(Random Seed)" : newWorldSeed;
                AddBtn("focus_world_seed", "Seed: " + seedHint, bx, cy + 116.0f, bw, bh, isInput: true, focused: focusedInputField == 1);
                AddBtn("confirm_create_world", "Create New World", bx, cy + 190.0f, 204.0f, bh);
                AddBtn("open_world_select", "Cancel", bx + 216.0f, cy + 190.0f, 204.0f, bh);
                break;

            case UiOverlayMode.MultiplayerMenu:
                var my = MathF.Max(148.0f, height * 0.26f);
                AddBtn("focus_endpoint", "Server: " + serverEndpointInput, bx, my, bw, bh, isInput: true, focused: focusedInputField == 0);
                AddBtn("focus_username", "Name:   " + usernameInput, bx, my + 54.0f, bw, bh, isInput: true, focused: focusedInputField == 1);
                AddBtn("join_server", "Join Server", bx, my + 118.0f);
                AddBtn("host_and_join", "Host Local Server (25565) & Play", bx, my + 172.0f);
                AddBtn("back_to_title", "Cancel", bx, my + 232.0f);
                break;

            case UiOverlayMode.OptionsMenu:
                var oy = MathF.Max(136.0f, height * 0.22f);
                var vdNorm = Math.Clamp((activeViewDistance - 2.0f) / 62.0f, 0.0f, 1.0f);
                var fovNorm = Math.Clamp((fovDegrees - 70.0f) / 40.0f, 0.0f, 1.0f);
                var volNorm = Math.Clamp(player.MasterVolume, 0.0f, 1.0f);
                var sensNorm = Math.Clamp((player.MouseSensitivity - 0.03f) / 0.22f, 0.0f, 1.0f);
                AddBtn("toggle_gamemode", $"Game Mode: {player.Mode} (F4)", bx, oy);
                AddBtn("slider_view_dist", $"Render Distance: {activeViewDistance} Chunks", bx, oy + 50.0f, bw, bh, isSlider: true, sliderVal: vdNorm);
                AddBtn("slider_fov", $"FOV: {fovDegrees:F0} deg", bx, oy + 100.0f, bw, bh, isSlider: true, sliderVal: fovNorm);
                AddBtn("slider_volume", $"Sound Volume: {(int)MathF.Round(player.MasterVolume * 100.0f)}%", bx, oy + 150.0f, bw, bh, isSlider: true, sliderVal: volNorm);
                AddBtn("slider_sensitivity", $"Mouse Sensitivity: {(int)MathF.Round(player.MouseSensitivity * 1000.0f)}", bx, oy + 200.0f, bw, bh, isSlider: true, sliderVal: sensNorm);
                AddBtn("options_done", "Done", bx, oy + 258.0f);
                break;

            case UiOverlayMode.PauseMenu:
                var py = MathF.Max(165.0f, height * 0.26f);
                AddBtn("resume", "Back to Game", bx, py);
                AddBtn("toggle_gamemode", $"Switch Mode (Current: {player.Mode})", bx, py + 52.0f);
                AddBtn("open_inventory", player.Mode == GameMode.Survival ? "Survival Inventory (E)" : "Creative Inventory (E)", bx, py + 104.0f);
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
        string emptyHint,
        bool isSelected = false)
    {
        var hovered = Contains(mousePos, x, y, size, size);
        if (stack.IsEmpty)
        {
            cells.Add(new UiInventoryCell(cellId, emptyHint, new Vector3(0.35f, 0.35f, 0.37f), x, y, size, hovered, AtlasTileIndex: -1, StackCount: 0, IsSelected: isSelected));
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
            hotbar.BuildTooltipDetail(def.Key),
            IsSelected: isSelected));
    }

    private static bool Contains(Vector2 pt, float x, float y, float w, float h) =>
        pt.X >= x && pt.X <= x + w && pt.Y >= y && pt.Y <= y + h;
}
