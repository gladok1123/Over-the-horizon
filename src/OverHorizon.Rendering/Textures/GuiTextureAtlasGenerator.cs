namespace OverHorizon.Rendering.Textures;

/// <summary>
/// Builds a composite <c>512x768</c> RGBA8 GUI texture sheet packing six <c>256x256</c> real game GUI sheets
/// (<c>widgets.png</c>, <c>icons.png</c>, <c>slider.png</c>, <c>container/inventory.png</c>,
/// <c>container/crafting_table.png</c>, <c>container/furnace.png</c>) from <c>23w13a__or__b</c>,
/// with pixel-exact clean-room Minecraft stone-button, hotbar, icon, and container fallbacks when offline.
/// </summary>
public static class GuiTextureAtlasGenerator
{
    /// <summary>Width of the composite GUI texture atlas in pixels (<c>512</c>).</summary>
    public const int AtlasWidth = 512;

    /// <summary>Height of the composite GUI texture atlas in pixels (<c>768</c>).</summary>
    public const int AtlasHeight = 768;

    /// <summary>X origin of <c>widgets.png</c> inside the composite sheet.</summary>
    public const int WidgetsOriginX = 0;

    /// <summary>Y origin of <c>widgets.png</c> inside the composite sheet.</summary>
    public const int WidgetsOriginY = 0;

    /// <summary>X origin of <c>icons.png</c> inside the composite sheet.</summary>
    public const int IconsOriginX = 256;

    /// <summary>Y origin of <c>icons.png</c> inside the composite sheet.</summary>
    public const int IconsOriginY = 0;

    /// <summary>X origin of <c>slider.png</c> inside the composite sheet.</summary>
    public const int SliderOriginX = 0;

    /// <summary>Y origin of <c>slider.png</c> inside the composite sheet.</summary>
    public const int SliderOriginY = 256;

    /// <summary>X origin of <c>container/inventory.png</c> inside the composite sheet.</summary>
    public const int InventoryOriginX = 256;

    /// <summary>Y origin of <c>container/inventory.png</c> inside the composite sheet.</summary>
    public const int InventoryOriginY = 256;

    /// <summary>X origin of <c>container/crafting_table.png</c> inside the composite sheet.</summary>
    public const int CraftingOriginX = 0;

    /// <summary>Y origin of <c>container/crafting_table.png</c> inside the composite sheet.</summary>
    public const int CraftingOriginY = 512;

    /// <summary>X origin of <c>container/furnace.png</c> inside the composite sheet.</summary>
    public const int FurnaceOriginX = 256;

    /// <summary>Y origin of <c>container/furnace.png</c> inside the composite sheet.</summary>
    public const int FurnaceOriginY = 512;

    /// <summary>Generates the <c>512x768</c> RGBA8 composite GUI texture sheet.</summary>
    /// <param name="externalAssetsPath">Optional external resource pack root path.</param>
    /// <returns>Composite <c>512x768</c> RGBA8 byte buffer.</returns>
    public static byte[] Generate(string? externalAssetsPath = null)
    {
        var rgba = new byte[AtlasWidth * AtlasHeight * 4];
        PaintFallbackWidgets(rgba, WidgetsOriginX, WidgetsOriginY);
        PaintFallbackIcons(rgba, IconsOriginX, IconsOriginY);
        PaintFallbackWidgets(rgba, SliderOriginX, SliderOriginY);
        PaintFallbackInventorySheet(rgba, InventoryOriginX, InventoryOriginY);
        PaintFallbackCraftingSheet(rgba, CraftingOriginX, CraftingOriginY);
        PaintFallbackFurnaceSheet(rgba, FurnaceOriginX, FurnaceOriginY);

        using var pack = ResourcePackLoader.Discover(externalAssetsPath);
        TryOverlaySheet(rgba, pack, "widgets", WidgetsOriginX, WidgetsOriginY);
        TryOverlaySheet(rgba, pack, "icons", IconsOriginX, IconsOriginY);
        TryOverlaySheet(rgba, pack, "slider", SliderOriginX, SliderOriginY);
        TryOverlaySheet(rgba, pack, "container/inventory", InventoryOriginX, InventoryOriginY);
        TryOverlaySheet(rgba, pack, "container/crafting_table", CraftingOriginX, CraftingOriginY);
        TryOverlaySheet(rgba, pack, "container/furnace", FurnaceOriginX, FurnaceOriginY);
        return rgba;
    }

