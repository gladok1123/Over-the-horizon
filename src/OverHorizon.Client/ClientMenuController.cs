using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Crafting;
using OverHorizon.Gameplay.Inventory;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Meshing;

namespace OverHorizon.Client;

/// <summary>
/// Interactive menu &amp; container screen controller powering the Title Screen, World Select (with double-click entry),
/// Create World, persistent <c>options.json</c> Video/Graphics/Audio/Controls settings, X-Ray &amp; Spectator toggles,
/// Pause Menu "Open to LAN", and Creative / Survival / Crafting / Furnace containers.
/// </summary>
internal sealed partial class ClientMenuController
{
    private static readonly string[] SplashLines =
    [
        "Redstone Comparators & Pistons!", "Open to LAN & Full 3D Armor!", "3D Volumetric Clouds & Stars!",
        "Shield Blocking & 3D Drops!", "Mineshafts, Geodes & Pyramids!", "Spectator Noclip & X-Ray!",
    ];

    private readonly IRegistry<ItemDefinition> _items;
    private readonly IRegistry<ICraftingRecipe> _recipes;
    private readonly ClientContainerSlotController _containers;
    private readonly Stopwatch _uiClickWatch = Stopwatch.StartNew();
    private readonly string _splashText;
    private readonly bool _headlessMode;

    private UiOverlayMode _returnModeFromOptions = UiOverlayMode.TitleMenu;
    private IReadOnlyList<SavedWorldMetadata> _savedWorlds = [];
    private int _selectedWorldIndex;
    private int _lastClickedWorldIndex = -1;
    private double _lastWorldClickTimeSeconds = -10.0;
    private string? _draggingSliderId;
    private int _creativeTabIndex;
    private int _inventoryScrollRow;
    private float _creativeScrollNorm;

    public ClientMenuController(
        ClientOptions options,
        UiOverlayMode initialMode,
        IRegistry<ItemDefinition> items,
        IRegistry<ICraftingRecipe>? recipes = null,
        ClientPlayerController? playerToHydrate = null)
    {
        _items = items;
        _recipes = recipes ?? BuiltInRecipes.CreateFrozen(items);
        _containers = new ClientContainerSlotController(_items, _recipes);
        _headlessMode = options.Headless || options.HeadlessCheckOnly;
        Mode = initialMode;

        var persisted = _headlessMode ? new PersistedClientSettings { ViewDistanceChunks = options.ViewDistanceChunks } : PersistedClientSettings.LoadOrDefault();
        ServerEndpointInput = !string.IsNullOrWhiteSpace(options.ServerHost)
            ? $"{options.ServerHost}:{options.ServerPort.ToString(CultureInfo.InvariantCulture)}"
            : persisted.LastServerEndpoint;
        UsernameInput = options.Username != "Explorer" ? options.Username : persisted.LastUsername;
        ActiveViewDistance = options.ViewDistanceChunks != 4 ? Math.Clamp(options.ViewDistanceChunks, 1, 64) : Math.Clamp(persisted.ViewDistanceChunks, 2, 64);
        FovDegrees = Math.Clamp(persisted.FovDegrees, 70.0f, 110.0f);
        VSyncEnabled = persisted.VSync;
        CloudsEnabled = persisted.CloudsEnabled;
        ParticlesMode = persisted.ParticlesEnabled ? 0 : 2;
        XRayEnabled = !_headlessMode && persisted.XRayEnabled;
        GreedySectionMesher.XRayEnabled = XRayEnabled;

        if (playerToHydrate is not null)
        {
            playerToHydrate.MasterVolume = Math.Clamp(persisted.MasterVolume, 0.0f, 1.0f);
            playerToHydrate.MouseSensitivity = Math.Clamp(persisted.MouseSensitivity, 0.03f, 0.25f);
            playerToHydrate.InvertMouseY = persisted.InvertMouseY;
            playerToHydrate.Camera.FieldOfViewDegrees = FovDegrees;
        }

        _splashText = SplashLines[(int)((uint)Environment.TickCount % (uint)SplashLines.Length)];
        RefreshSavedWorlds();
    }

