using System.Globalization;
using System.Numerics;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Textures;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Pure CPU screen-space UI batch builder rendering real <c>23w13a__or__b</c> GUI elements
/// (<c>widgets.png</c> hotbar/offhand/buttons/sliders, <c>icons.png</c> HUD, <c>container/*.png</c>,
/// <c>creative_inventory/*.png</c>, Title emblem, World Selection/Creation, and floating cursor stacks).
/// </summary>
public static class UiGeometryBuilder
{
    private static readonly Vector2 WhiteUv = new(124.0f / 128.0f, 60.0f / 64.0f);

    /// <summary>Builds all screen-space UI triangles for the current frame.</summary>
    public static void BuildFrameQuads(
        List<TexturedUiVertex> vertices,
        FirstPersonCamera camera,
        float width,
        float height,
        FrameOverlayState state)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(state);

        vertices.Clear();

        if (state.Mode == UiOverlayMode.TitleMenu)
        {
            UiTitleAndCreativeScreenBuilder.BuildTitleScreen(vertices, width, height, state);
            return;
        }

        if (state.Mode is UiOverlayMode.WorldSelectMenu or UiOverlayMode.CreateWorldMenu or UiOverlayMode.MultiplayerMenu)
        {
            UiTitleAndCreativeScreenBuilder.BuildDirtMenuBackdrop(vertices, width, height);
            BuildModalMenuLabelsAndButtons(vertices, width, height, state);
            return;
        }

