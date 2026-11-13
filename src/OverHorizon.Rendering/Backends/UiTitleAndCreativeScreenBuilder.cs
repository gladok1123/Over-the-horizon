using System.Globalization;
using System.Numerics;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Textures;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Builds the authentic Minecraft-styled Title Screen emblem &amp; subtitle, dirt-backed World Select / Create screens,
/// and the real <c>195x136</c> <c>creative_inventory/*.png</c> Creative Inventory with category tabs, scrollbar, search box,
/// and Delete Item trash slot.
/// </summary>
internal static class UiTitleAndCreativeScreenBuilder
{
    public const float CreativeGuiScale = 3.0f;

    private static readonly int[] CreativeTabIconTiles =
    [
        ProceduralAtlasGenerator.TileClayBricks,
        ProceduralAtlasGenerator.TileGrassSide,
        ProceduralAtlasGenerator.TileCraftingSide,
        ProceduralAtlasGenerator.TileRedstoneBlock,
        142, // Diamond Sword item tile
        211, // Book / Search item tile
        ProceduralAtlasGenerator.TileChestSide,
    ];

    public static void BuildTitleScreen(List<TexturedUiVertex> v, float width, float height, FrameOverlayState state)
    {
        // Soft gradient glaze over the live rotating 3D voxel panorama landscape
        UiGeometryBuilder.DrawRect(v, 0, 0, width, height, new Vector4(0.04f, 0.06f, 0.11f, 0.24f));

        var cx = width * 0.5f;
        var logoY = MathF.Max(28.0f, height * 0.07f);

        // Authentic split-halves title/minecraft.png emblem backdrop + bold stone-chiseled "OVER THE HORIZON" + title/edition.png banner!
        const float logoScale = 2.0f;
        var emblemW = 274.0f * logoScale;
        var emblemX = MathF.Round(cx - (emblemW * 0.5f));
        UiGuiSpriteHelper.DrawGuiSubRect(v, GuiTextureAtlasGenerator.TitleLogoOriginX, GuiTextureAtlasGenerator.TitleLogoOriginY, 0, 0, 155, 44, emblemX, logoY, 155.0f * logoScale, 44.0f * logoScale, new Vector4(1.0f, 1.0f, 1.0f, 0.22f));
        UiGuiSpriteHelper.DrawGuiSubRect(v, GuiTextureAtlasGenerator.TitleLogoOriginX, GuiTextureAtlasGenerator.TitleLogoOriginY, 0, 45, 119, 44, emblemX + (155.0f * logoScale), logoY, 119.0f * logoScale, 44.0f * logoScale, new Vector4(1.0f, 1.0f, 1.0f, 0.22f));

        var title = string.IsNullOrEmpty(state.MenuTitle) ? "OVER THE HORIZON" : state.MenuTitle;
        // 3D Stone-extruded Minecraft Title Banner
        for (var depth = 6; depth >= 1; depth--)
        {
            UiGeometryBuilder.DrawCenteredText(v, title, cx + depth, logoY + 14.0f + (depth * 1.4f), 4.6f, new Vector4(0.14f, 0.15f, 0.18f, 0.96f), shadow: false);
        }

        UiGeometryBuilder.DrawCenteredText(v, title, cx, logoY + 13.0f, 4.6f, new Vector4(0.86f, 0.88f, 0.92f, 1.0f), shadow: false);
        UiGeometryBuilder.DrawCenteredText(v, title, cx, logoY + 11.0f, 4.6f, new Vector4(0.97f, 0.98f, 1.0f, 1.0f), shadow: false);

        // Subtitle plate styled with title/edition.png
        var subY = logoY + 64.0f;
        UiGuiSpriteHelper.DrawGuiSubRect(v, GuiTextureAtlasGenerator.TitleEditionOriginX, GuiTextureAtlasGenerator.TitleEditionOriginY, 0, 0, 98, 14, cx - 108.0f, subY - 2.0f, 216.0f, 28.0f, new Vector4(0.85f, 0.88f, 0.94f, 0.30f));
        var sub = string.IsNullOrEmpty(state.MenuSubtitle) ? "C# EDITION" : state.MenuSubtitle;
        UiGeometryBuilder.DrawCenteredText(v, sub, cx, subY + 2.0f, 2.35f, new Vector4(0.76f, 0.82f, 0.90f, 1.0f), shadow: true);

        if (!string.IsNullOrEmpty(state.SplashText))
        {
            UiGeometryBuilder.DrawCenteredText(v, state.SplashText, cx + 178.0f, subY + 24.0f, 1.55f, new Vector4(1.0f, 0.93f, 0.22f, 1.0f), shadow: true);
        }

        UiGeometryBuilder.DrawButtons(v, state.Buttons);

        if (!string.IsNullOrEmpty(state.StatusMessage))
        {
            UiGeometryBuilder.DrawCenteredText(v, state.StatusMessage, cx, height - 62.0f, 1.6f, new Vector4(1.0f, 0.76f, 0.42f, 1.0f), shadow: true);
        }

        UiGeometryBuilder.DrawText(v, "Over-the-Horizon v0.8.0-alpha (C# / .NET 9)", 14.0f, height - 24.0f, 1.5f, new Vector4(0.92f, 0.94f, 0.98f, 0.94f), shadow: true);
        const string rightNote = "Clean-Room Engine - 23w13a Assets";
        UiGeometryBuilder.DrawText(v, rightNote, width - UiGeometryBuilder.MeasureText(rightNote, 1.5f) - 14.0f, height - 24.0f, 1.5f, new Vector4(0.84f, 0.86f, 0.90f, 0.88f), shadow: true);
    }

