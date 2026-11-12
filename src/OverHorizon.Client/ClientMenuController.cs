using System.Globalization;
using System.Numerics;
using OverHorizon.Rendering.Hud;

namespace OverHorizon.Client;

/// <summary>
/// Interactive menu &amp; overlay state machine powering the "OVER THE HORIZON — C# EDITION" Title Screen,
/// Multiplayer Connect/Host dialog, Options menu, Pause menu, 22-block Creative Inventory, and live Chat bar.
/// </summary>
internal sealed class ClientMenuController
{
    private static readonly string[] SplashLines =
    [
        "100% Pure C# 13 & .NET 9!",
        "Clean-Room Voxel Engine!",
        "Play With Friends on TCP 25565!",
        "Greedy-Meshed at 60+ FPS!",
        "Zero GC Spikes, Pure Fun!",
    ];

    private static readonly float[] FovPresets = [70.0f, 85.0f, 100.0f, 110.0f];
    private static readonly int[] ViewDistancePresets = [3, 5, 7, 9];
    private static readonly float[] VolumePresets = [1.0f, 0.5f, 0.0f];
    private static readonly float[] SensitivityPresets = [0.07f, 0.12f, 0.20f];

    private readonly string _splashText;
    private UiOverlayMode _returnModeFromOptions = UiOverlayMode.TitleMenu;
    private int _fovIndex;
    private int _viewDistIndex;
    private int _volumeIndex;
    private int _sensitivityIndex = 1;

    public ClientMenuController(ClientOptions options, UiOverlayMode initialMode)
    {
        Mode = initialMode;
        ServerEndpointInput = string.IsNullOrWhiteSpace(options.ServerHost)
            ? $"127.0.0.1:{options.ServerPort.ToString(CultureInfo.InvariantCulture)}"
            : $"{options.ServerHost}:{options.ServerPort.ToString(CultureInfo.InvariantCulture)}";
        UsernameInput = options.Username;
        ActiveViewDistance = Math.Clamp(options.ViewDistanceChunks, 1, 16);
        _viewDistIndex = Array.IndexOf(ViewDistancePresets, ActiveViewDistance);
        if (_viewDistIndex < 0)
        {
            _viewDistIndex = 0;
        }

        _splashText = SplashLines[(int)((uint)Environment.TickCount % (uint)SplashLines.Length)];
    }

    public UiOverlayMode Mode { get; set; }

    public bool IsGameplayActive => Mode == UiOverlayMode.InGame;

    public string ServerEndpointInput { get; set; }

    public string UsernameInput { get; set; }

    public int FocusedInputField { get; set; }

    public string ChatDraft { get; set; } = string.Empty;

    public string StatusMessage { get; set; } = string.Empty;

    public int ActiveViewDistance { get; private set; }

    public Vector2 MousePosition { get; set; }

    public float PanoramaYawDegrees { get; set; }

    public void AdvancePanorama(double dt)
    {
        if (Mode == UiOverlayMode.TitleMenu)
        {
            PanoramaYawDegrees = (PanoramaYawDegrees + (float)(dt * 9.0)) % 360.0f;
        }
    }

    public void AppendTypedCharacter(char ch)
    {
        if (char.IsControl(ch))
        {
            return;
        }

        if (Mode == UiOverlayMode.ChatInput)
        {
            if (ChatDraft.Length < 120)
            {
                ChatDraft += ch;
            }

            return;
        }

        if (Mode != UiOverlayMode.MultiplayerMenu)
        {
            return;
        }

        if (FocusedInputField == 0 && ServerEndpointInput.Length < 48 && !char.IsWhiteSpace(ch))
        {
            ServerEndpointInput += ch;
        }
        else if (FocusedInputField == 1 && UsernameInput.Length < 16 && (char.IsLetterOrDigit(ch) || ch is '_' or '-'))
        {
            UsernameInput += ch;
        }
    }

    public void AppendPastedText(string? clip)
    {
        if (string.IsNullOrEmpty(clip))
        {
            return;
        }

        for (var i = 0; i < clip.Length; i++)
        {
            AppendTypedCharacter(clip[i]);
        }
    }

    public void HandleBackspace()
    {
        if (Mode == UiOverlayMode.ChatInput && ChatDraft.Length > 0)
        {
            ChatDraft = ChatDraft[..^1];
            return;
        }

        if (Mode != UiOverlayMode.MultiplayerMenu)
        {
            return;
        }

        if (FocusedInputField == 0 && ServerEndpointInput.Length > 0)
        {
            ServerEndpointInput = ServerEndpointInput[..^1];
        }
        else if (FocusedInputField == 1 && UsernameInput.Length > 0)
        {
            UsernameInput = UsernameInput[..^1];
        }
    }

