using System.Globalization;
using System.Numerics;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Crafting;
using OverHorizon.Gameplay.Inventory;
using OverHorizon.Rendering.Hud;
using OverHorizon.World.Lighting;

namespace OverHorizon.Client;

/// <summary>
/// Interactive menu &amp; container screen controller powering the Title Screen, <c>2..64</c>-chunk Render Distance slider,
/// Creative Catalog, real <c>2x2</c> Survival Inventory, real <c>3x3</c> Crafting Table, real <c>3</c>-slot Furnace smelting,
/// and <c>0..15</c> dynamic hand &amp; torch lighting.
/// </summary>
internal sealed class ClientMenuController
{
    private static readonly string[] SplashLines =
    [
        "Up to 64-Chunk Render Distance!", "23w13a__or__b Real GUI & Items!", "0-15 Dynamic Torch Lighting!",
        "3x3 Crafting Table & Furnace!", "3D Peaceful & Hostile Mobs!", "Double-Tap Space to Fly!",
    ];

    private readonly IRegistry<ItemDefinition> _items;
    private readonly IRegistry<ICraftingRecipe> _recipes;
    private readonly ClientContainerSlotController _containers;
    private readonly string _splashText;

    private UiOverlayMode _returnModeFromOptions = UiOverlayMode.TitleMenu;
    private string? _draggingSliderId;
    private float _fovDegrees = 78.0f;
    private int _creativeTabIndex;
    private int _inventoryScrollRow;

    public ClientMenuController(
        ClientOptions options,
        UiOverlayMode initialMode,
        IRegistry<ItemDefinition> items,
        IRegistry<ICraftingRecipe>? recipes = null)
    {
        _items = items;
        _recipes = recipes ?? BuiltInRecipes.CreateFrozen(items);
        _containers = new ClientContainerSlotController(_items, _recipes);
        Mode = initialMode;
        ServerEndpointInput = string.IsNullOrWhiteSpace(options.ServerHost)
            ? $"127.0.0.1:{options.ServerPort.ToString(CultureInfo.InvariantCulture)}"
            : $"{options.ServerHost}:{options.ServerPort.ToString(CultureInfo.InvariantCulture)}";
        UsernameInput = options.Username;
        ActiveViewDistance = Math.Clamp(options.ViewDistanceChunks, 1, 64);
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

    public void RemoveFurnaceAndDropContents(BlockPos pos, PlayerInventory inv)
    {
        if (_containers.RemoveFurnaceAndDropContents(pos, inv) && Mode == UiOverlayMode.FurnaceScreen)
        {
            Mode = UiOverlayMode.InGame;
        }
    }

    public void OpenContainerScreen(UiOverlayMode containerMode, BlockPos? blockPos)
    {
        _containers.OpenFurnace(containerMode == UiOverlayMode.FurnaceScreen ? blockPos : null);
        StatusMessage = string.Empty;
        Mode = containerMode;
    }

    public void AdvanceAnimations(double dt, ClientPlayerController? player = null)
    {
        CloudDriftOffset = (CloudDriftOffset + (float)(dt * 1.4)) % 1024.0f;
        if (Mode == UiOverlayMode.TitleMenu) PanoramaYawDegrees = (PanoramaYawDegrees + (float)(dt * 9.0)) % 360.0f;
        _containers.TickFurnaces((float)dt);

        if (player is not null && player.IsDead && Mode is not (UiOverlayMode.TitleMenu or UiOverlayMode.DeathScreen))
        {
            CloseContainerAndRefundCursor(player);
            Mode = UiOverlayMode.DeathScreen;
        }
    }

    public void OpenOrCloseInventory(ClientPlayerController player)
    {
        if (Mode is UiOverlayMode.BlockInventory or UiOverlayMode.SurvivalInventory or UiOverlayMode.CraftingTableScreen or UiOverlayMode.FurnaceScreen)
        {
            CloseContainerAndRefundCursor(player);
            Mode = UiOverlayMode.InGame;
            return;
        }

        if (Mode == UiOverlayMode.InGame)
        {
            _inventoryScrollRow = 0;
            StatusMessage = string.Empty;
            Mode = player.Mode == GameMode.Survival ? UiOverlayMode.SurvivalInventory : UiOverlayMode.BlockInventory;
        }
    }

    public void CloseContainerAndRefundCursor(ClientPlayerController player)
    {
        _containers.RefundCursorAndGrids(player);
        _draggingSliderId = null;
    }

    public void ScrollInventory(int wheelDelta)
    {
        if (wheelDelta != 0) _inventoryScrollRow = Math.Max(0, _inventoryScrollRow + (wheelDelta > 0 ? -1 : 1));
    }

    public bool TryAssignHoveredCreativeItemToSlot(Vector2 viewport, ClientPlayerController player, int hotbarSlot)
    {
        if (Mode != UiOverlayMode.BlockInventory) return false;
        var w = viewport.X > 64.0f ? viewport.X : 1280.0f;
        var h = viewport.Y > 64.0f ? viewport.Y : 720.0f;
        var buttons = new List<UiButtonItem>(12);
        var cells = new List<UiInventoryCell>(64);
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
        if (char.IsControl(ch)) return;
        if (Mode == UiOverlayMode.ChatInput && ChatDraft.Length < 120) ChatDraft += ch;
        else if (Mode == UiOverlayMode.BlockInventory && CreativeSearchQuery.Length < 22)
        {
            CreativeSearchQuery += ch;
            _inventoryScrollRow = 0;
        }
        else if (Mode == UiOverlayMode.MultiplayerMenu)
        {
            if (FocusedInputField == 0 && ServerEndpointInput.Length < 42 && !char.IsWhiteSpace(ch)) ServerEndpointInput += ch;
            else if (FocusedInputField == 1 && UsernameInput.Length < 16 && (char.IsLetterOrDigit(ch) || ch == '_')) UsernameInput += ch;
        }
    }

    public void AppendPastedText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        for (var i = 0; i < text.Length; i++) AppendTypedCharacter(text[i]);
    }

