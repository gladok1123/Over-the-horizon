using System.Globalization;
using System.Numerics;
using OverHorizon.Core.Items;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Crafting;
using OverHorizon.Rendering.Hud;

namespace OverHorizon.Client;

/// <summary>
/// Interactive menu &amp; overlay state machine powering the "OVER THE HORIZON — C# EDITION" Title Screen,
/// Multiplayer Connect/Host dialog, Options menu, Pause menu, Redesigned Tabbed Creative Catalog,
/// 36-Slot Survival Inventory &amp; Crafting, Death Screen, and live Chat bar.
/// </summary>
internal sealed class ClientMenuController
{
    private static readonly string[] SplashLines =
    [
        "100% Pure C# 13 & .NET 9!", "23w13a__or__b Textures!", "Survival, Crafting & PvP!",
        "Real Recorded OGG Foley!", "Play With Friends on TCP 25565!", "Authentic Water & Underwater!",
    ];

    private static readonly float[] FovPresets = [70.0f, 85.0f, 100.0f, 110.0f];
    private static readonly int[] ViewDistancePresets = [3, 5, 7, 9];
    private static readonly float[] VolumePresets = [1.0f, 0.5f, 0.0f];
    private static readonly float[] SensitivityPresets = [0.07f, 0.12f, 0.20f];

    private readonly IRegistry<ItemDefinition> _items;
    private readonly string _splashText;
    private UiOverlayMode _returnModeFromOptions = UiOverlayMode.TitleMenu;
    private int _fovIndex;
    private int _viewDistIndex;
    private int _volumeIndex;
    private int _sensitivityIndex = 1;
    private int _creativeTabIndex;
    private int _inventoryScrollRow;
    private int _selectedSurvivalSwapSlot = -1;

    public ClientMenuController(ClientOptions options, UiOverlayMode initialMode, IRegistry<ItemDefinition> items)
    {
        _items = items;
        Mode = initialMode;
        ServerEndpointInput = string.IsNullOrWhiteSpace(options.ServerHost)
            ? $"127.0.0.1:{options.ServerPort.ToString(CultureInfo.InvariantCulture)}"
            : $"{options.ServerHost}:{options.ServerPort.ToString(CultureInfo.InvariantCulture)}";
        UsernameInput = options.Username;
        ActiveViewDistance = Math.Clamp(options.ViewDistanceChunks, 1, 16);
        _viewDistIndex = Math.Max(0, Array.IndexOf(ViewDistancePresets, ActiveViewDistance));
        _splashText = SplashLines[(int)((uint)Environment.TickCount % (uint)SplashLines.Length)];
    }

    public UiOverlayMode Mode { get; set; }
    public bool IsGameplayActive => Mode == UiOverlayMode.InGame;
    public string ServerEndpointInput { get; set; }
    public string UsernameInput { get; set; }
    public int FocusedInputField { get; set; }
    public string ChatDraft { get; set; } = string.Empty;
    public string CreativeSearchQuery { get; set; } = string.Empty;
    public string StatusMessage { get; set; } = string.Empty;
    public int ActiveViewDistance { get; private set; }
    public Vector2 MousePosition { get; set; }
    public float PanoramaYawDegrees { get; set; }
    public float CloudDriftOffset { get; private set; }

    public void AdvanceAnimations(double dt, ClientPlayerController? player = null)
    {
        CloudDriftOffset = (CloudDriftOffset + (float)(dt * 1.4)) % 1024.0f;
        if (Mode == UiOverlayMode.TitleMenu)
        {
            PanoramaYawDegrees = (PanoramaYawDegrees + (float)(dt * 9.0)) % 360.0f;
        }

        if (player is not null && player.IsDead && Mode is not (UiOverlayMode.TitleMenu or UiOverlayMode.DeathScreen))
        {
            Mode = UiOverlayMode.DeathScreen;
        }
    }

