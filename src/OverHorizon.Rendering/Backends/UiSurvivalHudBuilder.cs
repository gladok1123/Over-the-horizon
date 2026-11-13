using System.Numerics;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Textures;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Builds screen-space quads for underwater tinting, PvP/mob hurt flash, and real <c>icons.png</c>
/// Survival Hearts, Armor Shields, Hunger Drumsticks, Oxygen Bubbles, and <c>16x16</c> item atlas sprites.
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

    public static void DrawCrosshairIcon(List<TexturedUiVertex> v, float centerX, float centerY)
    {
        const float size = 27.0f;
        UiGuiSpriteHelper.DrawGuiSubRect(
            v,
            GuiTextureAtlasGenerator.IconsOriginX,
            GuiTextureAtlasGenerator.IconsOriginY,
            0, 0, 15, 15,
            MathF.Round(centerX - (size * 0.5f)),
            MathF.Round(centerY - (size * 0.5f)),
            size,
            size,
            new Vector4(1.0f, 1.0f, 1.0f, 0.92f));
    }

    public static void BuildUnderwaterAndHurtScreenTint(
        List<TexturedUiVertex> v,
        float width,
        float height,
        FrameOverlayState state)
    {
        if (state.IsUnderwater)
        {
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
            const float barW = 74.0f;
            const float barH = 7.0f;
            var bx = cx - (barW * 0.5f);
            var by = cy + 18.0f;
            DrawRect(v, bx - 2.0f, by - 2.0f, barW + 4.0f, barH + 4.0f, new Vector4(0.05f, 0.06f, 0.08f, 0.85f));
            DrawRect(v, bx, by, barW * Math.Clamp(state.MiningProgress, 0.0f, 1.0f), barH, new Vector4(0.35f, 0.92f, 0.42f, 0.96f));
        }

        if (!state.IsSurvivalMode)
        {
            return;
        }

        const float iconDrawSize = 22.0f;
        const float iconStep = 21.0f;
        var heartY = hotbarTopY - 27.0f;
        var hp = Math.Clamp(state.Health, 0.0f, state.MaxHealth);

        // 10 Hearts from icons.png (container 16,0; full 52,0; half 61,0)
        for (var i = 0; i < 10; i++)
        {
            var hx = hotbarStartX + (i * iconStep);
            DrawIcon9x9(v, 16, 0, hx, heartY, iconDrawSize);
            var thresh = (i + 1) * 2.0f;
            if (hp >= thresh - 0.2f)
            {
                DrawIcon9x9(v, 52, 0, hx, heartY, iconDrawSize);
            }
            else if (hp >= thresh - 1.2f)
            {
                DrawIcon9x9(v, 61, 0, hx, heartY, iconDrawSize);
            }
        }

        // Armor Shield Bar above hearts when wearing armor! (icons.png empty 16,9; half 25,9; full 34,9)
        if (state.ArmorDefensePoints > 0)
        {
            var armorY = heartY - 24.0f;
            for (var i = 0; i < 10; i++)
            {
                var ax = hotbarStartX + (i * iconStep);
                var thresh = (i + 1) * 2;
                if (state.ArmorDefensePoints >= thresh)
                {
                    DrawIcon9x9(v, 34, 9, ax, armorY, iconDrawSize);
                }
                else if (state.ArmorDefensePoints == thresh - 1)
                {
                    DrawIcon9x9(v, 25, 9, ax, armorY, iconDrawSize);
                }
                else
                {
                    DrawIcon9x9(v, 16, 9, ax, armorY, iconDrawSize);
                }
            }
        }

        // 10 Hunger Drumsticks on right from icons.png (container 16,27; full 52,27; half 61,27)
        var rightEdge = hotbarStartX + hotbarWidth;
        var hunger = Math.Clamp(state.Hunger, 0, 20);
        for (var i = 0; i < 10; i++)
        {
            var fx = rightEdge - ((i + 1) * iconStep);
            DrawIcon9x9(v, 16, 27, fx, heartY, iconDrawSize);
            var thresh = (i + 1) * 2;
            if (hunger >= thresh)
            {
                DrawIcon9x9(v, 52, 27, fx, heartY, iconDrawSize);
            }
            else if (hunger == thresh - 1)
            {
                DrawIcon9x9(v, 61, 27, fx, heartY, iconDrawSize);
            }
        }

        // 10 Oxygen Bubbles from icons.png (16,18)
        if (state.IsUnderwater || state.AirSupplyNormalized < 0.99f)
        {
            var bubbleY = heartY - 24.0f;
            var bubbles = (int)MathF.Ceiling(Math.Clamp(state.AirSupplyNormalized, 0.0f, 1.0f) * 10.0f);
            for (var i = 0; i < bubbles; i++)
            {
                var bx = rightEdge - ((i + 1) * iconStep);
                DrawIcon9x9(v, 16, 18, bx, bubbleY, iconDrawSize);
            }
        }
    }

    private static void DrawIcon9x9(List<TexturedUiVertex> v, float srcX, float srcY, float dstX, float dstY, float size) =>
        UiGuiSpriteHelper.DrawGuiSubRect(
            v,
            GuiTextureAtlasGenerator.IconsOriginX,
            GuiTextureAtlasGenerator.IconsOriginY,
            srcX, srcY, 9, 9,
            dstX, dstY, size, size,
            Vector4.One);

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