    private static void TryOverlaySheet(byte[] dst, ResourcePackLoader pack, string key, int ox, int oy)
    {
        if (!pack.TryGetGuiPng(key, out var png) ||
            !PngImageDecoder.TryDecodeRgba(png, out var w, out var h, out var sheetRgba))
        {
            return;
        }

        var copyW = Math.Min(256, w);
        var copyH = Math.Min(256, h);
        for (var y = 0; y < copyH; y++)
        {
            for (var x = 0; x < copyW; x++)
            {
                var s = ((y * w) + x) * 4;
                var d = (((oy + y) * AtlasWidth) + (ox + x)) * 4;
                dst[d + 0] = sheetRgba[s + 0];
                dst[d + 1] = sheetRgba[s + 1];
                dst[d + 2] = sheetRgba[s + 2];
                dst[d + 3] = sheetRgba[s + 3];
            }
        }
    }

    private static void PaintFallbackWidgets(byte[] dst, int ox, int oy)
    {
        // Hotbar (0,0, 182x22)
        FillRect(dst, ox + 0, oy + 0, 182, 22, 34, 34, 38, 220);
        DrawBevelBox(dst, ox + 0, oy + 0, 182, 22, 138, 138, 144, 22, 22, 26);
        for (var i = 0; i < 9; i++)
        {
            DrawSlotRecess(dst, ox + 2 + (i * 20), oy + 2, 18, 18);
        }

        // Selected hotbar frame (0,22, 24x24)
        DrawBevelBox(dst, ox + 0, oy + 22, 24, 24, 242, 244, 248, 168, 172, 178);
        DrawBevelBox(dst, ox + 1, oy + 23, 22, 22, 224, 226, 232, 140, 144, 150);

        // Disabled/Slider track button (0,46, 200x20), Normal button (0,66, 200x20), Hovered button (0,86, 200x20)
        PaintStoneButton(dst, ox + 0, oy + 46, 200, 20, 44, 44, 48, 26, 26, 30, 82, 82, 88);
        PaintStoneButton(dst, ox + 0, oy + 66, 200, 20, 114, 114, 118, 172, 172, 178, 58, 58, 62);
        PaintStoneButton(dst, ox + 0, oy + 86, 200, 20, 126, 136, 184, 238, 244, 255, 74, 82, 122);
    }

    private static void PaintFallbackIcons(byte[] dst, int ox, int oy)
    {
        // Crosshair (0,0, 15x15)
        FillRect(dst, ox + 7, oy + 1, 1, 13, 245, 245, 245, 235);
        FillRect(dst, ox + 1, oy + 7, 13, 1, 245, 245, 245, 235);

        // Heart container (16,0), Full (52,0), Half (61,0)
        FillRect(dst, ox + 17, oy + 1, 7, 7, 28, 20, 24, 240);
        FillRect(dst, ox + 53, oy + 1, 7, 7, 228, 34, 42, 255);
        FillRect(dst, ox + 62, oy + 1, 4, 7, 228, 34, 42, 255);

        // Armor empty (16,9), half (25,9), full (34,9)
        FillRect(dst, ox + 17, oy + 10, 7, 7, 42, 44, 50, 235);
        FillRect(dst, ox + 26, oy + 10, 4, 7, 208, 214, 224, 255);
        FillRect(dst, ox + 35, oy + 10, 7, 7, 208, 214, 224, 255);

        // Bubble (16,18)
        FillRect(dst, ox + 17, oy + 19, 7, 7, 48, 162, 248, 245);
        FillRect(dst, ox + 18, oy + 20, 2, 2, 235, 250, 255, 255);

        // Hunger container (16,27), full (52,27), half (61,27)
        FillRect(dst, ox + 17, oy + 28, 7, 7, 36, 28, 22, 235);
        FillRect(dst, ox + 53, oy + 28, 7, 7, 198, 114, 52, 255);
        FillRect(dst, ox + 62, oy + 28, 4, 7, 198, 114, 52, 255);
    }

    private static void PaintFallbackInventorySheet(byte[] dst, int ox, int oy)
    {
        PaintBaseContainerWindow(dst, ox, oy);
        for (var a = 0; a < 4; a++)
        {
            DrawSlotRecess(dst, ox + 7, oy + 7 + (a * 18), 18, 18);
        }

        FillRect(dst, ox + 26, oy + 8, 49, 70, 14, 14, 16, 255); // Player preview box
        DrawSlotRecess(dst, ox + 76, oy + 61, 18, 18); // Offhand

        // 2x2 Crafting Grid + Arrow + Output
        for (var r = 0; r < 2; r++)
        {
            for (var c = 0; c < 2; c++)
            {
                DrawSlotRecess(dst, ox + 97 + (c * 18), oy + 17 + (r * 18), 18, 18);
            }
        }

        FillRect(dst, ox + 136, oy + 33, 14, 4, 128, 128, 132, 255);
        DrawSlotRecess(dst, ox + 153, oy + 27, 18, 18);
    }

