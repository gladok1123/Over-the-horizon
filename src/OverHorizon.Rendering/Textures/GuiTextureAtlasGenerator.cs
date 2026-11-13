namespace OverHorizon.Rendering.Textures;

/// <summary>
/// Builds a composite <c>1024x1024</c> RGBA8 GUI texture sheet packing real <c>23w13a__or__b</c> GUI textures:
/// <c>widgets</c>, <c>icons</c>, <c>slider</c>, <c>container/inventory</c>, <c>container/crafting_table</c>,
/// <c>container/furnace</c>, <c>container/creative_inventory/{tab_items,tab_item_search,tab_inventory,tabs}</c>,
/// <c>title/minecraft</c>, <c>title/edition</c>, <c>options_background</c>, and <c>title/background/panorama_0</c>,
/// with clean-room procedural fallbacks when offline.
/// </summary>
public static class GuiTextureAtlasGenerator
{
    /// <summary>Width of the composite GUI texture atlas in pixels (<c>1024</c>).</summary>
    public const int AtlasWidth = 1024;

    /// <summary>Height of the composite GUI texture atlas in pixels (<c>1024</c>).</summary>
    public const int AtlasHeight = 1024;

    internal const int WidgetsOriginX = 0;
    internal const int WidgetsOriginY = 0;

    internal const int IconsOriginX = 256;
    internal const int IconsOriginY = 0;

    internal const int SliderOriginX = 0;
    internal const int SliderOriginY = 256;

    internal const int InventoryOriginX = 256;
    internal const int InventoryOriginY = 256;

    internal const int CraftingOriginX = 0;
    internal const int CraftingOriginY = 512;

    internal const int FurnaceOriginX = 256;
    internal const int FurnaceOriginY = 512;

    internal const int CreativeItemsOriginX = 512;
    internal const int CreativeItemsOriginY = 0;

    internal const int CreativeSearchOriginX = 768;
    internal const int CreativeSearchOriginY = 0;

    internal const int CreativeInvOriginX = 512;
    internal const int CreativeInvOriginY = 256;

    internal const int CreativeTabsOriginX = 768;
    internal const int CreativeTabsOriginY = 256;

    internal const int TitleLogoOriginX = 512;
    internal const int TitleLogoOriginY = 512;

    internal const int TitleEditionOriginX = 512;
    internal const int TitleEditionOriginY = 768;

    internal const int OptionsBgOriginX = 704;
    internal const int OptionsBgOriginY = 768;

    internal const int TitlePanoramaOriginX = 768;
    internal const int TitlePanoramaOriginY = 512;

    /// <summary>Generates the <c>1024x1024</c> RGBA8 composite GUI texture sheet.</summary>
    public static byte[] Generate(string? externalAssetsPath = null)
    {
        var rgba = new byte[AtlasWidth * AtlasHeight * 4];
        PaintFallbackWidgets(rgba, WidgetsOriginX, WidgetsOriginY);
        PaintFallbackIcons(rgba, IconsOriginX, IconsOriginY);
        PaintFallbackWidgets(rgba, SliderOriginX, SliderOriginY);
        PaintFallbackInventorySheet(rgba, InventoryOriginX, InventoryOriginY);
        PaintFallbackCraftingSheet(rgba, CraftingOriginX, CraftingOriginY);
        PaintFallbackFurnaceSheet(rgba, FurnaceOriginX, FurnaceOriginY);
        PaintFallbackCreativeTabSheet(rgba, CreativeItemsOriginX, CreativeItemsOriginY, withSearchBox: false);
        PaintFallbackCreativeTabSheet(rgba, CreativeSearchOriginX, CreativeSearchOriginY, withSearchBox: true);
        PaintFallbackCreativeSurvivalSheet(rgba, CreativeInvOriginX, CreativeInvOriginY);
        PaintFallbackCreativeTabs(rgba, CreativeTabsOriginX, CreativeTabsOriginY);
        PaintFallbackTitleLogo(rgba, TitleLogoOriginX, TitleLogoOriginY);
        PaintFallbackEditionSubtitle(rgba, TitleEditionOriginX, TitleEditionOriginY);
        PaintFallbackDirtBg(rgba, OptionsBgOriginX, OptionsBgOriginY);
        PaintFallbackPanorama(rgba, TitlePanoramaOriginX, TitlePanoramaOriginY);

        using var pack = ResourcePackLoader.Discover(externalAssetsPath);
        TryOverlaySheet(rgba, pack, "widgets", WidgetsOriginX, WidgetsOriginY, 256, 256);
        TryOverlaySheet(rgba, pack, "icons", IconsOriginX, IconsOriginY, 256, 256);
        TryOverlaySheet(rgba, pack, "slider", SliderOriginX, SliderOriginY, 256, 256);
        TryOverlaySheet(rgba, pack, "container/inventory", InventoryOriginX, InventoryOriginY, 256, 256);
        TryOverlaySheet(rgba, pack, "container/crafting_table", CraftingOriginX, CraftingOriginY, 256, 256);
        TryOverlaySheet(rgba, pack, "container/furnace", FurnaceOriginX, FurnaceOriginY, 256, 256);
        TryOverlaySheet(rgba, pack, "container/creative_inventory/tab_items", CreativeItemsOriginX, CreativeItemsOriginY, 256, 256);
        TryOverlaySheet(rgba, pack, "container/creative_inventory/tab_item_search", CreativeSearchOriginX, CreativeSearchOriginY, 256, 256);
        TryOverlaySheet(rgba, pack, "container/creative_inventory/tab_inventory", CreativeInvOriginX, CreativeInvOriginY, 256, 256);
        TryOverlaySheet(rgba, pack, "container/creative_inventory/tabs", CreativeTabsOriginX, CreativeTabsOriginY, 256, 256);
        TryOverlaySheet(rgba, pack, "title/minecraft", TitleLogoOriginX, TitleLogoOriginY, 256, 256);
        TryOverlaySheet(rgba, pack, "title/edition", TitleEditionOriginX, TitleEditionOriginY, 128, 32);
        TryOverlaySheet(rgba, pack, "options_background", OptionsBgOriginX, OptionsBgOriginY, 32, 32);
        TryOverlaySheet(rgba, pack, "title/background/panorama_0", TitlePanoramaOriginX, TitlePanoramaOriginY, 256, 256);
        return rgba;
    }