    public UiOverlayMode Mode { get; set; }
    public bool IsGameplayActive => Mode == UiOverlayMode.InGame;
    public string ServerEndpointInput { get; set; }
    public string UsernameInput { get; set; }
    public string NewWorldName { get; set; } = "New World";
    public GameMode NewWorldGameMode { get; set; } = GameMode.Survival;
    public string NewWorldSeed { get; set; } = string.Empty;
    public int FocusedInputField { get; set; }
    public string ChatDraft { get; set; } = string.Empty;
    public string CreativeSearchQuery { get; set; } = string.Empty;
    public string StatusMessage { get; set; } = string.Empty;
    public int ActiveViewDistance { get; private set; }
    public float FovDegrees { get; private set; }
    public bool VSyncEnabled { get; private set; }
    public bool CloudsEnabled { get; private set; }
    public int ParticlesMode { get; private set; }
    public bool XRayEnabled { get; private set; }
    public bool XRayDirtyRemeshRequested { get; set; }
    public bool IsMultiplayerConnected { get; set; }
    public bool IsLanOpen { get; set; }
    public Vector2 MousePosition { get; set; }
    public float PanoramaYawDegrees { get; set; }
    public float CloudDriftOffset { get; private set; }
    private ClientPlayerController? _lastPlayerRef;

    public SavedWorldMetadata? SelectedSavedWorld =>
        _selectedWorldIndex >= 0 && _selectedWorldIndex < _savedWorlds.Count ? _savedWorlds[_selectedWorldIndex] : null;

    public void SavePersistedSettings(ClientPlayerController? player = null)
    {
        if (_headlessMode) return;
        var p = player ?? _lastPlayerRef;
        var dto = new PersistedClientSettings
        {
            ViewDistanceChunks = ActiveViewDistance,
            FovDegrees = FovDegrees,
            MasterVolume = p?.MasterVolume ?? 1.0f,
            MouseSensitivity = p?.MouseSensitivity ?? 0.12f,
            InvertMouseY = p?.InvertMouseY ?? false,
            VSync = VSyncEnabled,
            CloudsEnabled = CloudsEnabled,
            ParticlesEnabled = ParticlesMode < 2,
            XRayEnabled = XRayEnabled,
            LastUsername = string.IsNullOrWhiteSpace(UsernameInput) ? "Explorer" : UsernameInput,
            LastServerEndpoint = string.IsNullOrWhiteSpace(ServerEndpointInput) ? "127.0.0.1:25565" : ServerEndpointInput,
        };
        dto.Save();
    }

    public void ToggleXRayMode(ClientPlayerController? player = null)
    {
        XRayEnabled = !XRayEnabled;
        GreedySectionMesher.XRayEnabled = XRayEnabled;
        XRayDirtyRemeshRequested = true;
        SavePersistedSettings(player);
    }

    public void CloseOptionsAndReturn(ClientPlayerController? player = null)
    {
        _draggingSliderId = null;
        SavePersistedSettings(player);
        Mode = _returnModeFromOptions;
    }

    public void RefreshSavedWorlds()
    {
        _savedWorlds = WorldSaveManager.ListWorlds();
        _selectedWorldIndex = Math.Clamp(_selectedWorldIndex, 0, Math.Max(0, _savedWorlds.Count - 1));
    }

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
        if (player is not null) _lastPlayerRef = player;
        CloudDriftOffset = CloudsEnabled ? (CloudDriftOffset + (float)(dt * 1.4)) % 1024.0f : -99999.0f;
        if (Mode == UiOverlayMode.TitleMenu)
        {
            PanoramaYawDegrees = (PanoramaYawDegrees + (float)(dt * 9.0)) % 360.0f;
        }

        _containers.TickFurnaces((float)dt);

        if (player is not null && player.IsDead && Mode is not (UiOverlayMode.TitleMenu or UiOverlayMode.WorldSelectMenu or UiOverlayMode.CreateWorldMenu or UiOverlayMode.DeathScreen))
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

        if (Mode == UiOverlayMode.InGame && player.Mode != GameMode.Spectator)
        {
            _inventoryScrollRow = 0;
            StatusMessage = string.Empty;
            player.Hotbar.SyncInventoryFromCreativeSlots();
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
        if (wheelDelta != 0)
        {
            _inventoryScrollRow = Math.Max(0, _inventoryScrollRow + (wheelDelta > 0 ? -1 : 1));
        }
    }