    public static void BuildDirtMenuBackdrop(List<TexturedUiVertex> v, float width, float height)
    {
        const float tileDraw = 64.0f;
        var tint = new Vector4(0.42f, 0.42f, 0.44f, 1.0f);
        for (var y = 0.0f; y < height; y += tileDraw)
        {
            for (var x = 0.0f; x < width; x += tileDraw)
            {
                var w = MathF.Min(tileDraw, width - x);
                var h = MathF.Min(tileDraw, height - y);
                UiGuiSpriteHelper.DrawGuiSubRect(
                    v,
                    GuiTextureAtlasGenerator.OptionsBgOriginX,
                    GuiTextureAtlasGenerator.OptionsBgOriginY,
                    0, 0, 32.0f * (w / tileDraw), 32.0f * (h / tileDraw),
                    x, y, w, h,
                    tint);
            }
        }

        UiGeometryBuilder.DrawRect(v, 0, 0, width, 82.0f, new Vector4(0.04f, 0.04f, 0.05f, 0.56f));
        UiGeometryBuilder.DrawRect(v, 0, height - 92.0f, width, 92.0f, new Vector4(0.04f, 0.04f, 0.05f, 0.56f));
    }

    public static (float WinX, float WinY, float WinW, float WinH) GetCreativeWindowBounds(float width, float height)
    {
        const float winW = 195.0f * CreativeGuiScale;
        const float winH = 136.0f * CreativeGuiScale;
        var winX = MathF.Round((width - winW) * 0.5f);
        var winY = MathF.Max(48.0f, MathF.Round((height - winH) * 0.50f));
        return (winX, winY, winW, winH);
    }