    public void OpenOrCloseInventory(ClientPlayerController player)
    {
        if (Mode is UiOverlayMode.BlockInventory or UiOverlayMode.SurvivalInventory)
        {
            Mode = UiOverlayMode.InGame;
            _selectedSurvivalSwapSlot = -1;
            return;
        }

        if (Mode == UiOverlayMode.InGame)
        {
            _inventoryScrollRow = 0;
            _selectedSurvivalSwapSlot = -1;
            Mode = player.Mode == GameMode.Survival ? UiOverlayMode.SurvivalInventory : UiOverlayMode.BlockInventory;
        }
    }

    public void ScrollInventory(int wheelDelta)
    {
        if (wheelDelta == 0)
        {
            return;
        }

        _inventoryScrollRow = Math.Max(0, _inventoryScrollRow + (wheelDelta > 0 ? -1 : 1));
    }

    public bool TryAssignHoveredCreativeItemToSlot(Vector2 viewport, ClientPlayerController player, int hotbarSlot)
    {
        if (Mode != UiOverlayMode.BlockInventory)
        {
            return false;
        }

        var w = viewport.X > 64.0f ? viewport.X : 1280.0f;
        var h = viewport.Y > 64.0f ? viewport.Y : 720.0f;
        var buttons = new List<UiButtonItem>(12);
        var cells = new List<UiInventoryCell>(52);
        ClientInventoryScreenBuilder.BuildCreativeCatalog(w, h, MousePosition, player, _creativeTabIndex, CreativeSearchQuery, _inventoryScrollRow, buttons, cells);

        for (var i = 0; i < cells.Count; i++)
        {
            if (cells[i].IsHovered && cells[i].BlockIndex < ClientInventoryScreenBuilder.CreativeHotbarCellOffset)
            {
                player.Hotbar.EquipPaletteEntryInSlot(cells[i].BlockIndex, hotbarSlot);
                StatusMessage = $"Equipped {cells[i].DisplayName} in Slot {hotbarSlot + 1}!";
                return true;
            }
        }

        return false;
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

        if (Mode == UiOverlayMode.BlockInventory)
        {
            if (CreativeSearchQuery.Length < 22)
            {
                CreativeSearchQuery += ch;
                _inventoryScrollRow = 0;
            }

            return;
        }

        if (Mode != UiOverlayMode.MultiplayerMenu)
        {
            return;
        }

        if (FocusedInputField == 0 && ServerEndpointInput.Length < 42 && !char.IsWhiteSpace(ch))
        {
            ServerEndpointInput += ch;
        }
        else if (FocusedInputField == 1 && UsernameInput.Length < 16 && (char.IsLetterOrDigit(ch) || ch == '_'))
        {
            UsernameInput += ch;
        }
    }