    private static void TryOverlaySheet(byte[] dst, ResourcePackLoader pack, string key, int ox, int oy, int maxW, int maxH)
    {
        if (!pack.TryGetGuiPng(key, out var png) ||
            !PngImageDecoder.TryDecodeRgba(png, out var w, out var h, out var sheetRgba))
        {
            return;
        }

        // Clear target region before overlaying transparent real PNGs (e.g. title/minecraft.png)
        FillRect(dst, ox, oy, Math.Min(maxW, w), Math.Min(maxH, h), 0, 0, 0, 0);

        var copyW = Math.Min(maxW, w);
        var copyH = Math.Min(maxH, h);
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
        FillRect(dst, ox, oy, 182, 22, 34, 34, 38, 220);
        DrawBevelBox(dst, ox, oy, 182, 22, 138, 138, 144, 22, 22, 26);
        for (var i = 0; i < 9; i++)
        {
            DrawSlotRecess(dst, ox + 2 + (i * 20), oy + 2, 18, 18);
        }

        DrawBevelBox(dst, ox, oy + 22, 24, 24, 242, 244, 248, 168, 172, 178);
        DrawBevelBox(dst, ox + 1, oy + 23, 22, 22, 224, 226, 232, 140, 144, 150);

        // Offhand HUD slot frame at (24, 23, 22x22)
        FillRect(dst, ox + 24, oy + 23, 22, 22, 34, 34, 38, 220);
        DrawBevelBox(dst, ox + 24, oy + 23, 22, 22, 148, 148, 156, 22, 22, 26);
        DrawSlotRecess(dst, ox + 26, oy + 25, 18, 18);

        PaintStoneButton(dst, ox, oy + 46, 200, 20, 44, 44, 48, 26, 26, 30, 82, 82, 88);
        PaintStoneButton(dst, ox, oy + 66, 200, 20, 114, 114, 118, 172, 172, 178, 58, 58, 62);
        PaintStoneButton(dst, ox, oy + 86, 200, 20, 126, 136, 184, 238, 244, 255, 74, 82, 122);
    }

    private static void PaintFallbackIcons(byte[] dst, int ox, int oy)
    {
        FillRect(dst, ox + 7, oy + 1, 1, 13, 245, 245, 245, 235);
        FillRect(dst, ox + 1, oy + 7, 13, 1, 245, 245, 245, 235);
        FillRect(dst, ox + 17, oy + 1, 7, 7, 28, 20, 24, 240);
        FillRect(dst, ox + 53, oy + 1, 7, 7, 228, 34, 42, 255);
        FillRect(dst, ox + 62, oy + 1, 4, 7, 228, 34, 42, 255);
        FillRect(dst, ox + 17, oy + 10, 7, 7, 42, 44, 50, 235);
        FillRect(dst, ox + 26, oy + 10, 4, 7, 208, 214, 224, 255);
        FillRect(dst, ox + 35, oy + 10, 7, 7, 208, 214, 224, 255);
        FillRect(dst, ox + 17, oy + 19, 7, 7, 48, 162, 248, 245);
        FillRect(dst, ox + 18, oy + 20, 2, 2, 235, 250, 255, 255);
        FillRect(dst, ox + 17, oy + 28, 7, 7, 36, 28, 22, 235);
        FillRect(dst, ox + 53, oy + 28, 7, 7, 198, 114, 52, 255);
        FillRect(dst, ox + 62, oy + 28, 4, 7, 198, 114, 52, 255);
    }