    public static void BuildCreativeInventoryScreen(List<TexturedUiVertex> v, float width, float height, FrameOverlayState state)
    {
        var (winX, winY, winW, winH) = GetCreativeWindowBounds(width, height);
        const float s = CreativeGuiScale;
        var selTab = Math.Clamp(state.CreativeSelectedTab, 0, 6);

        // 1. Unselected Top Tabs (drawn behind main 195x136 panel!)
        for (var t = 0; t < 7; t++)
        {
            if (t == selTab) continue;
            var tx = GetTabScreenX(winX, t, s);
            var ty = winY - (28.0f * s);
            var tabSrcCol = Math.Min(t, 5);
            UiGuiSpriteHelper.DrawGuiSubRect(v, GuiTextureAtlasGenerator.CreativeTabsOriginX, GuiTextureAtlasGenerator.CreativeTabsOriginY, tabSrcCol * 26.0f, 0.0f, 26.0f, 30.0f, tx, ty, 26.0f * s, 30.0f * s, new Vector4(0.88f, 0.88f, 0.90f, 1.0f));
            UiSurvivalHudBuilder.DrawAtlasSprite(v, CreativeTabIconTiles[t], tx + (5.0f * s), ty + (7.0f * s), 16.0f * s, Vector4.One);
        }

        // 2. Main 195x136 Creative Inventory Sheet (tab_items, tab_item_search, or tab_inventory)
        int sheetOx;
        int sheetOy;
        if (selTab == 6)
        {
            sheetOx = GuiTextureAtlasGenerator.CreativeInvOriginX;
            sheetOy = GuiTextureAtlasGenerator.CreativeInvOriginY;
        }
        else if (selTab == 5 || !string.IsNullOrEmpty(state.CreativeSearchQuery))
        {
            sheetOx = GuiTextureAtlasGenerator.CreativeSearchOriginX;
            sheetOy = GuiTextureAtlasGenerator.CreativeSearchOriginY;
        }
        else
        {
            sheetOx = GuiTextureAtlasGenerator.CreativeItemsOriginX;
            sheetOy = GuiTextureAtlasGenerator.CreativeItemsOriginY;
        }

        UiGuiSpriteHelper.DrawGuiSubRect(v, sheetOx, sheetOy, 0, 0, 195, 136, winX, winY, winW, winH, Vector4.One);

        // 3. Selected Top Tab (drawn seamlessly in front of top border!)
        var selTabX = GetTabScreenX(winX, selTab, s);
        var selTabY = winY - (28.0f * s);
        var selTabSrcCol = Math.Min(selTab, 5);
        UiGuiSpriteHelper.DrawGuiSubRect(v, GuiTextureAtlasGenerator.CreativeTabsOriginX, GuiTextureAtlasGenerator.CreativeTabsOriginY, selTabSrcCol * 26.0f, 32.0f, 26.0f, 32.0f, selTabX, selTabY, 26.0f * s, 32.0f * s, Vector4.One);
        UiSurvivalHudBuilder.DrawAtlasSprite(v, CreativeTabIconTiles[selTab], selTabX + (5.0f * s), selTabY + (8.0f * s), 16.0f * s, Vector4.One);

        // 4. Category Title & Live Search Query + Scrollbar Thumb
        var labelCol = new Vector4(0.24f, 0.24f, 0.26f, 1.0f);
        UiGeometryBuilder.DrawText(v, state.MenuTitle, winX + (8.0f * s), winY + (6.0f * s), 1.45f, labelCol, shadow: false);

        if (selTab == 6)
        {
            // Crisp red [X] badge inside the Delete Item trash slot (173, 112) on tab_inventory.png!
            UiGeometryBuilder.DrawCenteredText(v, "X", winX + (181.0f * s), winY + (116.0f * s), 1.6f, new Vector4(0.82f, 0.20f, 0.20f, 0.92f), shadow: false);
        }
        else
        {
            if (selTab == 5 || !string.IsNullOrEmpty(state.CreativeSearchQuery))
            {
                var qText = string.IsNullOrEmpty(state.CreativeSearchQuery) ? "Type to search..." : state.CreativeSearchQuery + "_";
                var qCol = string.IsNullOrEmpty(state.CreativeSearchQuery) ? new Vector4(0.62f, 0.64f, 0.68f, 0.95f) : new Vector4(1.0f, 0.98f, 0.72f, 1.0f);
                UiGeometryBuilder.DrawText(v, qText, winX + (83.0f * s), winY + (7.0f * s), 1.30f, qCol, shadow: true);
            }

            var scrollNorm = Math.Clamp(state.CreativeScrollNormalized, 0.0f, 1.0f);
            var thumbY = winY + ((18.0f + (scrollNorm * 95.0f)) * s);
            UiGuiSpriteHelper.DrawGuiSubRect(v, GuiTextureAtlasGenerator.CreativeTabsOriginX, GuiTextureAtlasGenerator.CreativeTabsOriginY, 232.0f, 0.0f, 12.0f, 15.0f, winX + (175.0f * s), thumbY, 12.0f * s, 15.0f * s, Vector4.One);
        }

        // 5. Render InventoryCells (9x5 grid + Hotbar OR Survival Inventory + Delete Item Trash Slot) + Cursor Stack + Tooltip
        string? hoveredTitle = null;
        string? hoveredDetail = null;
        if (state.InventoryCells is { Count: > 0 } cells)
        {
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell.AtlasTileIndex >= 0 && cell.StackCount > 0)
                {
                    UiSurvivalHudBuilder.DrawAtlasSprite(v, cell.AtlasTileIndex, cell.X + 2.0f, cell.Y + 2.0f, cell.Size - 4.0f, Vector4.One);
                }

                if (cell.StackCount > 1)
                {
                    var cnt = cell.StackCount.ToString(CultureInfo.InvariantCulture);
                    UiGeometryBuilder.DrawText(v, cnt, cell.X + cell.Size - UiGeometryBuilder.MeasureText(cnt, 1.40f) - 1.0f, cell.Y + cell.Size - 13.0f, 1.40f, Vector4.One, shadow: true);
                }

                if (cell.IsSelected)
                {
                    UiGeometryBuilder.DrawRect(v, cell.X - 2.0f, cell.Y - 2.0f, cell.Size + 4.0f, 2.0f, new Vector4(0.98f, 0.92f, 0.32f, 0.95f));
                }

                if (cell.IsHovered)
                {
                    UiGeometryBuilder.DrawRect(v, cell.X, cell.Y, cell.Size, cell.Size, new Vector4(1.0f, 1.0f, 1.0f, 0.36f));
                    if (!string.IsNullOrEmpty(cell.DisplayName))
                    {
                        hoveredTitle = cell.DisplayName;
                        hoveredDetail = cell.TooltipDetail;
                    }
                }
            }
        }

        if (state.CursorItemTile >= 0 && state.CursorItemCount > 0)
        {
            const float curSize = 44.0f;
            var mx = state.CursorScreenPos.X - (curSize * 0.5f);
            var my = state.CursorScreenPos.Y - (curSize * 0.5f);
            UiSurvivalHudBuilder.DrawAtlasSprite(v, state.CursorItemTile, mx, my, curSize, Vector4.One);
            if (state.CursorItemCount > 1)
            {
                var cnt = state.CursorItemCount.ToString(CultureInfo.InvariantCulture);
                UiGeometryBuilder.DrawText(v, cnt, mx + curSize - UiGeometryBuilder.MeasureText(cnt, 1.45f), my + curSize - 12.0f, 1.45f, Vector4.One, shadow: true);
            }
        }
        else if (hoveredTitle is not null)
        {
            UiGeometryBuilder.DrawMinecraftTooltipBox(v, state.CursorScreenPos.X + 14.0f, state.CursorScreenPos.Y - 12.0f, width, hoveredTitle, hoveredDetail);
        }

        if (!string.IsNullOrEmpty(state.StatusMessage))
        {
            UiGeometryBuilder.DrawCenteredText(v, state.StatusMessage, winX + (winW * 0.5f), winY + winH + 10.0f, 1.5f, new Vector4(1.0f, 0.92f, 0.52f, 0.96f), shadow: true);
        }
    }

    public static float GetTabScreenX(float winX, int tabIndex, float scale) =>
        tabIndex == 6 ? winX + ((195.0f - 26.0f) * scale) : winX + (tabIndex * 27.0f * scale);
}