    private static void PaintFallbackCraftingSheet(byte[] dst, int ox, int oy)
    {
        PaintBaseContainerWindow(dst, ox, oy);
        for (var r = 0; r < 3; r++)
        {
            for (var c = 0; c < 3; c++)
            {
                DrawSlotRecess(dst, ox + 29 + (c * 18), oy + 16 + (r * 18), 18, 18);
            }
        }

        FillRect(dst, ox + 90, oy + 39, 22, 5, 126, 126, 130, 255);
        DrawSlotRecess(dst, ox + 120, oy + 31, 24, 24);
    }

    private static void PaintFallbackFurnaceSheet(byte[] dst, int ox, int oy)
    {
        PaintBaseContainerWindow(dst, ox, oy);
        DrawSlotRecess(dst, ox + 55, oy + 16, 18, 18); // Input
        DrawSlotRecess(dst, ox + 55, oy + 52, 18, 18); // Fuel
        DrawSlotRecess(dst, ox + 112, oy + 31, 24, 24); // Output
        FillRect(dst, ox + 57, oy + 37, 14, 13, 118, 118, 122, 255);
        FillRect(dst, ox + 80, oy + 39, 22, 5, 118, 118, 122, 255);

        // Lit Flame sprite at (176, 0, 14x14) & Lit Arrow sprite at (176, 14, 24x17)
        FillRect(dst, ox + 178, oy + 2, 10, 12, 255, 154, 28, 255);
        FillRect(dst, ox + 180, oy + 6, 6, 8, 255, 236, 96, 255);
        FillRect(dst, ox + 176, oy + 20, 18, 5, 250, 250, 252, 255);
        FillRect(dst, ox + 192, oy + 16, 8, 13, 250, 250, 252, 255);
    }

    private static void PaintBaseContainerWindow(byte[] dst, int ox, int oy)
    {
        FillRect(dst, ox, oy, 176, 166, 198, 198, 198, 255);
        DrawBevelBox(dst, ox, oy, 176, 166, 255, 255, 255, 85, 85, 85);
        DrawBevelBox(dst, ox + 1, oy + 1, 174, 164, 255, 255, 255, 85, 85, 85);

        for (var r = 0; r < 3; r++)
        {
            for (var c = 0; c < 9; c++)
            {
                DrawSlotRecess(dst, ox + 7 + (c * 18), oy + 83 + (r * 18), 18, 18);
            }
        }

        for (var c = 0; c < 9; c++)
        {
            DrawSlotRecess(dst, ox + 7 + (c * 18), oy + 141, 18, 18);
        }
    }

    private static void PaintStoneButton(byte[] dst, int x, int y, int w, int h, byte r, byte g, byte b, byte hiR, byte hiG, byte hiB, byte shR, byte shG, byte shB)
    {
        FillRect(dst, x, y, w, h, 12, 12, 14, 255);
        FillRect(dst, x + 1, y + 1, w - 2, h - 2, r, g, b, 255);
        FillRect(dst, x + 1, y + 1, w - 3, 2, hiR, hiG, hiB, 255);
        FillRect(dst, x + 1, y + 1, 2, h - 3, hiR, hiG, hiB, 255);
        FillRect(dst, x + 2, y + h - 3, w - 3, 2, shR, shG, shB, 255);
        FillRect(dst, x + w - 3, y + 2, 2, h - 3, shR, shG, shB, 255);
    }

    private static void DrawSlotRecess(byte[] dst, int x, int y, int w, int h)
    {
        FillRect(dst, x, y, w, h, 139, 139, 139, 255);
        FillRect(dst, x, y, w - 1, 1, 55, 55, 55, 255);
        FillRect(dst, x, y, 1, h - 1, 55, 55, 55, 255);
        FillRect(dst, x + 1, y + h - 1, w - 1, 1, 255, 255, 255, 255);
        FillRect(dst, x + w - 1, y + 1, 1, h - 1, 255, 255, 255, 255);
    }

    private static void DrawBevelBox(byte[] dst, int x, int y, int w, int h, byte hiR, byte hiG, byte hiB, byte shR, byte shG, byte shB)
    {
        FillRect(dst, x, y, w, 1, hiR, hiG, hiB, 255);
        FillRect(dst, x, y, 1, h, hiR, hiG, hiB, 255);
        FillRect(dst, x, y + h - 1, w, 1, shR, shG, shB, 255);
        FillRect(dst, x + w - 1, y, 1, h, shR, shG, shB, 255);
    }

    private static void FillRect(byte[] dst, int x, int y, int w, int h, byte r, byte g, byte b, byte a)
    {
        for (var py = y; py < y + h; py++)
        {
            if ((uint)py >= AtlasHeight)
            {
                continue;
            }

            for (var px = x; px < x + w; px++)
            {
                if ((uint)px >= AtlasWidth)
                {
                    continue;
                }

                var i = ((py * AtlasWidth) + px) * 4;
                dst[i + 0] = r;
                dst[i + 1] = g;
                dst[i + 2] = b;
                dst[i + 3] = a;
            }
        }
    }
}