    public string? HandleLeftClick(
        Vector2 clickPos,
        Vector2 viewport,
        ClientPlayerController player)
    {
        MousePosition = clickPos;
        var width = viewport.X > 64.0f ? viewport.X : 1280.0f;
        var height = viewport.Y > 64.0f ? viewport.Y : 720.0f;

        if (Mode == UiOverlayMode.BlockInventory)
        {
            var cells = BuildInventoryCells(width, height);
            for (var i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                if (Contains(clickPos, c.X, c.Y, c.Size, c.Size))
                {
                    player.Hotbar.EquipPaletteBlock(c.BlockIndex);
                    Mode = UiOverlayMode.InGame;
                    return "equipped_block";
                }
            }
        }

        var buttons = BuildButtons(width, height, player);
        for (var i = 0; i < buttons.Count; i++)
        {
            var b = buttons[i];
            if (!Contains(clickPos, b.X, b.Y, b.Width, b.Height))
            {
                continue;
            }

            return ActivateAction(b.ActionId, player);
        }

        return null;
    }

    public FrameOverlayState BuildOverlayState(
        Vector2 viewport,
        ClientPlayerController player,
        IReadOnlyList<RemotePlayerAvatar> remotePlayers,
        ClientChatHistory chatHistory,
        string latestDebugText)
    {
        var width = viewport.X > 64.0f ? viewport.X : 1280.0f;
        var height = viewport.Y > 64.0f ? viewport.Y : 720.0f;

        var (title, subtitle) = Mode switch
        {
            UiOverlayMode.TitleMenu => ("OVER THE HORIZON", "C# EDITION"),
            UiOverlayMode.MultiplayerMenu => ("PLAY WITH FRIENDS", "Enter friend's IP:Port or host your own server"),
            UiOverlayMode.OptionsMenu => ("GAME OPTIONS", "Customize graphics, view distance, and controls"),
            UiOverlayMode.PauseMenu => ("GAME MENU", "Press Esc to resume"),
            UiOverlayMode.BlockInventory => ("CREATIVE BLOCK PALETTE", $"Equipping into Hotbar Slot {player.Hotbar.SelectedSlot + 1}"),
            _ => (string.Empty, string.Empty),
        };

        var buttons = Mode is UiOverlayMode.InGame or UiOverlayMode.ChatInput
            ? null
            : BuildButtons(width, height, player);

        var invCells = Mode == UiOverlayMode.BlockInventory
            ? BuildInventoryCells(width, height)
            : null;

        return new FrameOverlayState(
            TargetedBlock: Mode == UiOverlayMode.InGame ? player.CurrentTarget?.BlockPosition : null,
            SelectedHotbarSlot: player.Hotbar.SelectedSlot,
            HotbarSlotColors: player.Hotbar.ActiveSlotColors,
            RemotePlayers: remotePlayers)
        {
            Mode = Mode,
            ViewportSize = new Vector2(width, height),
            ActiveBlockDisplayName = player.Hotbar.SelectedBlockDisplayName,
            ActiveBlockColor = player.Hotbar.SelectedBlockColor,
            HandSwingProgress = player.HandSwingProgress,
            WalkBobPhase = player.WalkBobPhase,
            DebugOverlayText = player.IsDebugOverlayVisible ? latestDebugText : null,
            RecentChatLines = chatHistory.GetVisibleLines(Mode == UiOverlayMode.ChatInput),
            ChatInputBuffer = ChatDraft,
            MenuTitle = title,
            MenuSubtitle = subtitle,
            SplashText = Mode == UiOverlayMode.TitleMenu ? _splashText : string.Empty,
            StatusMessage = StatusMessage,
            Buttons = buttons,
            InventoryCells = invCells,
        };
    }

    private static bool Contains(Vector2 pt, float x, float y, float w, float h) =>
        pt.X >= x && pt.X <= x + w && pt.Y >= y && pt.Y <= y + h;

    private string? ActivateAction(string id, ClientPlayerController player)
    {
        StatusMessage = string.Empty;
        switch (id)
        {
            case "singleplayer":
            case "resume":
                Mode = UiOverlayMode.InGame;
                return id;

            case "open_multiplayer":
                Mode = UiOverlayMode.MultiplayerMenu;
                return id;

            case "open_options":
                _returnModeFromOptions = Mode;
                Mode = UiOverlayMode.OptionsMenu;
                return id;

            case "options_done":
                Mode = _returnModeFromOptions;
                return id;

            case "open_inventory":
                Mode = UiOverlayMode.BlockInventory;
                return id;

            case "back_to_title":
                Mode = UiOverlayMode.TitleMenu;
                return id;

            case "focus_endpoint":
                FocusedInputField = 0;
                return id;

            case "focus_username":
                FocusedInputField = 1;
                return id;

            case "cycle_fov":
                _fovIndex = (_fovIndex + 1) % FovPresets.Length;
                player.Camera.FieldOfViewDegrees = FovPresets[_fovIndex];
                return id;

            case "cycle_view_dist":
                _viewDistIndex = (_viewDistIndex + 1) % ViewDistancePresets.Length;
                ActiveViewDistance = ViewDistancePresets[_viewDistIndex];
                return id;

            case "cycle_volume":
                _volumeIndex = (_volumeIndex + 1) % VolumePresets.Length;
                player.MasterVolume = VolumePresets[_volumeIndex];
                return id;

            case "cycle_sensitivity":
                _sensitivityIndex = (_sensitivityIndex + 1) % SensitivityPresets.Length;
                player.MouseSensitivity = SensitivityPresets[_sensitivityIndex];
                return id;

            default:
                return id;
        }
    }

