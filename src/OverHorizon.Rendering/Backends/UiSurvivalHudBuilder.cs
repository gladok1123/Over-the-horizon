using System.Numerics;
using OverHorizon.Rendering.Hud;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Builds screen-space quads for underwater tinting, PvP damage flash, Survival Hearts/Hunger/Oxygen bars,
/// progressive mining bar, and <c>16x16</c> block/item atlas sprites.
/// </summary>
internal static class UiSurvivalHudBuilder
{
    private static readonly Vector2 WhiteUv = new(124.0f / 128.0f, 60.0f / 64.0f);

    public static void DrawAtlasSprite(List<TexturedUiVertex> v, int tileIndex, float x, float y, float size, Vector4 tint)
    {
        if (tileIndex < 0 || tileIndex > 255)
        {
            DrawRect(v, x, y, size, size, tint);
            return;
        }

        var col = tileIndex & 15;
        var row = tileIndex >> 4;
        var au0 = (col + 0.002f) / 16.0f;
        var av0 = (row + 0.002f) / 16.0f;
        var au1 = (col + 0.998f) / 16.0f;
        var av1 = (row + 0.998f) / 16.0f;

        // Negative UV encoding (-uv - 1.0) instructs SilkOpenGlUiRenderer to sample uItemAtlas!
        var uv0 = new Vector2(-(au0 + 1.0f), -(av0 + 1.0f));
        var uv1 = new Vector2(-(au1 + 1.0f), -(av0 + 1.0f));
        var uv2 = new Vector2(-(au1 + 1.0f), -(av1 + 1.0f));
        var uv3 = new Vector2(-(au0 + 1.0f), -(av1 + 1.0f));

        var p0 = new Vector2(x, y);
        var p1 = new Vector2(x + size, y);
        var p2 = new Vector2(x + size, y + size);
        var p3 = new Vector2(x, y + size);

        v.Add(new TexturedUiVertex(p0, uv0, tint));
        v.Add(new TexturedUiVertex(p1, uv1, tint));
        v.Add(new TexturedUiVertex(p2, uv2, tint));
        v.Add(new TexturedUiVertex(p0, uv0, tint));
        v.Add(new TexturedUiVertex(p2, uv2, tint));
        v.Add(new TexturedUiVertex(p3, uv3, tint));
    }

    public static void BuildUnderwaterAndHurtScreenTint(
        List<TexturedUiVertex> v,
        float width,
        float height,
        FrameOverlayState state)
    {
        if (state.IsUnderwater)
        {
            // Authentic aquamarine water immersion wash + deep-water border vignette
            DrawRect(v, 0, 0, width, height, new Vector4(0.04f, 0.28f, 0.56f, 0.32f));
            var borderW = MathF.Max(36.0f, width * 0.06f);
            var borderH = MathF.Max(28.0f, height * 0.06f);
            var vig = new Vector4(0.01f, 0.12f, 0.28f, 0.26f);
            DrawRect(v, 0, 0, width, borderH, vig);
            DrawRect(v, 0, height - borderH, width, borderH, vig);
            DrawRect(v, 0, borderH, borderW, height - (borderH * 2.0f), vig);
            DrawRect(v, width - borderW, borderH, borderW, height - (borderH * 2.0f), vig);
        }

        if (state.HurtFlashStrength > 0.01f)
        {
            var alpha = Math.Clamp(state.HurtFlashStrength, 0.0f, 1.0f) * 0.34f;
            DrawRect(v, 0, 0, width, height, new Vector4(0.88f, 0.08f, 0.08f, alpha));
        }
    }

