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

        if (state.SleepFadeAlpha > 0.01f)
        {
            var sleepA = Math.Clamp(state.SleepFadeAlpha, 0.0f, 0.94f);
            DrawRect(v, 0, 0, width, height, new Vector4(0.02f, 0.03f, 0.09f, sleepA));
            UiGeometryBuilder.DrawCenteredText(v, "Sleeping until dawn...", width * 0.5f, height * 0.44f, 2.1f, new Vector4(0.92f, 0.94f, 1.0f, sleepA), shadow: true);
        }

        if (state.IsSpectatorMode && state.Mode == UiOverlayMode.InGame)
        {
            UiGeometryBuilder.DrawCenteredText(v, "SPECTATOR MODE (Noclip Flight)", width * 0.5f, 14.0f, 1.45f, new Vector4(0.75f, 0.88f, 1.0f, 0.86f), shadow: true);
        }
    }

    public static void BuildStatusEffectBadges(List<TexturedUiVertex> v, float width, FrameOverlayState state)
    {
        if (state.StatusEffects is not { Count: > 0 } effects) return;

        const float cardW = 168.0f;
        const float cardH = 26.0f;
        var x = width - cardW - 12.0f;
        for (var i = 0; i < effects.Count; i++)
        {
            var eff = effects[i];
            var y = 12.0f + (i * 31.0f);
            DrawRect(v, x - 2.0f, y - 2.0f, cardW + 4.0f, cardH + 4.0f, new Vector4(eff.AccentColor.X, eff.AccentColor.Y, eff.AccentColor.Z, 0.85f));
            DrawRect(v, x, y, cardW, cardH, new Vector4(0.08f, 0.09f, 0.12f, 0.86f));
            DrawRect(v, x + 5.0f, y + 5.0f, 16.0f, 16.0f, new Vector4(eff.AccentColor.X, eff.AccentColor.Y, eff.AccentColor.Z, 0.95f));
            var mins = eff.RemainingSeconds / 60;
            var secs = eff.RemainingSeconds % 60;
            var txt = $"{eff.Label} {mins}:{secs:D2}";
            UiGeometryBuilder.DrawText(v, txt, x + 27.0f, y + 7.0f, 1.30f, Vector4.One, shadow: true);
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
        _ = width;
        _ = height;

        // Offhand HUD slot to the left of the 9-slot hotbar whenever an Offhand item is equipped
        if (!state.OffhandIsEmpty && (state.Mode == UiOverlayMode.InGame || state.Mode == UiOverlayMode.ChatInput))
        {
            var hudScale = hotbarWidth / 182.0f;
            var offX = hotbarStartX - (29.0f * hudScale);
            UiGuiSpriteHelper.DrawGuiSubRect(
                v,
                GuiTextureAtlasGenerator.WidgetsOriginX,
                GuiTextureAtlasGenerator.WidgetsOriginY,
                24.0f, 23.0f, 22.0f, 22.0f,
                offX, hotbarTopY,
                22.0f * hudScale, 22.0f * hudScale,
                Vector4.One);

            var offTile = state.OffhandItemSpriteTile;
            if (offTile < 0)
            {
                offTile = state.OffhandFrontTile >= 0 ? state.OffhandFrontTile : state.OffhandSideTile;
            }

            if (offTile >= 0)
            {
                DrawAtlasSprite(v, offTile, offX + (3.0f * hudScale), hotbarTopY + (3.0f * hudScale), 16.0f * hudScale, Vector4.One);
            }

            if (state.OffhandCount > 1)
            {
                var cntStr = state.OffhandCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
                UiGeometryBuilder.DrawText(v, cntStr, offX + (19.0f * hudScale) - (cntStr.Length * 9.0f), hotbarTopY + (13.0f * hudScale), 1.5f, Vector4.One, shadow: true);
            }
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