    public void HandleBackspace()
    {
        if (Mode == UiOverlayMode.ChatInput && ChatDraft.Length > 0) ChatDraft = ChatDraft[..^1];
        else if (Mode == UiOverlayMode.BlockInventory && CreativeSearchQuery.Length > 0)
        {
            CreativeSearchQuery = CreativeSearchQuery[..^1];
            _inventoryScrollRow = 0;
        }
        else if (Mode == UiOverlayMode.MultiplayerMenu)
        {
            if (FocusedInputField == 0 && ServerEndpointInput.Length > 0) ServerEndpointInput = ServerEndpointInput[..^1];
            else if (FocusedInputField == 1 && UsernameInput.Length > 0) UsernameInput = UsernameInput[..^1];
        }
    }

    public void ReleaseSliderDrag() => _draggingSliderId = null;

    public void HandleSliderDrag(Vector2 mousePos, Vector2 viewport, ClientPlayerController player)
    {
        MousePosition = mousePos;
        if (_draggingSliderId is null || Mode != UiOverlayMode.OptionsMenu) return;
        var width = viewport.X > 64.0f ? viewport.X : 1280.0f;
        var height = viewport.Y > 64.0f ? viewport.Y : 720.0f;
        var buttons = new List<UiButtonItem>(8);
        ClientInventoryScreenBuilder.BuildStandardMenuButtons(Mode, width, height, mousePos, player, ServerEndpointInput, UsernameInput, FocusedInputField, _fovDegrees, ActiveViewDistance, buttons);
        for (var i = 0; i < buttons.Count; i++)
        {
            if (buttons[i].ActionId == _draggingSliderId)
            {
                ApplySliderAtX(buttons[i], mousePos.X, player);
                return;
            }
        }
    }

    public string? HandleContainerOrMenuClick(Vector2 mousePos, Vector2 viewport, ClientPlayerController player, bool isRightClick)
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
            if (!c.IsHovered) continue;
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