    public void AppendPastedText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        for (var i = 0; i < text.Length; i++)
        {
            AppendTypedCharacter(text[i]);
        }
    }

    public void HandleBackspace()
    {
        if (Mode == UiOverlayMode.ChatInput && ChatDraft.Length > 0)
        {
            ChatDraft = ChatDraft[..^1];
        }
        else if (Mode == UiOverlayMode.BlockInventory && CreativeSearchQuery.Length > 0)
        {
            CreativeSearchQuery = CreativeSearchQuery[..^1];
            _inventoryScrollRow = 0;
        }
        else if (Mode == UiOverlayMode.MultiplayerMenu)
        {
            if (FocusedInputField == 0 && ServerEndpointInput.Length > 0)
            {
                ServerEndpointInput = ServerEndpointInput[..^1];
            }
            else if (FocusedInputField == 1 && UsernameInput.Length > 0)
            {
                UsernameInput = UsernameInput[..^1];
            }
        }
    }

    public string? HandleLeftClick(Vector2 mousePos, Vector2 viewport, ClientPlayerController player)
    {
        MousePosition = mousePos;
        var width = viewport.X > 64.0f ? viewport.X : 1280.0f;
        var height = viewport.Y > 64.0f ? viewport.Y : 720.0f;

        var buttons = new List<UiButtonItem>(12);
        var cells = new List<UiInventoryCell>(64);
        BuildScreenControls(width, height, player, buttons, cells);

        for (var i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            if (!c.IsHovered)
            {
                continue;
            }

            if (Mode == UiOverlayMode.BlockInventory)
            {
                if (c.BlockIndex >= ClientInventoryScreenBuilder.CreativeHotbarCellOffset)
                {
                    player.Hotbar.SelectSlot(c.BlockIndex - ClientInventoryScreenBuilder.CreativeHotbarCellOffset);
                    return "select_hotbar";
                }

                player.Hotbar.EquipPaletteBlock(c.BlockIndex);
                StatusMessage = $"Equipped {c.DisplayName} in Slot {player.Hotbar.SelectedSlot + 1}!";
                return "equip_creative";
            }

            if (Mode == UiOverlayMode.SurvivalInventory)
            {
                if (c.BlockIndex >= ClientInventoryScreenBuilder.SurvivalRecipeOffset)
                {
                    var recipeIdx = c.BlockIndex - ClientInventoryScreenBuilder.SurvivalRecipeOffset;
                    var recipe = QuickCraftCatalog.Entries[recipeIdx];
                    if (QuickCraftCatalog.TryCraft(player.Hotbar.SurvivalInventory, recipe, _items))
                    {
                        player.Hotbar.RefreshSlotVisuals(player.Mode);
                        StatusMessage = $"Crafted {recipe.Label}!";
                        return "crafted_item";
                    }

                    StatusMessage = $"Need more materials for {recipe.Label}!";
                    return "craft_failed";
                }

                if (_selectedSurvivalSwapSlot < 0)
                {
                    _selectedSurvivalSwapSlot = c.BlockIndex;
                }
                else
                {
                    var inv = player.Hotbar.SurvivalInventory;
                    var a = inv.GetSlot(_selectedSurvivalSwapSlot);
                    var b = inv.GetSlot(c.BlockIndex);
                    inv.SetSlot(_selectedSurvivalSwapSlot, b);
                    inv.SetSlot(c.BlockIndex, a);
                    _selectedSurvivalSwapSlot = -1;
                    player.Hotbar.RefreshSlotVisuals(player.Mode);
                }

                return "swap_slot";
            }
        }

        for (var i = 0; i < buttons.Count; i++)
        {
            if (buttons[i].IsHovered)
            {
                return ActivateAction(buttons[i].Id, player);
            }
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
        player.Hotbar.RefreshSlotVisuals(player.Mode);

        var (title, subtitle) = Mode switch
        {
            UiOverlayMode.TitleMenu => ("OVER THE HORIZON", "C# EDITION"),
            UiOverlayMode.MultiplayerMenu => ("PLAY WITH FRIENDS (PVP & CO-OP)", "Enter friend's IP:Port or host your own server"),
            UiOverlayMode.OptionsMenu => ("GAME OPTIONS", "Customize game mode, graphics, view distance, and audio"),
            UiOverlayMode.PauseMenu => ("GAME MENU", $"Mode: {player.Mode} — Press Esc to resume"),
            UiOverlayMode.BlockInventory => ("CREATIVE CATALOG", string.Empty),
            UiOverlayMode.SurvivalInventory => ("SURVIVAL INVENTORY & CRAFTING", "Left: Click 2 slots to swap | Right: One-click Craft & Smelt"),
            UiOverlayMode.DeathScreen => ("YOU DIED!", "Click Respawn to return to spawn with full health"),
            _ => (string.Empty, string.Empty),
        };

        List<UiButtonItem>? buttons = null;
        List<UiInventoryCell>? invCells = null;
        if (Mode is not (UiOverlayMode.InGame or UiOverlayMode.ChatInput))
        {
            buttons = new List<UiButtonItem>(12);
            invCells = new List<UiInventoryCell>(64);
            BuildScreenControls(width, height, player, buttons, invCells);
        }

        return new FrameOverlayState(
            TargetedBlock: Mode == UiOverlayMode.InGame ? player.CurrentTarget?.BlockPosition : null,
            SelectedHotbarSlot: player.Hotbar.SelectedSlot,
            HotbarSlotColors: player.Hotbar.ActiveSlotColors,
            RemotePlayers: remotePlayers)
        {
            Mode = Mode,
            ViewportSize = new Vector2(width, height),
            ActiveBlockDisplayName = player.Hotbar.SelectedBlockDisplayName,
            HeldBlockColor = player.Hotbar.SelectedBlockColor,
            HandSwingProgress = player.HandSwingProgress,
            WalkBobPhase = player.WalkBobPhase,
            CloudDriftOffset = CloudDriftOffset,
            DebugOverlayText = player.IsDebugOverlayVisible ? latestDebugText : string.Empty,
            RecentChatLines = chatHistory.GetVisibleLines(Mode == UiOverlayMode.ChatInput),
            ChatInputBuffer = ChatDraft,
            MenuTitle = title,
            MenuSubtitle = subtitle,
            SplashText = Mode == UiOverlayMode.TitleMenu ? _splashText : string.Empty,
            StatusMessage = StatusMessage,
            Buttons = buttons,
            InventoryCells = invCells,
            IsUnderwater = player.IsHeadUnderwater,
            IsSurvivalMode = player.Mode == GameMode.Survival,
            Health = player.Health,
            MaxHealth = 20.0f,
            Hunger = player.Hunger,
            AirSupplyNormalized = player.AirSupplyNormalized,
            MiningProgress = player.MiningProgress,
            HurtFlashStrength = player.HurtFlashStrength,
            HeldIsToolOrWeapon = player.Hotbar.SelectedIsToolOrWeapon,
            HeldIsEmpty = player.Hotbar.SelectedIsEmpty,
            HotbarSlotTiles = player.Hotbar.ActiveSlotTiles,
            HotbarSlotCounts = player.Hotbar.ActiveSlotCounts,
            CreativeSearchQuery = CreativeSearchQuery,
        };
    }

    private string ActivateAction(string id, ClientPlayerController player)
    {
        StatusMessage = string.Empty;
        if (id.StartsWith("tab_", StringComparison.Ordinal) && int.TryParse(id.AsSpan(4), out var tab))
        {
            _creativeTabIndex = tab;
            _inventoryScrollRow = 0;
            return id;
        }

        switch (id)
        {
            case "singleplayer":
                player.SetGameMode(GameMode.Creative);
                Mode = UiOverlayMode.InGame;
                return id;
            case "singleplayer_survival":
                player.SetGameMode(GameMode.Survival);
                Mode = UiOverlayMode.InGame;
                return "singleplayer";
            case "resume":
                Mode = UiOverlayMode.InGame;
                return id;
            case "respawn_player":
                player.Respawn();
                Mode = UiOverlayMode.InGame;
                return id;
            case "toggle_gamemode":
                player.SetGameMode(player.Mode == GameMode.Creative ? GameMode.Survival : GameMode.Creative);
                return id;
            case "switch_to_creative":
                player.SetGameMode(GameMode.Creative);
                Mode = UiOverlayMode.BlockInventory;
                return id;
            case "switch_to_survival":
                player.SetGameMode(GameMode.Survival);
                Mode = UiOverlayMode.SurvivalInventory;
                return id;
            case "scroll_up":
                ScrollInventory(1);
                return id;
            case "scroll_down":
                ScrollInventory(-1);
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
                Mode = player.Mode == GameMode.Survival ? UiOverlayMode.SurvivalInventory : UiOverlayMode.BlockInventory;
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

    private void BuildScreenControls(
        float width,
        float height,
        ClientPlayerController player,
        List<UiButtonItem> buttons,
        List<UiInventoryCell> cells)
    {
        if (Mode == UiOverlayMode.BlockInventory)
        {
            ClientInventoryScreenBuilder.BuildCreativeCatalog(width, height, MousePosition, player, _creativeTabIndex, CreativeSearchQuery, _inventoryScrollRow, buttons, cells);
            return;
        }

        if (Mode == UiOverlayMode.SurvivalInventory)
        {
            ClientInventoryScreenBuilder.BuildSurvivalInventoryAndCrafting(width, height, MousePosition, player, _items, _selectedSurvivalSwapSlot, _inventoryScrollRow, buttons, cells);
            return;
        }

        ClientInventoryScreenBuilder.BuildStandardMenuButtons(
            Mode,
            width,
            height,
            MousePosition,
            player,
            ServerEndpointInput,
            UsernameInput,
            FocusedInputField,
            FovPresets[_fovIndex],
            ActiveViewDistance,
            buttons);
    }
}