        UiSurvivalHudBuilder.BuildUnderwaterAndHurtScreenTint(vertices, width, height, state);
        UiSurvivalHudBuilder.BuildStatusEffectBadges(vertices, width, state);
        BuildProjectedNametags(vertices, camera, width, height, state.RemotePlayers);
        BuildInGameHud(vertices, width, height, state);
        if (state.Mode != UiOverlayMode.InGame)
        {
            BuildModalOverlay(vertices, width, height, state);
        }
    }

    internal static float MeasureText(ReadOnlySpan<char> text, float scale) => text.Length * 8.0f * scale;

    private static void BuildModalOverlay(List<TexturedUiVertex> v, float width, float height, FrameOverlayState state)
    {
        if (state.Mode == UiOverlayMode.ChatInput)
        {
            var barY = height - 116.0f;
            DrawRect(v, 18.0f, barY, width - 36.0f, 30.0f, new Vector4(0.04f, 0.05f, 0.07f, 0.84f));
            DrawText(v, "> " + state.ChatInputBuffer + "_", 26.0f, barY + 8.0f, 1.8f, new Vector4(1.0f, 1.0f, 1.0f, 0.98f), shadow: true);
            return;
        }

        if (state.Mode == UiOverlayMode.DeathScreen)
        {
            DrawRect(v, 0, 0, width, height, new Vector4(0.42f, 0.04f, 0.04f, 0.76f));
            var dcx = width * 0.5f;
            DrawCenteredText(v, "YOU DIED!", dcx, height * 0.24f, 4.2f, new Vector4(1.0f, 0.92f, 0.92f, 1.0f), shadow: true);
            DrawCenteredText(v, state.MenuSubtitle, dcx, (height * 0.24f) + 52.0f, 1.8f, new Vector4(1.0f, 0.84f, 0.65f, 0.95f), shadow: true);
            DrawButtons(v, state.Buttons);
            return;
        }

        DrawRect(v, 0, 0, width, height, new Vector4(0.03f, 0.04f, 0.07f, 0.72f));

        if (state.Mode is UiOverlayMode.SurvivalInventory or UiOverlayMode.CraftingTableScreen or UiOverlayMode.FurnaceScreen or
            UiOverlayMode.BarrelScreen or UiOverlayMode.ChestScreen)
        {
            BuildRealGameContainerScreen(v, width, height, state);
            return;
        }

        if (state.Mode == UiOverlayMode.BlockInventory)
        {
            UiTitleAndCreativeScreenBuilder.BuildCreativeInventoryScreen(v, width, height, state);
            return;
        }

        BuildModalMenuLabelsAndButtons(v, width, height, state);
    }

    private static void BuildModalMenuLabelsAndButtons(List<TexturedUiVertex> v, float width, float height, FrameOverlayState state)
    {
        var cx = width * 0.5f;
        if (!string.IsNullOrEmpty(state.MenuTitle))
        {
            DrawCenteredText(v, state.MenuTitle, cx, 24.0f, 2.7f, Vector4.One, shadow: true);
        }

        if (!string.IsNullOrEmpty(state.MenuSubtitle))
        {
            DrawCenteredText(v, state.MenuSubtitle, cx, 56.0f, 1.55f, new Vector4(0.80f, 0.88f, 0.96f, 0.95f), shadow: true);
        }

        DrawButtons(v, state.Buttons);

        if (!string.IsNullOrEmpty(state.StatusMessage))
        {
            DrawCenteredText(v, state.StatusMessage, cx, height - 30.0f, 1.55f, new Vector4(1.0f, 0.84f, 0.42f, 1.0f), shadow: true);
        }
    }

    private static void BuildRealGameContainerScreen(List<TexturedUiVertex> v, float width, float height, FrameOverlayState state)
    {
        var (winX, winY, winW, winH) = UiGuiSpriteHelper.DrawContainerWindow(v, width, height, state);
        const float s = UiGuiSpriteHelper.ContainerGuiScale;

        var labelCol = new Vector4(0.25f, 0.25f, 0.25f, 1.0f);
        if (state.Mode == UiOverlayMode.SurvivalInventory)
        {
            DrawText(v, "Crafting", winX + (97.0f * s), winY + (6.0f * s), 1.55f, labelCol, shadow: false);
        }
        else if (state.Mode == UiOverlayMode.CraftingTableScreen)
        {
            DrawText(v, "Crafting", winX + (29.0f * s), winY + (6.0f * s), 1.55f, labelCol, shadow: false);
        }
        else if (state.Mode == UiOverlayMode.FurnaceScreen)
        {
            DrawText(v, "Furnace", winX + (62.0f * s), winY + (6.0f * s), 1.55f, labelCol, shadow: false);
        }
        else if (state.Mode == UiOverlayMode.BarrelScreen)
        {
            DrawText(v, "Barrel", winX + (8.0f * s), winY + (6.0f * s), 1.55f, labelCol, shadow: false);
        }
        else if (state.Mode == UiOverlayMode.ChestScreen)
        {
            DrawText(v, "Chest", winX + (8.0f * s), winY + (6.0f * s), 1.55f, labelCol, shadow: false);
        }

        DrawText(v, "Inventory", winX + (8.0f * s), winY + (73.0f * s), 1.55f, labelCol, shadow: false);

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
                    DrawText(v, cnt, cell.X + cell.Size - MeasureText(cnt, 1.45f) - 1.0f, cell.Y + cell.Size - 13.0f, 1.45f, Vector4.One, shadow: true);
                }

                if (cell.IsHovered)
                {
                    DrawRect(v, cell.X, cell.Y, cell.Size, cell.Size, new Vector4(1.0f, 1.0f, 1.0f, 0.36f));
                    if (!string.IsNullOrEmpty(cell.DisplayName))
                    {
                        hoveredTitle = cell.DisplayName;
                        hoveredDetail = cell.TooltipDetail;
                    }
                }
            }
        }

        DrawButtons(v, state.Buttons);

        if (state.CursorItemTile >= 0 && state.CursorItemCount > 0)
        {
            const float curSize = 44.0f;
            var mx = state.CursorScreenPos.X - (curSize * 0.5f);
            var my = state.CursorScreenPos.Y - (curSize * 0.5f);
            UiSurvivalHudBuilder.DrawAtlasSprite(v, state.CursorItemTile, mx, my, curSize, Vector4.One);
            if (state.CursorItemCount > 1)
            {
                var cnt = state.CursorItemCount.ToString(CultureInfo.InvariantCulture);
                DrawText(v, cnt, mx + curSize - MeasureText(cnt, 1.45f), my + curSize - 12.0f, 1.45f, Vector4.One, shadow: true);
            }
        }
        else if (hoveredTitle is not null)
        {
            DrawMinecraftTooltipBox(v, state.CursorScreenPos.X + 14.0f, state.CursorScreenPos.Y - 12.0f, width, hoveredTitle, hoveredDetail);
        }

        if (!string.IsNullOrEmpty(state.StatusMessage))
        {
            DrawCenteredText(v, state.StatusMessage, winX + (winW * 0.5f), winY + winH + 10.0f, 1.55f, new Vector4(1.0f, 0.92f, 0.52f, 0.96f), shadow: true);
        }
    }

    internal static void DrawMinecraftTooltipBox(List<TexturedUiVertex> v, float x, float y, float maxScreenW, string title, string? detail)
    {
        var w1 = MeasureText(title, 1.55f);
        var w2 = string.IsNullOrEmpty(detail) ? 0.0f : MeasureText(detail, 1.3f);
        var boxW = MathF.Max(w1, w2) + 16.0f;
        var boxH = string.IsNullOrEmpty(detail) ? 24.0f : 42.0f;
        var clampedX = MathF.Min(x, maxScreenW - boxW - 8.0f);
        var clampedY = MathF.Max(8.0f, y);

        DrawRect(v, clampedX - 2.0f, clampedY - 2.0f, boxW + 4.0f, boxH + 4.0f, new Vector4(0.18f, 0.04f, 0.36f, 0.95f));
        DrawRect(v, clampedX, clampedY, boxW, boxH, new Vector4(0.06f, 0.02f, 0.10f, 0.94f));
        DrawText(v, title, clampedX + 8.0f, clampedY + 5.0f, 1.55f, Vector4.One, shadow: true);
        if (!string.IsNullOrEmpty(detail))
        {
            DrawText(v, detail, clampedX + 8.0f, clampedY + 23.0f, 1.3f, new Vector4(0.68f, 0.68f, 0.96f, 0.98f), shadow: true);
        }
    }

    private static void BuildInGameHud(List<TexturedUiVertex> v, float width, float height, FrameOverlayState state)
    {
        var cx = width * 0.5f;
        var cy = height * 0.5f;

        if (state.Mode == UiOverlayMode.InGame)
        {
            UiSurvivalHudBuilder.DrawCrosshairIcon(v, cx, cy);
        }

        var colors = state.HotbarSlotColors.Span;
        var tiles = state.HotbarSlotTiles.Span;
        var counts = state.HotbarSlotCounts.Span;
        if (!colors.IsEmpty)
        {
            const float hbScale = 2.8f;
            const float barW = 182.0f * hbScale;
            const float barH = 22.0f * hbScale;
            var barX = MathF.Round(cx - (barW * 0.5f));
            var barY = height - barH - 6.0f;

            UiSurvivalHudBuilder.BuildSurvivalBarsAndMiningProgress(v, width, height, barX, barW, barY, state);

            UiGuiSpriteHelper.DrawGuiSubRect(v, GuiTextureAtlasGenerator.WidgetsOriginX, GuiTextureAtlasGenerator.WidgetsOriginY, 0, 0, 182, 22, barX, barY, barW, barH, Vector4.One);

            var sel = Math.Clamp(state.SelectedHotbarSlot, 0, 8);
            UiGuiSpriteHelper.DrawGuiSubRect(
                v,
                GuiTextureAtlasGenerator.WidgetsOriginX,
                GuiTextureAtlasGenerator.WidgetsOriginY,
                0, 22, 24, 24,
                barX + (((sel * 20.0f) - 1.0f) * hbScale),
                barY - (1.0f * hbScale),
                24.0f * hbScale,
                24.0f * hbScale,
                Vector4.One);

            var iconSize = 16.0f * hbScale;
            for (var i = 0; i < Math.Min(9, colors.Length); i++)
            {
                var sx = barX + ((3.0f + (i * 20.0f)) * hbScale);
                var sy = barY + (3.0f * hbScale);
                var slotCount = i < counts.Length ? counts[i] : 1;
                var tileIdx = i < tiles.Length ? tiles[i] : -1;

                if (slotCount > 0 && tileIdx >= 0)
                {
                    UiSurvivalHudBuilder.DrawAtlasSprite(v, tileIdx, sx, sy, iconSize, Vector4.One);
                }

                if (slotCount > 1)
                {
                    var sCnt = slotCount.ToString(CultureInfo.InvariantCulture);
                    DrawText(v, sCnt, sx + iconSize - MeasureText(sCnt, 1.4f), sy + iconSize - 12.0f, 1.4f, Vector4.One, shadow: true);
                }
            }

            if (!string.IsNullOrEmpty(state.ActiveBlockDisplayName) && state.Mode == UiOverlayMode.InGame)
            {
                var labelY = state.IsSurvivalMode ? barY - 56.0f : barY - 24.0f;
                DrawCenteredText(v, state.ActiveBlockDisplayName, cx, labelY, 1.7f, new Vector4(1.0f, 0.96f, 0.78f, 0.95f), shadow: true);
            }
        }

        if (state.RecentChatLines is { Count: > 0 } chatLines)
        {
            var baseChatY = height - 160.0f;
            for (var i = 0; i < chatLines.Count; i++)
            {
                var line = chatLines[chatLines.Count - 1 - i];
                var ly = baseChatY - (i * 20.0f);
                var tw = MeasureText(line, 1.5f);
                DrawRect(v, 16.0f, ly - 2.0f, tw + 12.0f, 18.0f, new Vector4(0.05f, 0.06f, 0.08f, 0.58f));
                DrawText(v, line, 22.0f, ly + 1.0f, 1.5f, new Vector4(0.96f, 0.97f, 1.0f, 0.96f), shadow: true);
            }
        }

        if (!string.IsNullOrEmpty(state.DebugOverlayText))
        {
            var lines = state.DebugOverlayText.Split('\n');
            var y = 12.0f;
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                if (line.Length == 0) continue;
                var w = MeasureText(line, 1.5f);
                DrawRect(v, 10.0f, y - 2.0f, w + 10.0f, 16.0f, new Vector4(0.06f, 0.07f, 0.09f, 0.62f));
                DrawText(v, line, 15.0f, y, 1.5f, new Vector4(0.92f, 0.96f, 1.0f, 0.98f), shadow: true);
                y += 18.0f;
            }
        }
        else if (state.Mode == UiOverlayMode.InGame)
        {
            var creativeBadge = state.IsCreativeFlying ? "[CREATIVE - FLYING]" : "[CREATIVE]";
            var modeBadge = state.IsSurvivalMode ? "[SURVIVAL]" : creativeBadge;
            DrawText(v, $"{modeBadge} E: Inv | F: Offhand | G/2xSpace: Fly | /: Cmds | Esc: Menu", 12.0f, 12.0f, 1.35f, new Vector4(0.92f, 0.95f, 1.0f, 0.80f), shadow: true);
        }
    }

    private static void BuildProjectedNametags(List<TexturedUiVertex> v, FirstPersonCamera camera, float width, float height, IReadOnlyList<RemotePlayerAvatar> players)
    {
        if (players.Count == 0) return;

        var vp = camera.BuildCameraRelativeViewProjectionMatrix();
        for (var i = 0; i < players.Count; i++)
        {
            var p = players[i];
            var relHead = new Vector3((float)(p.FeetPosition.X - camera.Position.X), (float)(p.FeetPosition.Y + 2.12 - camera.Position.Y), (float)(p.FeetPosition.Z - camera.Position.Z));
            var clip = Vector4.Transform(new Vector4(relHead, 1.0f), vp);
            if (clip.W <= 0.1f) continue;

            var ndcX = clip.X / clip.W;
            var ndcY = clip.Y / clip.W;
            if (MathF.Abs(ndcX) > 1.05f || MathF.Abs(ndcY) > 1.05f) continue;

            var sx = (ndcX + 1.0f) * 0.5f * width;
            var sy = (1.0f - ndcY) * 0.5f * height;
            var tw = MeasureText(p.Username, 1.6f);
            DrawRect(v, sx - (tw * 0.5f) - 5.0f, sy - 4.0f, tw + 10.0f, 18.0f, new Vector4(0.05f, 0.06f, 0.08f, 0.65f));
            DrawCenteredText(v, p.Username, sx, sy - 1.0f, 1.6f, Vector4.One, shadow: true);

            if (p.Health < 19.9f)
            {
                const float hpW = 54.0f;
                DrawRect(v, sx - (hpW * 0.5f) - 1.0f, sy + 16.0f, hpW + 2.0f, 6.0f, new Vector4(0.06f, 0.06f, 0.08f, 0.85f));
                DrawRect(v, sx - (hpW * 0.5f), sy + 17.0f, hpW * Math.Clamp(p.Health / 20.0f, 0.0f, 1.0f), 4.0f, new Vector4(0.95f, 0.22f, 0.24f, 0.98f));
            }
        }
    }

    internal static void DrawButtons(List<TexturedUiVertex> v, IReadOnlyList<UiButtonItem>? buttons)
    {
        if (buttons is null) return;

        for (var i = 0; i < buttons.Count; i++)
        {
            var b = buttons[i];
            if (b.IsInputField)
            {
                var border = b.IsFocused ? new Vector4(1.0f, 1.0f, 1.0f, 1.0f) : new Vector4(0.62f, 0.62f, 0.64f, 1.0f);
                DrawRect(v, b.X - 2.0f, b.Y - 2.0f, b.Width + 4.0f, b.Height + 4.0f, border);
                DrawRect(v, b.X, b.Y, b.Width, b.Height, new Vector4(0.0f, 0.0f, 0.0f, 0.96f));
                var display = b.IsFocused ? b.Label + "_" : b.Label;
                DrawText(v, display, b.X + 10.0f, b.Y + ((b.Height - 16.0f) * 0.5f), 1.85f, Vector4.One, shadow: true);
                continue;
            }

            if (b.IsSlider)
            {
                UiGuiSpriteHelper.DrawSliderControl(v, in b);
            }
            else
            {
                UiGuiSpriteHelper.DrawStoneButton(v, in b);
            }

            var scale = b.Height <= 34.0f ? 1.45f : 1.80f;
            var maxTextW = MathF.Max(24.0f, b.Width - 16.0f);
            var rawTextW = MeasureText(b.Label, scale);
            if (rawTextW > maxTextW)
            {
                scale = Math.Clamp(scale * (maxTextW / rawTextW), 0.95f, scale);
            }

            var textCol = (b.IsHovered || b.IsFocused) ? new Vector4(1.0f, 1.0f, 0.62f, 1.0f) : new Vector4(0.95f, 0.96f, 0.98f, 1.0f);
            DrawCenteredText(v, b.Label, b.X + (b.Width * 0.5f), b.Y + ((b.Height - (8.0f * scale)) * 0.5f), scale, textCol, shadow: true);
        }
    }

    internal static void DrawCenteredText(List<TexturedUiVertex> v, ReadOnlySpan<char> text, float centerX, float y, float scale, Vector4 color, bool shadow) =>
        DrawText(v, text, MathF.Round(centerX - (MeasureText(text, scale) * 0.5f)), MathF.Round(y), scale, color, shadow);

    internal static void DrawText(List<TexturedUiVertex> v, ReadOnlySpan<char> text, float x, float y, float scale, Vector4 color, bool shadow)
    {
        var step = 8.0f * scale;
        if (shadow)
        {
            var off = MathF.Max(1.0f, scale);
            var shadowCol = new Vector4(0.08f, 0.08f, 0.10f, color.W * 0.92f);
            for (var i = 0; i < text.Length; i++)
            {
                EmitGlyphQuad(v, PixelFontGlyphs.MapCharToCell(text[i]), x + (i * step) + off, y + off, step, shadowCol);
            }
        }

        for (var i = 0; i < text.Length; i++)
        {
            EmitGlyphQuad(v, PixelFontGlyphs.MapCharToCell(text[i]), x + (i * step), y, step, color);
        }
    }

    private static void EmitGlyphQuad(List<TexturedUiVertex> v, int cellIndex, float x, float y, float size, Vector4 color)
    {
        if (cellIndex == ' ') return;
        var col = cellIndex & 15;
        var row = cellIndex >> 4;
        var u0 = (col * 8.0f) / PixelFontGlyphs.AtlasWidth;
        var v0 = (row * 8.0f) / PixelFontGlyphs.AtlasHeight;
        var u1 = ((col + 1) * 8.0f) / PixelFontGlyphs.AtlasWidth;
        var v1 = ((row + 1) * 8.0f) / PixelFontGlyphs.AtlasHeight;
        EmitQuad(v, x, y, size, size, new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1), color);
    }

    internal static void DrawRect(List<TexturedUiVertex> v, float x, float y, float w, float h, Vector4 color) =>
        EmitQuad(v, x, y, w, h, WhiteUv, WhiteUv, WhiteUv, WhiteUv, color);

    private static void EmitQuad(List<TexturedUiVertex> v, float x, float y, float w, float h, Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3, Vector4 color)
    {
        v.Add(new TexturedUiVertex(new Vector2(x, y), uv0, color));
        v.Add(new TexturedUiVertex(new Vector2(x + w, y), uv1, color));
        v.Add(new TexturedUiVertex(new Vector2(x + w, y + h), uv2, color));
        v.Add(new TexturedUiVertex(new Vector2(x, y), uv0, color));
        v.Add(new TexturedUiVertex(new Vector2(x + w, y + h), uv2, color));
        v.Add(new TexturedUiVertex(new Vector2(x, y + h), uv3, color));
    }
}
