using System.Globalization;
using System.Numerics;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Hud;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Pure CPU screen-space UI, bitmap-font, and <c>16x16</c> textured sprite batch builder for the "C# EDITION"
/// Title Screen, Redesigned Tabbed Creative Catalog, Survival Inventory &amp; Crafting, Underwater/Survival HUD, and PvP Nametags.
/// </summary>
public static class UiGeometryBuilder
{
    private static readonly Vector2 WhiteUv = new(124.0f / 128.0f, 60.0f / 64.0f);

    /// <summary>
    /// Populates <paramref name="vertices"/> with screen-space UI triangles for <paramref name="state"/>.
    /// </summary>
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
            BuildTitleScreen(vertices, width, height, state);
            return;
        }

        UiSurvivalHudBuilder.BuildUnderwaterAndHurtScreenTint(vertices, width, height, state);
        BuildProjectedNametags(vertices, camera, width, height, state.RemotePlayers);
        BuildInGameHud(vertices, width, height, state);

        if (state.Mode != UiOverlayMode.InGame)
        {
            BuildModalOverlay(vertices, width, height, state);
        }
    }

    private static float MeasureText(ReadOnlySpan<char> text, float scale) => text.Length * 8.0f * scale;

    private static Vector4 ResolveCellBorderColor(in UiInventoryCell c)
    {
        if (c.IsSelected)
        {
            return new Vector4(0.28f, 0.95f, 0.52f, 1.0f);
        }

        return c.IsHovered ? new Vector4(1.0f, 0.90f, 0.28f, 1.0f) : new Vector4(0.10f, 0.11f, 0.13f, 0.96f);
    }

    private static Vector4 ResolveButtonFillColor(in UiButtonItem b)
    {
        if (b.IsHovered)
        {
            return new Vector4(0.42f, 0.48f, 0.66f, 0.96f);
        }

        return b.IsFocused ? new Vector4(0.28f, 0.52f, 0.38f, 0.96f) : new Vector4(0.32f, 0.34f, 0.38f, 0.95f);
    }

    private static void BuildTitleScreen(List<TexturedUiVertex> v, float width, float height, FrameOverlayState state)
    {
        DrawRect(v, 0, 0, width, height, new Vector4(0.05f, 0.07f, 0.12f, 0.34f));

        var cx = width * 0.5f;
        var bannerY = MathF.Max(46.0f, height * 0.11f);

        var title = string.IsNullOrEmpty(state.MenuTitle) ? "OVER THE HORIZON" : state.MenuTitle;
        DrawCenteredText(v, title, cx + 4.0f, bannerY + 4.0f, 5.0f, new Vector4(0.14f, 0.14f, 0.16f, 0.95f), shadow: false);
        DrawCenteredText(v, title, cx, bannerY, 5.0f, new Vector4(0.92f, 0.93f, 0.96f, 1.0f), shadow: true);

        var sub = string.IsNullOrEmpty(state.MenuSubtitle) ? "C# EDITION" : state.MenuSubtitle;
        DrawCenteredText(v, sub, cx, bannerY + 48.0f, 2.75f, new Vector4(0.45f, 0.88f, 1.0f, 1.0f), shadow: true);

        if (!string.IsNullOrEmpty(state.SplashText))
        {
            DrawCenteredText(v, state.SplashText, cx + 150.0f, bannerY + 76.0f, 1.6f, new Vector4(1.0f, 0.92f, 0.20f, 1.0f), shadow: true);
        }

        DrawButtons(v, state.Buttons);

        if (!string.IsNullOrEmpty(state.StatusMessage))
        {
            DrawCenteredText(v, state.StatusMessage, cx, height - 64.0f, 1.6f, new Vector4(1.0f, 0.72f, 0.42f, 1.0f), shadow: true);
        }

        DrawText(v, "Over-the-Horizon v0.4.0-alpha (C# / .NET 9)", 14.0f, height - 24.0f, 1.5f, new Vector4(0.88f, 0.90f, 0.94f, 0.90f), shadow: true);
        const string rightNote = "Clean-Room Engine - Not affiliated with Mojang";
        DrawText(v, rightNote, width - MeasureText(rightNote, 1.5f) - 14.0f, height - 24.0f, 1.5f, new Vector4(0.82f, 0.84f, 0.88f, 0.85f), shadow: true);
    }

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
        var cx = width * 0.5f;

        if (state.Mode is UiOverlayMode.BlockInventory or UiOverlayMode.SurvivalInventory)
        {
            BuildFramedInventoryScreen(v, cx, height, state);
            DrawButtons(v, state.Buttons);
            return;
        }

        if (!string.IsNullOrEmpty(state.MenuTitle))
        {
            DrawCenteredText(v, state.MenuTitle, cx, MathF.Max(48.0f, height * 0.13f), 3.0f, new Vector4(1.0f, 1.0f, 1.0f, 1.0f), shadow: true);
        }

        if (!string.IsNullOrEmpty(state.MenuSubtitle))
        {
            DrawCenteredText(v, state.MenuSubtitle, cx, MathF.Max(84.0f, height * 0.19f), 1.7f, new Vector4(0.75f, 0.84f, 0.95f, 0.95f), shadow: true);
        }

        DrawButtons(v, state.Buttons);

        if (!string.IsNullOrEmpty(state.StatusMessage))
        {
            DrawCenteredText(v, state.StatusMessage, cx, height - 68.0f, 1.7f, new Vector4(1.0f, 0.82f, 0.36f, 1.0f), shadow: true);
        }
    }

    private static void BuildFramedInventoryScreen(List<TexturedUiVertex> v, float cx, float height, FrameOverlayState state)
    {
        const float panelW = 760.0f;
        var panelH = MathF.Min(540.0f, height - 90.0f);
        var panelX = cx - (panelW * 0.5f);
        var panelY = MathF.Max(34.0f, (height - panelH) * 0.44f);

        DrawBeveledPanel(
            v, panelX, panelY, panelW, panelH,
            new Vector4(0.18f, 0.19f, 0.23f, 0.96f),
            new Vector4(0.46f, 0.48f, 0.54f, 0.98f),
            new Vector4(0.08f, 0.09f, 0.11f, 0.98f));

        DrawText(v, state.MenuTitle, panelX + 18.0f, panelY + 14.0f, 2.1f, new Vector4(0.96f, 0.97f, 1.0f, 1.0f), shadow: true);

        if (state.Mode == UiOverlayMode.BlockInventory)
        {
            var searchBoxX = panelX + panelW - 270.0f;
            var searchBoxY = panelY + 10.0f;
            DrawRect(v, searchBoxX - 2.0f, searchBoxY - 2.0f, 254.0f, 30.0f, new Vector4(0.58f, 0.76f, 0.98f, 0.95f));
            DrawRect(v, searchBoxX, searchBoxY, 250.0f, 26.0f, new Vector4(0.07f, 0.08f, 0.10f, 0.98f));
            var queryText = string.IsNullOrEmpty(state.CreativeSearchQuery)
                ? "Type to search..."
                : state.CreativeSearchQuery + "_";
            var queryCol = string.IsNullOrEmpty(state.CreativeSearchQuery)
                ? new Vector4(0.56f, 0.60f, 0.68f, 0.95f)
                : new Vector4(1.0f, 0.96f, 0.58f, 1.0f);
            DrawText(v, queryText, searchBoxX + 8.0f, searchBoxY + 6.0f, 1.6f, queryCol, shadow: true);
        }
        else if (!string.IsNullOrEmpty(state.MenuSubtitle))
        {
            DrawText(v, state.MenuSubtitle, panelX + 18.0f, panelY + 38.0f, 1.5f, new Vector4(0.76f, 0.86f, 0.96f, 0.95f), shadow: true);
        }

        string? hoveredTitle = null;
        string? hoveredDetail = null;
        if (state.InventoryCells is { Count: > 0 } cells)
        {
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                var borderCol = ResolveCellBorderColor(in cell);
                DrawRect(v, cell.X - 2.0f, cell.Y - 2.0f, cell.Size + 4.0f, cell.Size + 4.0f, borderCol);
                DrawRect(v, cell.X, cell.Y, cell.Size, cell.Size, new Vector4(0.24f, 0.25f, 0.29f, 0.96f));

                if (cell.AtlasTileIndex >= 0)
                {
                    UiSurvivalHudBuilder.DrawAtlasSprite(v, cell.AtlasTileIndex, cell.X + 5.0f, cell.Y + 5.0f, cell.Size - 10.0f, Vector4.One);
                }
                else if (cell.StackCount > 0)
                {
                    DrawRect(v, cell.X + 6.0f, cell.Y + 6.0f, cell.Size - 12.0f, cell.Size - 12.0f, new Vector4(cell.Color.X, cell.Color.Y, cell.Color.Z, 1.0f));
                }

                if (cell.StackCount > 1)
                {
                    var cnt = cell.StackCount.ToString(CultureInfo.InvariantCulture);
                    DrawText(v, cnt, cell.X + cell.Size - MeasureText(cnt, 1.35f) - 3.0f, cell.Y + cell.Size - 13.0f, 1.35f, Vector4.One, shadow: true);
                }

                if (cell.IsHovered && !string.IsNullOrEmpty(cell.DisplayName))
                {
                    hoveredTitle = cell.DisplayName;
                    hoveredDetail = cell.TooltipDetail;
                }
            }
        }

        var tipY = panelY + panelH - 54.0f;
        if (hoveredTitle is not null)
        {
            DrawCenteredText(v, hoveredTitle, cx, tipY, 1.85f, new Vector4(1.0f, 0.96f, 0.56f, 1.0f), shadow: true);
            if (!string.IsNullOrEmpty(hoveredDetail))
            {
                DrawCenteredText(v, hoveredDetail, cx, tipY + 20.0f, 1.45f, new Vector4(0.76f, 0.92f, 1.0f, 0.96f), shadow: true);
            }
        }
        else if (!string.IsNullOrEmpty(state.StatusMessage))
        {
            DrawCenteredText(v, state.StatusMessage, cx, tipY + 8.0f, 1.55f, new Vector4(0.86f, 0.90f, 0.96f, 0.92f), shadow: true);
        }
    }

    private static void BuildInGameHud(List<TexturedUiVertex> v, float width, float height, FrameOverlayState state)
    {
        var cx = width * 0.5f;
        var cy = height * 0.5f;

        if (state.Mode == UiOverlayMode.InGame)
        {
            DrawRect(v, cx - 9.0f, cy - 1.0f, 18.0f, 2.5f, new Vector4(0.96f, 0.96f, 0.96f, 0.88f));
            DrawRect(v, cx - 1.0f, cy - 9.0f, 2.5f, 18.0f, new Vector4(0.96f, 0.96f, 0.96f, 0.88f));
        }

        var colors = state.HotbarSlotColors.Span;
        var tiles = state.HotbarSlotTiles.Span;
        var counts = state.HotbarSlotCounts.Span;
        if (!colors.IsEmpty)
        {
            var count = Math.Min(9, colors.Length);
            const float slotSize = 48.0f;
            const float gap = 6.0f;
            var totalW = (count * slotSize) + ((count - 1) * gap);
            var startX = cx - (totalW * 0.5f);
            var barY = height - slotSize - 14.0f;

            UiSurvivalHudBuilder.BuildSurvivalBarsAndMiningProgress(v, width, height, startX, totalW, barY, state);

            DrawBeveledPanel(
                v, startX - 8.0f, barY - 6.0f, totalW + 16.0f, slotSize + 12.0f,
                new Vector4(0.11f, 0.12f, 0.15f, 0.84f),
                new Vector4(0.32f, 0.34f, 0.38f, 0.88f),
                new Vector4(0.05f, 0.05f, 0.07f, 0.92f));

            for (var i = 0; i < count; i++)
            {
                var sx = startX + (i * (slotSize + gap));
                if (i == state.SelectedHotbarSlot)
                {
                    DrawRect(v, sx - 3.0f, barY - 3.0f, slotSize + 6.0f, slotSize + 6.0f, new Vector4(1.0f, 0.88f, 0.24f, 0.98f));
                }

                DrawRect(v, sx, barY, slotSize, slotSize, new Vector4(0.16f, 0.17f, 0.20f, 0.95f));
                var slotCount = i < counts.Length ? counts[i] : 1;
                var tileIdx = i < tiles.Length ? tiles[i] : -1;

                if (slotCount > 0)
                {
                    if (tileIdx >= 0)
                    {
                        UiSurvivalHudBuilder.DrawAtlasSprite(v, tileIdx, sx + 6.0f, barY + 6.0f, slotSize - 12.0f, Vector4.One);
                    }
                    else
                    {
                        var c = colors[i];
                        DrawRect(v, sx + 7.0f, barY + 7.0f, slotSize - 14.0f, slotSize - 14.0f, new Vector4(c.X, c.Y, c.Z, 1.0f));
                    }

                    if (slotCount > 1)
                    {
                        var sCnt = slotCount.ToString(CultureInfo.InvariantCulture);
                        DrawText(v, sCnt, sx + slotSize - MeasureText(sCnt, 1.35f) - 3.0f, barY + slotSize - 13.0f, 1.35f, Vector4.One, shadow: true);
                    }
                }

                DrawText(v, (i + 1).ToString(CultureInfo.InvariantCulture), sx + 3.0f, barY + 3.0f, 1.2f, new Vector4(0.88f, 0.90f, 0.94f, 0.85f), shadow: true);
            }

            if (!string.IsNullOrEmpty(state.ActiveBlockDisplayName) && state.Mode == UiOverlayMode.InGame)
            {
                var labelY = state.IsSurvivalMode ? barY - 44.0f : barY - 24.0f;
                DrawCenteredText(v, state.ActiveBlockDisplayName, cx, labelY, 1.7f, new Vector4(1.0f, 0.96f, 0.78f, 0.95f), shadow: true);
            }
        }

        if (state.RecentChatLines is { Count: > 0 } chatLines)
        {
            var baseChatY = height - 156.0f;
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
                if (line.Length == 0)
                {
                    continue;
                }

                var w = MeasureText(line, 1.5f);
                DrawRect(v, 10.0f, y - 2.0f, w + 10.0f, 16.0f, new Vector4(0.06f, 0.07f, 0.09f, 0.62f));
                DrawText(v, line, 15.0f, y, 1.5f, new Vector4(0.92f, 0.96f, 1.0f, 0.98f), shadow: true);
                y += 18.0f;
            }
        }
        else if (state.Mode == UiOverlayMode.InGame)
        {
            var modeBadge = state.IsSurvivalMode ? "[SURVIVAL]" : "[CREATIVE]";
            DrawText(v, $"{modeBadge} E: Inventory | F4: Mode | LMB: Attack/Mine | RMB: Use/Place | T: Chat", 12.0f, 12.0f, 1.35f, new Vector4(0.92f, 0.95f, 1.0f, 0.78f), shadow: true);
        }
    }

    private static void BuildProjectedNametags(
        List<TexturedUiVertex> v,
        FirstPersonCamera camera,
        float width,
        float height,
        IReadOnlyList<RemotePlayerAvatar> players)
    {
        if (players.Count == 0)
        {
            return;
        }

        var vp = camera.BuildCameraRelativeViewProjectionMatrix();
        for (var i = 0; i < players.Count; i++)
        {
            var p = players[i];
            var relHead = new Vector3(
                (float)(p.FeetPosition.X - camera.Position.X),
                (float)(p.FeetPosition.Y + 2.12 - camera.Position.Y),
                (float)(p.FeetPosition.Z - camera.Position.Z));

            var clip = Vector4.Transform(new Vector4(relHead, 1.0f), vp);
            if (clip.W <= 0.1f)
            {
                continue;
            }

            var ndcX = clip.X / clip.W;
            var ndcY = clip.Y / clip.W;
            if (MathF.Abs(ndcX) > 1.05f || MathF.Abs(ndcY) > 1.05f)
            {
                continue;
            }

            var sx = (ndcX + 1.0f) * 0.5f * width;
            var sy = (1.0f - ndcY) * 0.5f * height;
            var tw = MeasureText(p.Username, 1.6f);
            DrawRect(v, sx - (tw * 0.5f) - 5.0f, sy - 4.0f, tw + 10.0f, 18.0f, new Vector4(0.05f, 0.06f, 0.08f, 0.65f));
            DrawCenteredText(v, p.Username, sx, sy - 1.0f, 1.6f, new Vector4(1.0f, 1.0f, 1.0f, 0.98f), shadow: true);

            // PvP HP bar under friend's nametag when damaged
            if (p.Health < 19.9f)
            {
                const float hpW = 54.0f;
                DrawRect(v, sx - (hpW * 0.5f) - 1.0f, sy + 16.0f, hpW + 2.0f, 6.0f, new Vector4(0.06f, 0.06f, 0.08f, 0.85f));
                DrawRect(v, sx - (hpW * 0.5f), sy + 17.0f, hpW * Math.Clamp(p.Health / 20.0f, 0.0f, 1.0f), 4.0f, new Vector4(0.95f, 0.22f, 0.24f, 0.98f));
            }
        }
    }

    private static void DrawButtons(List<TexturedUiVertex> v, IReadOnlyList<UiButtonItem>? buttons)
    {
        if (buttons is null)
        {
            return;
        }

        for (var i = 0; i < buttons.Count; i++)
        {
            var b = buttons[i];
            if (b.IsInputField)
            {
                var border = b.IsFocused
                    ? new Vector4(0.95f, 0.88f, 0.35f, 1.0f)
                    : new Vector4(0.55f, 0.58f, 0.64f, 0.95f);
                DrawRect(v, b.X - 2.0f, b.Y - 2.0f, b.Width + 4.0f, b.Height + 4.0f, border);
                DrawRect(v, b.X, b.Y, b.Width, b.Height, new Vector4(0.06f, 0.07f, 0.09f, 0.96f));
                var display = b.IsFocused ? b.Label + "_" : b.Label;
                DrawText(v, display, b.X + 10.0f, b.Y + ((b.Height - 16.0f) * 0.5f), 2.0f, new Vector4(0.96f, 0.97f, 1.0f, 1.0f), shadow: true);
                continue;
            }

            var fill = ResolveButtonFillColor(in b);
            var hi = b.IsHovered
                ? new Vector4(0.72f, 0.78f, 0.98f, 1.0f)
                : new Vector4(0.56f, 0.58f, 0.62f, 0.96f);
            var lo = new Vector4(0.14f, 0.15f, 0.18f, 0.98f);

            DrawBeveledPanel(v, b.X, b.Y, b.Width, b.Height, fill, hi, lo);
            var scale = b.Height <= 32.0f ? 1.45f : 1.9f;
            var textCol = b.IsHovered
                ? new Vector4(1.0f, 0.96f, 0.55f, 1.0f)
                : new Vector4(0.95f, 0.96f, 0.98f, 1.0f);
            DrawCenteredText(v, b.Label, b.X + (b.Width * 0.5f), b.Y + ((b.Height - (8.0f * scale)) * 0.5f), scale, textCol, shadow: true);
        }
    }

    private static void DrawBeveledPanel(List<TexturedUiVertex> v, float x, float y, float w, float h, Vector4 fill, Vector4 hi, Vector4 lo)
    {
        DrawRect(v, x - 2.0f, y - 2.0f, w + 4.0f, h + 4.0f, new Vector4(0.04f, 0.04f, 0.05f, 0.96f));
        DrawRect(v, x, y, w, h, fill);
        DrawRect(v, x, y, w, 3.0f, hi);
        DrawRect(v, x, y, 3.0f, h, hi);
        DrawRect(v, x, y + h - 3.0f, w, 3.0f, lo);
        DrawRect(v, x + w - 3.0f, y, 3.0f, h, lo);
    }

    private static void DrawCenteredText(List<TexturedUiVertex> v, ReadOnlySpan<char> text, float centerX, float y, float scale, Vector4 color, bool shadow) =>
        DrawText(v, text, MathF.Round(centerX - (MeasureText(text, scale) * 0.5f)), MathF.Round(y), scale, color, shadow);

    private static void DrawText(List<TexturedUiVertex> v, ReadOnlySpan<char> text, float x, float y, float scale, Vector4 color, bool shadow)
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
        if (cellIndex == ' ')
        {
            return;
        }

        var col = cellIndex & 15;
        var row = cellIndex >> 4;
        var u0 = (col * 8.0f) / PixelFontGlyphs.AtlasWidth;
        var v0 = (row * 8.0f) / PixelFontGlyphs.AtlasHeight;
        var u1 = ((col + 1) * 8.0f) / PixelFontGlyphs.AtlasWidth;
        var v1 = ((row + 1) * 8.0f) / PixelFontGlyphs.AtlasHeight;
        EmitQuad(v, x, y, size, size, new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1), color);
    }

    private static void DrawRect(List<TexturedUiVertex> v, float x, float y, float w, float h, Vector4 color) =>
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