            if (Mode is UiOverlayMode.SurvivalInventory or UiOverlayMode.CraftingTableScreen or UiOverlayMode.FurnaceScreen)
            {
                return _containers.HandleCellClick(Mode, c.BlockIndex, isRightClick, player);
            }
        }

        if (isRightClick) return null;
        for (var i = 0; i < buttons.Count; i++)
        {
            var b = buttons[i];
            if (!b.IsHovered) continue;
            if (b.IsSlider)
            {
                _draggingSliderId = b.ActionId;
                ApplySliderAtX(b, mousePos.X, player);
                return b.ActionId;
            }

            return ActivateAction(b.ActionId, player);
        }

        return null;
    }

    public FrameOverlayState BuildOverlayState(
        Vector2 viewport,
        ClientPlayerController player,
        ClientWorldCache worldCache,
        IReadOnlyList<RemotePlayerAvatar> remotePlayers,
        IReadOnlyList<WorldMobVisual> worldMobs,
        ClientChatHistory chatHistory,
        string latestDebugText)
    {
        var width = viewport.X > 64.0f ? viewport.X : 1280.0f;
        var height = viewport.Y > 64.0f ? viewport.Y : 720.0f;
        player.Hotbar.RefreshSlotVisuals(player.Mode);
        player.Camera.FarPlane = Math.Max(160.0f, (ActiveViewDistance + 3) * 16.0f);

        var (title, subtitle) = Mode switch
        {
            UiOverlayMode.TitleMenu => ("OVER THE HORIZON", "C# EDITION"),
            UiOverlayMode.MultiplayerMenu => ("PLAY WITH FRIENDS (PVP & CO-OP)", "Enter friend's IP:Port or host your own server"),
            UiOverlayMode.OptionsMenu => ("GAME OPTIONS", "Drag sliders for Render Distance (2-64 Chunks), FOV, Volume & Sensitivity"),
            UiOverlayMode.PauseMenu => ("GAME MENU", $"Mode: {player.Mode} — Press Esc to resume"),
            UiOverlayMode.BlockInventory => ("CREATIVE CATALOG", string.Empty),
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

        var (eyeSky, eyeBlock) = worldCache.GetLight(player.Camera.Position.ToBlockPos());
        var furnaceBoost = _containers.ComputeNearbyLitFurnaceLight(player.Camera.Position);
        var heldEmit = player.Hotbar.SelectedHeldLightEmission;
        var effLevel = LightingEngine.ComputeEffectiveLightLevel(eyeSky, Math.Max(eyeBlock, furnaceBoost), heldEmit);
        var handBright = LightingEngine.LightLevelToBrightness(effLevel);

        var activeFurnace = _containers.ActiveFurnace;
        var curStack = _containers.CursorStack;
        var cursorTile = curStack.IsEmpty ? -1 : player.Hotbar.ResolveAtlasTile(_items.GetById(curStack.ItemId).Key);

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
            IsCreativeFlying = player.IsFlying,
            Health = player.Health,
            MaxHealth = 20.0f,
            ArmorDefensePoints = player.Hotbar.TotalArmorDefense,
            Hunger = player.Hunger,
            AirSupplyNormalized = player.AirSupplyNormalized,
            MiningProgress = player.MiningProgress,
            HurtFlashStrength = player.HurtFlashStrength,
            HeldIsToolOrWeapon = player.Hotbar.SelectedIsToolOrWeapon,
            HeldIsEmpty = player.Hotbar.SelectedIsEmpty,
            HeldIsBlock3D = player.Hotbar.SelectedIsBlock3D,
            HeldIsTorchStyle = player.Hotbar.SelectedIsTorchStyle,
            HeldTopTile = player.Hotbar.SelectedTopTile,
            HeldSideTile = player.Hotbar.SelectedSideTile,
            HeldFrontTile = player.Hotbar.SelectedFrontTile,
            HeldBottomTile = player.Hotbar.SelectedBottomTile,
            HeldItemSpriteTile = player.Hotbar.SelectedItemTile,
            HeldLightBrightness = handBright,
            HeldTorchLightLevel = Math.Max(heldEmit, furnaceBoost),
            CursorScreenPos = MousePosition,
            CursorItemTile = cursorTile,
            CursorItemCount = curStack.Count,
            FurnaceBurnNormalized = activeFurnace?.NormalizedBurnRemaining ?? 0.0f,
            FurnaceCookNormalized = activeFurnace?.NormalizedCookProgress ?? 0.0f,
            WorldMobs = worldMobs,
            HotbarSlotTiles = player.Hotbar.ActiveSlotTiles,
            HotbarSlotCounts = player.Hotbar.ActiveSlotCounts,
            CreativeSearchQuery = CreativeSearchQuery,
        };
    }

    private void ApplySliderAtX(in UiButtonItem b, float mouseX, ClientPlayerController player)
    {
        var t = Math.Clamp((mouseX - (b.X + 8.0f)) / Math.Max(1.0f, b.Width - 16.0f), 0.0f, 1.0f);
        switch (b.ActionId)
        {
            case "slider_view_dist": ActiveViewDistance = Math.Clamp((int)MathF.Round(2.0f + (t * 62.0f)), 2, 64); break;
            case "slider_fov": _fovDegrees = MathF.Round(70.0f + (t * 40.0f)); player.Camera.FieldOfViewDegrees = _fovDegrees; break;
            case "slider_volume": player.MasterVolume = MathF.Round(t * 100.0f) / 100.0f; break;
            case "slider_sensitivity": player.MouseSensitivity = 0.03f + (t * 0.22f); break;
        }
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
            case "singleplayer": player.SetGameMode(GameMode.Creative); Mode = UiOverlayMode.InGame; return id;
            case "singleplayer_survival": player.SetGameMode(GameMode.Survival); Mode = UiOverlayMode.InGame; return "singleplayer";
            case "resume": CloseContainerAndRefundCursor(player); Mode = UiOverlayMode.InGame; return id;
            case "respawn_player": player.Respawn(); Mode = UiOverlayMode.InGame; return id;
            case "toggle_gamemode": player.SetGameMode(player.Mode == GameMode.Creative ? GameMode.Survival : GameMode.Creative); return id;
            case "switch_to_creative": CloseContainerAndRefundCursor(player); player.SetGameMode(GameMode.Creative); Mode = UiOverlayMode.BlockInventory; return id;
            case "switch_to_survival": player.SetGameMode(GameMode.Survival); Mode = UiOverlayMode.SurvivalInventory; return id;
            case "scroll_up": ScrollInventory(1); return id;
            case "scroll_down": ScrollInventory(-1); return id;
            case "open_multiplayer": Mode = UiOverlayMode.MultiplayerMenu; return id;
            case "open_options": _returnModeFromOptions = Mode; Mode = UiOverlayMode.OptionsMenu; return id;
            case "options_done": Mode = _returnModeFromOptions; return id;
            case "open_inventory": Mode = player.Mode == GameMode.Survival ? UiOverlayMode.SurvivalInventory : UiOverlayMode.BlockInventory; return id;
            case "back_to_title": Mode = UiOverlayMode.TitleMenu; return id;
            case "focus_endpoint": FocusedInputField = 0; return id;
            case "focus_username": FocusedInputField = 1; return id;
            default: return id;
        }
    }

    private void BuildScreenControls(float width, float height, ClientPlayerController player, List<UiButtonItem> buttons, List<UiInventoryCell> cells)
    {
        if (Mode == UiOverlayMode.BlockInventory)
        {
            ClientInventoryScreenBuilder.BuildCreativeCatalog(width, height, MousePosition, player, _creativeTabIndex, CreativeSearchQuery, _inventoryScrollRow, buttons, cells);
            return;
        }

        if (Mode is UiOverlayMode.SurvivalInventory or UiOverlayMode.CraftingTableScreen or UiOverlayMode.FurnaceScreen)
        {
            ClientInventoryScreenBuilder.BuildContainerScreen(Mode, width, height, MousePosition, player, _items, _recipes, _containers.Grid2x2, _containers.Grid3x3, _containers.ActiveFurnace, buttons, cells);
            return;
        }

        ClientInventoryScreenBuilder.BuildStandardMenuButtons(
            Mode, width, height, MousePosition, player, ServerEndpointInput, UsernameInput, FocusedInputField, _fovDegrees, ActiveViewDistance, buttons);
    }
}