    private List<UiButtonItem> BuildButtons(float width, float height, ClientPlayerController player)
    {
        var list = new List<UiButtonItem>(6);
        var cx = width * 0.5f;
        const float bw = 420.0f;
        const float bh = 44.0f;

        void AddBtn(string id, string label, float x, float y, float w = bw, bool isInput = false, bool focused = false)
        {
            var hovered = Contains(MousePosition, x, y, w, bh);
            list.Add(new UiButtonItem(id, label, x, y, w, bh, hovered, isInput, focused));
        }

        var bx = cx - (bw * 0.5f);
        switch (Mode)
        {
            case UiOverlayMode.TitleMenu:
                var ty = MathF.Max(210.0f, height * 0.40f);
                AddBtn("singleplayer", "Singleplayer", bx, ty);
                AddBtn("open_multiplayer", "Multiplayer (Play With Friends)", bx, ty + 56.0f);
                AddBtn("open_options", "Options...", bx, ty + 112.0f, 204.0f);
                AddBtn("quit_game", "Quit Game", bx + 216.0f, ty + 112.0f, 204.0f);
                break;

            case UiOverlayMode.MultiplayerMenu:
                var my = MathF.Max(165.0f, height * 0.28f);
                AddBtn("focus_endpoint", "Server: " + ServerEndpointInput, bx, my, bw, isInput: true, focused: FocusedInputField == 0);
                AddBtn("focus_username", "Name:   " + UsernameInput, bx, my + 56.0f, bw, isInput: true, focused: FocusedInputField == 1);
                AddBtn("join_server", "Join Friend's Server", bx, my + 122.0f);
                AddBtn("host_and_join", "Host Local Server (25565) & Play", bx, my + 178.0f);
                AddBtn("back_to_title", "Back to Title Screen", bx, my + 242.0f);
                break;

            case UiOverlayMode.OptionsMenu:
                var oy = MathF.Max(175.0f, height * 0.29f);
                AddBtn("cycle_fov", $"FOV: {FovPresets[_fovIndex]:F0} deg", bx, oy);
                AddBtn("cycle_view_dist", $"Render Distance: {ActiveViewDistance} chunks", bx, oy + 56.0f);
                AddBtn("cycle_volume", $"Sound Volume: {(int)(player.MasterVolume * 100.0f)}%", bx, oy + 112.0f);
                AddBtn("cycle_sensitivity", $"Mouse Sensitivity: {(int)(player.MouseSensitivity * 1000.0f)}", bx, oy + 168.0f);
                AddBtn("options_done", "Done", bx, oy + 232.0f);
                break;

            case UiOverlayMode.PauseMenu:
                var py = MathF.Max(190.0f, height * 0.32f);
                AddBtn("resume", "Back to Game", bx, py);
                AddBtn("open_inventory", "Creative Block Palette (E)", bx, py + 56.0f);
                AddBtn("open_options", "Options...", bx, py + 112.0f);
                AddBtn("save_and_title", "Save & Quit to Title", bx, py + 176.0f);
                break;

            case UiOverlayMode.BlockInventory:
                AddBtn("resume", "Done (Esc / E)", cx - 130.0f, height - 76.0f, 260.0f);
                break;
        }

        return list;
    }

    private List<UiInventoryCell> BuildInventoryCells(float width, float height)
    {
        const int cols = 6;
        const float cellSize = 56.0f;
        const float gap = 12.0f;
        var count = ClientHotbar.PaletteCount;
        var rows = (count + cols - 1) / cols;
        var gridW = (cols * cellSize) + ((cols - 1) * gap);
        var startX = (width - gridW) * 0.5f;
        var startY = MathF.Max(150.0f, (height - (rows * (cellSize + gap))) * 0.45f);

        var cells = new List<UiInventoryCell>(count);
        for (var i = 0; i < count; i++)
        {
            var c = i % cols;
            var r = i / cols;
            var x = startX + (c * (cellSize + gap));
            var y = startY + (r * (cellSize + gap));
            var (_, name, color) = ClientHotbar.GetPaletteEntry(i);
            cells.Add(new UiInventoryCell(i, name, color, x, y, cellSize, Contains(MousePosition, x, y, cellSize, cellSize)));
        }

        return cells;
    }
}
