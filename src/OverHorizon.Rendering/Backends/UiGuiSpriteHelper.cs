using System.Numerics;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Textures;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Emits screen-space quads sampling real <c>23w13a__or__b</c> GUI sheets (<c>widgets.png</c>, <c>icons.png</c>,
/// <c>container/inventory.png</c>, <c>container/crafting_table.png</c>, <c>container/furnace.png</c>) from <c>uGuiAtlas</c>.
/// </summary>
internal static class UiGuiSpriteHelper
{
    public const float ContainerGuiScale = 3.0f;

    public static void DrawGuiSubRect(
        List<TexturedUiVertex> v,
        int sheetOriginX,
        int sheetOriginY,
        float srcX,
        float srcY,
        float srcW,
        float srcH,
        float dstX,
        float dstY,
        float dstW,
        float dstH,
        Vector4 tint)
    {
        var u0 = (sheetOriginX + srcX + 0.05f) / GuiTextureAtlasGenerator.AtlasWidth;
        var v0 = (sheetOriginY + srcY + 0.05f) / GuiTextureAtlasGenerator.AtlasHeight;
        var u1 = (sheetOriginX + srcX + srcW - 0.05f) / GuiTextureAtlasGenerator.AtlasWidth;
        var v1 = (sheetOriginY + srcY + srcH - 0.05f) / GuiTextureAtlasGenerator.AtlasHeight;

        // Encoding (-uv - 3.0) routes SilkOpenGlUiRenderer to sample uGuiAtlas!
        var uv0 = new Vector2(-(u0 + 3.0f), -(v0 + 3.0f));
        var uv1 = new Vector2(-(u1 + 3.0f), -(v0 + 3.0f));
        var uv2 = new Vector2(-(u1 + 3.0f), -(v1 + 3.0f));
        var uv3 = new Vector2(-(u0 + 3.0f), -(v1 + 3.0f));

        var p0 = new Vector2(dstX, dstY);
        var p1 = new Vector2(dstX + dstW, dstY);
        var p2 = new Vector2(dstX + dstW, dstY + dstH);
        var p3 = new Vector2(dstX, dstY + dstH);

        v.Add(new TexturedUiVertex(p0, uv0, tint));
        v.Add(new TexturedUiVertex(p1, uv1, tint));
        v.Add(new TexturedUiVertex(p2, uv2, tint));
        v.Add(new TexturedUiVertex(p0, uv0, tint));
        v.Add(new TexturedUiVertex(p2, uv2, tint));
        v.Add(new TexturedUiVertex(p3, uv3, tint));
    }

    /// <summary>Draws an authentic split-cap stone button from <c>widgets.png</c> (<c>y = 46/66/86</c>).</summary>
    public static void DrawStoneButton(List<TexturedUiVertex> v, in UiButtonItem b)
    {
        var srcY = (b.IsHovered || b.IsFocused) ? 86.0f : 66.0f;
        DrawSplitWidgetBar(v, srcY, b.X, b.Y, b.Width, b.Height);
    }

    /// <summary>Draws an authentic Minecraft slider track + draggable stone handle knob.</summary>
    public static void DrawSliderControl(List<TexturedUiVertex> v, in UiButtonItem b)
    {
        // Recessed dark track from widgets.png y=46
        DrawSplitWidgetBar(v, 46.0f, b.X, b.Y, b.Width, b.Height);

        var handleW = MathF.Max(14.0f, b.Height * 0.42f);
        var usable = MathF.Max(0.0f, b.Width - handleW);
        var handleX = b.X + (Math.Clamp(b.SliderNormalizedValue, 0.0f, 1.0f) * usable);
        var knobSrcY = b.IsHovered ? 86.0f : 66.0f;

        // Left 4px + Right 4px cap of widgets.png stone button forms authentic 8x20 slider handle!
        var halfKnob = handleW * 0.5f;
        DrawGuiSubRect(v, GuiTextureAtlasGenerator.WidgetsOriginX, GuiTextureAtlasGenerator.WidgetsOriginY, 0, knobSrcY, 4, 20, handleX, b.Y, halfKnob, b.Height, Vector4.One);
        DrawGuiSubRect(v, GuiTextureAtlasGenerator.WidgetsOriginX, GuiTextureAtlasGenerator.WidgetsOriginY, 196, knobSrcY, 4, 20, handleX + halfKnob, b.Y, halfKnob, b.Height, Vector4.One);
    }

    /// <summary>Draws the <c>176x166</c> container background window and furnace progress overlays.</summary>
    public static (float WinX, float WinY, float WinW, float WinH) DrawContainerWindow(
        List<TexturedUiVertex> v,
        float screenWidth,
        float screenHeight,
        FrameOverlayState state)
    {
        var winW = 176.0f * ContainerGuiScale;
        var winH = 166.0f * ContainerGuiScale;
        var winX = MathF.Round((screenWidth - winW) * 0.5f);
        var winY = MathF.Max(22.0f, MathF.Round((screenHeight - winH) * 0.44f));

        var (ox, oy) = state.Mode switch
        {
            UiOverlayMode.CraftingTableScreen => (GuiTextureAtlasGenerator.CraftingOriginX, GuiTextureAtlasGenerator.CraftingOriginY),
            UiOverlayMode.FurnaceScreen => (GuiTextureAtlasGenerator.FurnaceOriginX, GuiTextureAtlasGenerator.FurnaceOriginY),
            UiOverlayMode.BarrelScreen or UiOverlayMode.ChestScreen => (GuiTextureAtlasGenerator.GenericContainerOriginX, GuiTextureAtlasGenerator.GenericContainerOriginY),
            _ => (GuiTextureAtlasGenerator.InventoryOriginX, GuiTextureAtlasGenerator.InventoryOriginY),
        };

        DrawGuiSubRect(v, ox, oy, 0, 0, 176, 166, winX, winY, winW, winH, Vector4.One);

        if (state.Mode == UiOverlayMode.FurnaceScreen)
        {
            var burn = Math.Clamp(state.FurnaceBurnNormalized, 0.0f, 1.0f);
            if (burn > 0.01f)
            {
                var pxH = MathF.Ceiling(14.0f * burn);
                var ySkip = 14.0f - pxH;
                DrawGuiSubRect(
                    v, ox, oy,
                    176.0f, ySkip, 14.0f, pxH,
                    winX + (56.0f * ContainerGuiScale),
                    winY + ((36.0f + ySkip) * ContainerGuiScale),
                    14.0f * ContainerGuiScale,
                    pxH * ContainerGuiScale,
                    Vector4.One);
            }

            var cook = Math.Clamp(state.FurnaceCookNormalized, 0.0f, 1.0f);
            if (cook > 0.01f)
            {
                var pxW = MathF.Ceiling(24.0f * cook);
                DrawGuiSubRect(
                    v, ox, oy,
                    176.0f, 14.0f, pxW, 17.0f,
                    winX + (79.0f * ContainerGuiScale),
                    winY + (34.0f * ContainerGuiScale),
                    pxW * ContainerGuiScale,
                    17.0f * ContainerGuiScale,
                    Vector4.One);
            }
        }

        return (winX, winY, winW, winH);
    }

    private static void DrawSplitWidgetBar(List<TexturedUiVertex> v, float srcY, float dstX, float dstY, float dstW, float dstH)
    {
        var scaleY = dstH / 20.0f;
        var halfDstW = dstW * 0.5f;
        var srcSliceW = Math.Clamp(halfDstW / Math.Max(1.0f, scaleY), 4.0f, 100.0f);

        DrawGuiSubRect(
            v,
            GuiTextureAtlasGenerator.WidgetsOriginX,
            GuiTextureAtlasGenerator.WidgetsOriginY,
            0.0f,
            srcY,
            srcSliceW,
            20.0f,
            dstX,
            dstY,
            halfDstW,
            dstH,
            Vector4.One);

        DrawGuiSubRect(
            v,
            GuiTextureAtlasGenerator.WidgetsOriginX,
            GuiTextureAtlasGenerator.WidgetsOriginY,
            200.0f - srcSliceW,
            srcY,
            srcSliceW,
            20.0f,
            dstX + halfDstW,
            dstY,
            halfDstW,
            dstH,
            Vector4.One);
    }
}