    public static void BuildSurvivalBarsAndMiningProgress(
        List<TexturedUiVertex> v,
        float width,
        float height,
        float hotbarStartX,
        float hotbarWidth,
        float hotbarTopY,
        FrameOverlayState state)
    {
        var cx = width * 0.5f;
        var cy = height * 0.5f;

        if (state.MiningProgress is > 0.01f and < 1.0f && state.Mode == UiOverlayMode.InGame)
        {
            const float barW = 72.0f;
            const float barH = 7.0f;
            var bx = cx - (barW * 0.5f);
            var by = cy + 16.0f;
            DrawRect(v, bx - 2.0f, by - 2.0f, barW + 4.0f, barH + 4.0f, new Vector4(0.05f, 0.06f, 0.08f, 0.85f));
            DrawRect(v, bx, by, barW * Math.Clamp(state.MiningProgress, 0.0f, 1.0f), barH, new Vector4(0.35f, 0.92f, 0.42f, 0.96f));
        }

        if (!state.IsSurvivalMode)
        {
            return;
        }

        // 10 Hearts on the left above hotbar
        var heartY = hotbarTopY - 22.0f;
        var hp = Math.Clamp(state.Health, 0.0f, state.MaxHealth);
        for (var i = 0; i < 10; i++)
        {
            var hx = hotbarStartX + (i * 18.0f);
            DrawRect(v, hx, heartY, 14.0f, 14.0f, new Vector4(0.10f, 0.08f, 0.10f, 0.92f));
            var heartThreshold = (i + 1) * 2.0f;
            if (hp >= heartThreshold - 0.2f)
            {
                DrawHeartShape(v, hx + 1.0f, heartY + 1.0f, full: true, new Vector4(0.92f, 0.16f, 0.20f, 1.0f));
            }
            else if (hp >= heartThreshold - 1.2f)
            {
                DrawHeartShape(v, hx + 1.0f, heartY + 1.0f, full: false, new Vector4(0.92f, 0.16f, 0.20f, 1.0f));
            }
        }

        // 10 Hunger drumsticks on the right above hotbar
        var foodRightX = hotbarStartX + hotbarWidth;
        var hunger = Math.Clamp(state.Hunger, 0, 20);
        for (var i = 0; i < 10; i++)
        {
            var fx = foodRightX - ((i + 1) * 18.0f) + 4.0f;
            DrawRect(v, fx, heartY, 14.0f, 14.0f, new Vector4(0.10f, 0.09f, 0.08f, 0.92f));
            var thresh = (i + 1) * 2;
            if (hunger >= thresh)
            {
                DrawDrumstick(v, fx + 2.0f, heartY + 2.0f, full: true);
            }
            else if (hunger == thresh - 1)
            {
                DrawDrumstick(v, fx + 2.0f, heartY + 2.0f, full: false);
            }
        }

        // 10 Oxygen bubbles above hunger when underwater or recovering breath
        if (state.IsUnderwater || state.AirSupplyNormalized < 0.99f)
        {
            var bubbleY = heartY - 18.0f;
            var bubbles = (int)MathF.Ceiling(Math.Clamp(state.AirSupplyNormalized, 0.0f, 1.0f) * 10.0f);
            for (var i = 0; i < bubbles; i++)
            {
                var bx = foodRightX - ((i + 1) * 18.0f) + 5.0f;
                DrawRect(v, bx, bubbleY, 12.0f, 12.0f, new Vector4(0.18f, 0.62f, 0.96f, 0.95f));
                DrawRect(v, bx + 2.0f, bubbleY + 2.0f, 4.0f, 4.0f, new Vector4(0.85f, 0.96f, 1.0f, 0.98f));
            }
        }
    }

    private static void DrawHeartShape(List<TexturedUiVertex> v, float x, float y, bool full, Vector4 col)
    {
        DrawRect(v, x + 1.0f, y + 1.0f, 4.0f, 4.0f, col);
        DrawRect(v, x + 2.0f, y + 2.0f, 2.0f, 2.0f, new Vector4(1.0f, 0.65f, 0.68f, 1.0f));
        if (full)
        {
            DrawRect(v, x + 7.0f, y + 1.0f, 4.0f, 4.0f, col);
            DrawRect(v, x + 1.0f, y + 4.0f, 10.0f, 4.0f, col);
            DrawRect(v, x + 3.0f, y + 8.0f, 6.0f, 3.0f, col);
        }
        else
        {
            DrawRect(v, x + 1.0f, y + 4.0f, 5.0f, 4.0f, col);
            DrawRect(v, x + 3.0f, y + 8.0f, 3.0f, 3.0f, col);
        }
    }

    private static void DrawDrumstick(List<TexturedUiVertex> v, float x, float y, bool full)
    {
        var meat = new Vector4(0.78f, 0.44f, 0.20f, 1.0f);
        var bone = new Vector4(0.94f, 0.92f, 0.86f, 1.0f);
        DrawRect(v, x + 6.0f, y + 6.0f, 4.0f, 4.0f, bone);
        DrawRect(v, x + 1.0f, y + 1.0f, full ? 7.0f : 4.0f, 6.0f, meat);
    }

    private static void DrawRect(List<TexturedUiVertex> v, float x, float y, float w, float h, Vector4 color)
    {
        var p0 = new Vector2(x, y);
        var p1 = new Vector2(x + w, y);
        var p2 = new Vector2(x + w, y + h);
        var p3 = new Vector2(x, y + h);

        v.Add(new TexturedUiVertex(p0, WhiteUv, color));
        v.Add(new TexturedUiVertex(p1, WhiteUv, color));
        v.Add(new TexturedUiVertex(p2, WhiteUv, color));
        v.Add(new TexturedUiVertex(p0, WhiteUv, color));
        v.Add(new TexturedUiVertex(p2, WhiteUv, color));
        v.Add(new TexturedUiVertex(p3, WhiteUv, color));
    }
}