    private static void PaintFallbackInventorySheet(byte[] dst, int ox, int oy)
    {
        PaintBaseContainerWindow(dst, ox, oy);
        for (var a = 0; a < 4; a++) DrawSlotRecess(dst, ox + 7, oy + 7 + (a * 18), 18, 18);
        FillRect(dst, ox + 26, oy + 8, 49, 70, 10, 10, 12, 255);
        DrawSlotRecess(dst, ox + 76, oy + 61, 18, 18);
        for (var r = 0; r < 2; r++)
        {
            for (var c = 0; c < 2; c++) DrawSlotRecess(dst, ox + 97 + (c * 18), oy + 17 + (r * 18), 18, 18);
        }

        FillRect(dst, ox + 136, oy + 33, 14, 4, 128, 128, 132, 255);
        DrawSlotRecess(dst, ox + 153, oy + 27, 18, 18);
    }

    private static void PaintFallbackCraftingSheet(byte[] dst, int ox, int oy)
    {
        PaintBaseContainerWindow(dst, ox, oy);
        for (var r = 0; r < 3; r++)
        {
            for (var c = 0; c < 3; c++) DrawSlotRecess(dst, ox + 29 + (c * 18), oy + 16 + (r * 18), 18, 18);
        }

        FillRect(dst, ox + 90, oy + 39, 22, 5, 126, 126, 130, 255);
        DrawSlotRecess(dst, ox + 120, oy + 31, 24, 24);
    }

    private static void PaintFallbackFurnaceSheet(byte[] dst, int ox, int oy)
    {
        PaintBaseContainerWindow(dst, ox, oy);
        DrawSlotRecess(dst, ox + 55, oy + 16, 18, 18);
        DrawSlotRecess(dst, ox + 55, oy + 52, 18, 18);
        DrawSlotRecess(dst, ox + 112, oy + 31, 24, 24);
        FillRect(dst, ox + 57, oy + 37, 14, 13, 118, 118, 122, 255);
        FillRect(dst, ox + 80, oy + 39, 22, 5, 118, 118, 122, 255);
        FillRect(dst, ox + 178, oy + 2, 10, 12, 255, 154, 28, 255);
        FillRect(dst, ox + 180, oy + 6, 6, 8, 255, 236, 96, 255);
        FillRect(dst, ox + 176, oy + 20, 18, 5, 250, 250, 252, 255);
        FillRect(dst, ox + 192, oy + 16, 8, 13, 250, 250, 252, 255);
    }

    private static void PaintFallbackCreativeTabSheet(byte[] dst, int ox, int oy, bool withSearchBox)
    {
        // Authentic 195x136 Creative Inventory panel
        FillRect(dst, ox, oy, 195, 136, 198, 198, 198, 255);
        DrawBevelBox(dst, ox, oy, 195, 136, 255, 255, 255, 85, 85, 85);
        DrawBevelBox(dst, ox + 1, oy + 1, 193, 134, 255, 255, 255, 85, 85, 85);
        if (withSearchBox)
        {
            FillRect(dst, ox + 81, oy + 5, 90, 11, 28, 28, 30, 255);
            DrawBevelBox(dst, ox + 81, oy + 5, 90, 11, 55, 55, 55, 220, 220, 220);
        }

        for (var r = 0; r < 5; r++)
        {
            for (var c = 0; c < 9; c++) DrawSlotRecess(dst, ox + 8 + (c * 18), oy + 17 + (r * 18), 18, 18);
        }

        for (var c = 0; c < 9; c++) DrawSlotRecess(dst, ox + 8 + (c * 18), oy + 111, 18, 18);

        // Scrollbar track at (174, 17, 14x112)
        FillRect(dst, ox + 174, oy + 17, 14, 112, 112, 112, 116, 255);
        DrawBevelBox(dst, ox + 174, oy + 17, 14, 112, 55, 55, 55, 240, 240, 240);
    }

