using System.Globalization;
using System.Numerics;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Hud;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Pure CPU screen-space UI &amp; bitmap-font triangle batch builder for the "C# EDITION" Title Screen,
/// Multiplayer/Options/Pause modals, 22-block Creative Inventory, F3 overlay, live Chat, Hotbar, and 3D Nametags.
/// </summary>
public static class UiGeometryBuilder
{
    private static readonly Vector2 WhiteUv = new(124.0f / 128.0f, 60.0f / 64.0f);

    /// <summary>
    /// Populates <paramref name="vertices"/> with screen-space UI triangles for <paramref name="state"/>.
    /// </summary>
    /// <param name="vertices">Destination vertex buffer cleared and filled in-place.</param>
    /// <param name="camera">Active first-person camera used to project 3D friend nametags.</param>
    /// <param name="width">Viewport width in pixels.</param>
    /// <param name="height">Viewport height in pixels.</param>
    /// <param name="state">Current frame HUD and menu state.</param>
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

        BuildProjectedNametags(vertices, camera, width, height, state.RemotePlayers);
        BuildInGameHud(vertices, width, height, state);

        if (state.Mode != UiOverlayMode.InGame)
        {
            BuildModalOverlay(vertices, width, height, state);
        }
    }

    private static float MeasureText(ReadOnlySpan<char> text, float scale) =>
        text.Length * 8.0f * scale;

    private static void BuildTitleScreen(List<TexturedUiVertex> v, float width, float height, FrameOverlayState state)
    {
        DrawRect(v, 0, 0, width, height, new Vector4(0.05f, 0.07f, 0.12f, 0.34f));

        var cx = width * 0.5f;
        var bannerY = MathF.Max(46.0f, height * 0.12f);

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

        DrawText(v, "Over-the-Horizon v0.3.0-alpha (C# / .NET 9)", 14.0f, height - 24.0f, 1.5f, new Vector4(0.88f, 0.90f, 0.94f, 0.90f), shadow: true);
        const string rightNote = "Clean-Room Engine - Not affiliated with Mojang";
        DrawText(v, rightNote, width - MeasureText(rightNote, 1.5f) - 14.0f, height - 24.0f, 1.5f, new Vector4(0.82f, 0.84f, 0.88f, 0.85f), shadow: true);
    }

    private static void BuildModalOverlay(List<TexturedUiVertex> v, float width, float height, FrameOverlayState state)
    {
        if (state.Mode == UiOverlayMode.ChatInput)
        {
            var barY = height - 112.0f;
            DrawRect(v, 18.0f, barY, width - 36.0f, 30.0f, new Vector4(0.04f, 0.05f, 0.07f, 0.82f));
            DrawText(v, "> " + state.ChatInputBuffer + "_", 26.0f, barY + 8.0f, 1.8f, new Vector4(1.0f, 1.0f, 1.0f, 0.98f), shadow: true);
            return;
        }

        DrawRect(v, 0, 0, width, height, new Vector4(0.04f, 0.05f, 0.08f, 0.68f));
        var cx = width * 0.5f;

        if (!string.IsNullOrEmpty(state.MenuTitle))
        {
            DrawCenteredText(v, state.MenuTitle, cx, MathF.Max(48.0f, height * 0.13f), 3.0f, new Vector4(1.0f, 1.0f, 1.0f, 1.0f), shadow: true);
        }

        if (!string.IsNullOrEmpty(state.MenuSubtitle))
        {
            DrawCenteredText(v, state.MenuSubtitle, cx, MathF.Max(84.0f, height * 0.19f), 1.7f, new Vector4(0.75f, 0.84f, 0.95f, 0.95f), shadow: true);
        }

        if (state.Mode == UiOverlayMode.BlockInventory && state.InventoryCells is { Count: > 0 } cells)
        {
            BuildInventoryGrid(v, cx, height, cells);
        }

        DrawButtons(v, state.Buttons);

        if (!string.IsNullOrEmpty(state.StatusMessage))
        {
            DrawCenteredText(v, state.StatusMessage, cx, height - 68.0f, 1.7f, new Vector4(1.0f, 0.82f, 0.36f, 1.0f), shadow: true);
        }
    }

    private static void BuildInventoryGrid(List<TexturedUiVertex> v, float cx, float height, IReadOnlyList<UiInventoryCell> cells)
    {
        string? hoveredLabel = null;
        for (var i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            var borderCol = cell.IsHovered
                ? new Vector4(1.0f, 0.90f, 0.28f, 1.0f)
                : new Vector4(0.22f, 0.24f, 0.28f, 0.95f);
            DrawRect(v, cell.X - 3.0f, cell.Y - 3.0f, cell.Size + 6.0f, cell.Size + 6.0f, borderCol);
            DrawRect(v, cell.X, cell.Y, cell.Size, cell.Size, new Vector4(0.12f, 0.13f, 0.16f, 0.95f));
            DrawRect(v, cell.X + 6.0f, cell.Y + 6.0f, cell.Size - 12.0f, cell.Size - 12.0f, new Vector4(cell.Color.X, cell.Color.Y, cell.Color.Z, 1.0f));

            if (cell.IsHovered)
            {
                hoveredLabel = cell.DisplayName;
            }
        }

        var tip = hoveredLabel ?? "Click any block to equip into active hotbar slot";
        DrawCenteredText(v, tip, cx, height - 120.0f, 2.0f, new Vector4(1.0f, 0.95f, 0.65f, 1.0f), shadow: true);
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
        if (!colors.IsEmpty)
        {
            var count = Math.Min(9, colors.Length);
            const float slotSize = 48.0f;
            const float gap = 6.0f;
            var totalW = (count * slotSize) + ((count - 1) * gap);
            var startX = cx - (totalW * 0.5f);
            var barY = height - slotSize - 14.0f;

            DrawBeveledPanel(
                v,
                startX - 8.0f,
                barY - 6.0f,
                totalW + 16.0f,
                slotSize + 12.0f,
                new Vector4(0.11f, 0.12f, 0.15f, 0.82f),
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
                var c = colors[i];
                DrawRect(v, sx + 7.0f, barY + 7.0f, slotSize - 14.0f, slotSize - 14.0f, new Vector4(c.X, c.Y, c.Z, 1.0f));
                DrawText(v, (i + 1).ToString(CultureInfo.InvariantCulture), sx + 3.0f, barY + 3.0f, 1.25f, new Vector4(0.92f, 0.92f, 0.95f, 0.90f), shadow: true);
            }

            if (!string.IsNullOrEmpty(state.ActiveBlockDisplayName))
            {
                DrawCenteredText(v, state.ActiveBlockDisplayName, cx, barY - 24.0f, 1.75f, new Vector4(1.0f, 0.96f, 0.78f, 0.95f), shadow: true);
            }
        }

        if (state.RecentChatLines is { Count: > 0 } chatLines)
        {
            var baseChatY = height - 148.0f;
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
            DrawText(v, "E: Blocks | T: Chat | F3: Debug | Esc: Menu", 12.0f, 12.0f, 1.35f, new Vector4(0.92f, 0.95f, 1.0f, 0.72f), shadow: true);
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
                (float)(p.FeetPosition.Y + 2.08 - camera.Position.Y),
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
            DrawRect(v, sx - (tw * 0.5f) - 5.0f, sy - 4.0f, tw + 10.0f, 18.0f, new Vector4(0.05f, 0.06f, 0.08f, 0.62f));
            DrawCenteredText(v, p.Username, sx, sy - 1.0f, 1.6f, new Vector4(1.0f, 1.0f, 1.0f, 0.98f), shadow: true);
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

            var fill = b.IsHovered
                ? new Vector4(0.42f, 0.48f, 0.66f, 0.96f)
                : new Vector4(0.32f, 0.34f, 0.38f, 0.95f);
            var hi = b.IsHovered
                ? new Vector4(0.72f, 0.78f, 0.98f, 1.0f)
                : new Vector4(0.56f, 0.58f, 0.62f, 0.96f);
            var lo = new Vector4(0.14f, 0.15f, 0.18f, 0.98f);

            DrawBeveledPanel(v, b.X, b.Y, b.Width, b.Height, fill, hi, lo);
            var textCol = b.IsHovered
                ? new Vector4(1.0f, 0.96f, 0.55f, 1.0f)
                : new Vector4(0.95f, 0.96f, 0.98f, 1.0f);
            DrawCenteredText(v, b.Label, b.X + (b.Width * 0.5f), b.Y + ((b.Height - 16.0f) * 0.5f), 2.0f, textCol, shadow: true);
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
            var shadowOffset = MathF.Max(1.0f, scale);
            var shadowCol = new Vector4(0.08f, 0.08f, 0.10f, color.W * 0.92f);
            for (var i = 0; i < text.Length; i++)
            {
                EmitGlyphQuad(v, PixelFontGlyphs.MapCharToCell(text[i]), x + (i * step) + shadowOffset, y + shadowOffset, step, shadowCol);
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

        var p0 = new Vector2(x, y);
        var p1 = new Vector2(x + size, y);
        var p2 = new Vector2(x + size, y + size);
        var p3 = new Vector2(x, y + size);

        v.Add(new TexturedUiVertex(p0, new Vector2(u0, v0), color));
        v.Add(new TexturedUiVertex(p1, new Vector2(u1, v0), color));
        v.Add(new TexturedUiVertex(p2, new Vector2(u1, v1), color));
        v.Add(new TexturedUiVertex(p0, new Vector2(u0, v0), color));
        v.Add(new TexturedUiVertex(p2, new Vector2(u1, v1), color));
        v.Add(new TexturedUiVertex(p3, new Vector2(u0, v1), color));
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