    public bool TryAssignHoveredCreativeItemToSlot(Vector2 viewport, ClientPlayerController player, int hotbarSlot)
    {
        if (Mode != UiOverlayMode.BlockInventory) return false;
        var w = viewport.X > 64.0f ? viewport.X : 1280.0f;
        var h = viewport.Y > 64.0f ? viewport.Y : 720.0f;
        var cells = new List<UiInventoryCell>(64);
        _ = ClientInventoryScreenBuilder.BuildCreativeCatalog(w, h, MousePosition, player, _items, _creativeTabIndex, CreativeSearchQuery, _inventoryScrollRow, cells);

        for (var i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            if (c.IsHovered && c.BlockIndex >= ClientInventoryScreenBuilder.CreativeCatalogCellOffset)
            {
                var catIdx = c.BlockIndex - ClientInventoryScreenBuilder.CreativeCatalogCellOffset;
                player.Hotbar.EquipPaletteEntryInSlot(catIdx, hotbarSlot);
                StatusMessage = $"Equipped {c.DisplayName} in Slot {hotbarSlot + 1}!";
                return true;
            }
        }

        return false;
    }

    public void AppendTypedCharacter(char ch)
    {
        if (char.IsControl(ch)) return;
        if (Mode == UiOverlayMode.ChatInput && ChatDraft.Length < 120)
        {
            ChatDraft += ch;
        }
        else if (Mode == UiOverlayMode.BlockInventory && _creativeTabIndex != 6 && CreativeSearchQuery.Length < 22)
        {
            CreativeSearchQuery += ch;
            _inventoryScrollRow = 0;
        }
        else if (Mode == UiOverlayMode.CreateWorldMenu)
        {
            if (FocusedInputField == 0 && NewWorldName.Length < 28) NewWorldName += ch;
            else if (FocusedInputField == 1 && NewWorldSeed.Length < 28) NewWorldSeed += ch;
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
        if (Mode == UiOverlayMode.ChatInput && ChatDraft.Length > 0)
        {
            ChatDraft = ChatDraft[..^1];
        }
        else if (Mode == UiOverlayMode.BlockInventory && CreativeSearchQuery.Length > 0)
        {
            CreativeSearchQuery = CreativeSearchQuery[..^1];
            _inventoryScrollRow = 0;
        }
        else if (Mode == UiOverlayMode.CreateWorldMenu)
        {
            if (FocusedInputField == 0 && NewWorldName.Length > 0) NewWorldName = NewWorldName[..^1];
            else if (FocusedInputField == 1 && NewWorldSeed.Length > 0) NewWorldSeed = NewWorldSeed[..^1];
        }
        else if (Mode == UiOverlayMode.MultiplayerMenu)
        {
            if (FocusedInputField == 0 && ServerEndpointInput.Length > 0) ServerEndpointInput = ServerEndpointInput[..^1];
            else if (FocusedInputField == 1 && UsernameInput.Length > 0) UsernameInput = UsernameInput[..^1];
        }
    }

    public void ReleaseSliderDrag()
    {
        if (_draggingSliderId is not null)
        {
            _draggingSliderId = null;
            SavePersistedSettings();
        }
    }

    public void HandleSliderDrag(Vector2 mousePos, Vector2 viewport, ClientPlayerController player)
    {
        MousePosition = mousePos;
        if (_draggingSliderId is null || Mode != UiOverlayMode.OptionsMenu) return;
        var width = viewport.X > 64.0f ? viewport.X : 1280.0f;
        var height = viewport.Y > 64.0f ? viewport.Y : 720.0f;
        var buttons = new List<UiButtonItem>(14);
        ClientInventoryScreenBuilder.BuildStandardMenuButtons(Mode, width, height, mousePos, player, this, _savedWorlds, _selectedWorldIndex, buttons, IsMultiplayerConnected, IsLanOpen);
        for (var i = 0; i < buttons.Count; i++)
        {
            if (buttons[i].ActionId == _draggingSliderId)
            {
                ApplySliderAtX(buttons[i], mousePos.X, player);
                return;
            }
        }
    }
}