    private static void PaintFallbackCreativeSurvivalSheet(byte[] dst, int ox, int oy)
    {
        FillRect(dst, ox, oy, 195, 136, 198, 198, 198, 255);
        DrawBevelBox(dst, ox, oy, 195, 136, 255, 255, 255, 85, 85, 85);
        DrawSlotRecess(dst, ox + 53, oy + 5, 18, 18);
        DrawSlotRecess(dst, ox + 53, oy + 32, 18, 18);
        DrawSlotRecess(dst, ox + 107, oy + 5, 18, 18);
        DrawSlotRecess(dst, ox + 107, oy + 32, 18, 18);
        FillRect(dst, ox + 72, oy + 5, 34, 45, 10, 10, 12, 255);
        DrawSlotRecess(dst, ox + 34, oy + 19, 18, 18); // Offhand
        DrawSlotRecess(dst, ox + 172, oy + 111, 18, 18); // Delete Item (X)
        FillRect(dst, ox + 177, oy + 118, 8, 2, 200, 48, 48, 255);
        FillRect(dst, ox + 180, oy + 115, 2, 8, 200, 48, 48, 255);
        for (var r = 0; r < 3; r++)
        {
            for (var c = 0; c < 9; c++) DrawSlotRecess(dst, ox + 8 + (c * 18), oy + 53 + (r * 18), 18, 18);
        }

        for (var c = 0; c < 9; c++) DrawSlotRecess(dst, ox + 8 + (c * 18), oy + 111, 18, 18);
    }

    private static void PaintFallbackCreativeTabs(byte[] dst, int ox, int oy)
    {
        // Unselected tabs at y=0 (26x32) & selected tabs at y=32 (26x32); scrollbar thumb at (232, 0, 12x15)
        for (var i = 0; i < 7; i++)
        {
            FillRect(dst, ox + (i * 26), oy, 26, 28, 148, 148, 152, 255);
            DrawBevelBox(dst, ox + (i * 26), oy, 26, 28, 224, 224, 228, 72, 72, 76);
            FillRect(dst, ox + (i * 26), oy + 32, 26, 32, 198, 198, 198, 255);
            DrawBevelBox(dst, ox + (i * 26), oy + 32, 26, 31, 255, 255, 255, 85, 85, 85);
            FillRect(dst, ox + (i * 26) + 2, oy + 62, 22, 2, 198, 198, 198, 255);
        }

        FillRect(dst, ox + 232, oy, 12, 15, 198, 198, 202, 255);
        DrawBevelBox(dst, ox + 232, oy, 12, 15, 255, 255, 255, 85, 85, 85);
    }

    private static void PaintFallbackTitleLogo(byte[] dst, int ox, int oy)
    {
        // Bold stone-beveled 3D block banner fallback when offline (overwritten by real title/minecraft.png online)
        FillRect(dst, ox + 4, oy + 8, 248, 36, 48, 48, 54, 255);
        FillRect(dst, ox + 2, oy + 4, 248, 36, 168, 172, 178, 255);
        DrawBevelBox(dst, ox + 2, oy + 4, 248, 36, 232, 236, 242, 84, 88, 94);
    }

    private static void PaintFallbackEditionSubtitle(byte[] dst, int ox, int oy)
    {
        FillRect(dst, ox + 8, oy + 2, 112, 12, 124, 128, 134, 235);
    }

    private static void PaintFallbackDirtBg(byte[] dst, int ox, int oy)
    {
        for (var y = 0; y < 32; y++)
        {
            for (var x = 0; x < 32; x++)
            {
                var n = ((x * 37) ^ (y * 71)) & 15;
                FillRect(dst, ox + x, oy + y, 1, 1, (byte)(52 + n), (byte)(36 + (n * 2 / 3)), (byte)(24 + (n / 2)), 255);
            }
        }
    }

    private static void PaintFallbackPanorama(byte[] dst, int ox, int oy)
    {
        for (var y = 0; y < 256; y++)
        {
            byte r = y < 140 ? (byte)(88 + (y / 3)) : (byte)(56 + ((y - 140) / 4));
            byte g = y < 140 ? (byte)(156 + (y / 3)) : (byte)(126 - ((y - 140) / 3));
            byte b = y < 140 ? (byte)238 : (byte)54;
            FillRect(dst, ox, oy + y, 256, 1, r, g, b, 255);
        }
    }

    private static void PaintBaseContainerWindow(byte[] dst, int ox, int oy)
    {
        FillRect(dst, ox, oy, 176, 166, 198, 198, 198, 255);
        DrawBevelBox(dst, ox, oy, 176, 166, 255, 255, 255, 85, 85, 85);
        DrawBevelBox(dst, ox + 1, oy + 1, 174, 164, 255, 255, 255, 85, 85, 85);
        for (var r = 0; r < 3; r++)
        {
            for (var c = 0; c < 9; c++) DrawSlotRecess(dst, ox + 7 + (c * 18), oy + 83 + (r * 18), 18, 18);
        }

        for (var c = 0; c < 9; c++) DrawSlotRecess(dst, ox + 7 + (c * 18), oy + 141, 18, 18);
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
            if ((uint)py >= AtlasHeight) continue;
            for (var px = x; px < x + w; px++)
            {
                if ((uint)px >= AtlasWidth) continue;
                var i = ((py * AtlasWidth) + px) * 4;
                dst[i + 0] = r;
                dst[i + 1] = g;
                dst[i + 2] = b;
                dst[i + 3] = a;
            }
        }
    }
}
