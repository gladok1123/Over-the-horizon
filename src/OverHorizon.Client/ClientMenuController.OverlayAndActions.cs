using System.Globalization;
using System.Numerics;
using OverHorizon.Core;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Effects;
using OverHorizon.Rendering.Hud;
using OverHorizon.World.Lighting;

namespace OverHorizon.Client;

/// <summary>
/// Container click routing, double-click world entry, settings toggles, and <see cref="FrameOverlayState"/> assembly
/// for <see cref="ClientMenuController"/>.
/// </summary>
internal sealed partial class ClientMenuController
{
    public string? HandleContainerOrMenuClick(Vector2 mousePos, Vector2 viewport, ClientPlayerController player, bool isRightClick)
    {
        MousePosition = mousePos;
        var width = viewport.X > 64.0f ? viewport.X : 1280.0f;
        var height = viewport.Y > 64.0f ? viewport.Y : 720.0f;

        if (Mode == UiOverlayMode.BlockInventory && !isRightClick)
        {
            var hitTab = ClientInventoryScreenBuilder.HitTestCreativeTab(width, height, mousePos);
            if (hitTab >= 0)
            {
                _creativeTabIndex = hitTab;
                _inventoryScrollRow = 0;
                return $"tab_{hitTab.ToString(CultureInfo.InvariantCulture)}";
            }
        }

        var buttons = new List<UiButtonItem>(14);
        var cells = new List<UiInventoryCell>(64);
        BuildScreenControls(width, height, player, buttons, cells);

        for (var i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            if (!c.IsHovered) continue;
            if (Mode == UiOverlayMode.BlockInventory)
            {
                if (c.BlockIndex == ClientInventoryScreenBuilder.CreativeDeleteTrashCellId)
                {
                    if (isRightClick || _containers.CursorStack.IsEmpty) player.Hotbar.ClearEntireInventory();
                    _containers.ClearCursor();
                    return "creative_delete";
                }

                if (c.BlockIndex >= ClientInventoryScreenBuilder.CreativeCatalogCellOffset)
                {
                    var catIdx = c.BlockIndex - ClientInventoryScreenBuilder.CreativeCatalogCellOffset;
                    _containers.PickOrPlaceCreativeCatalogItem(catIdx, fullStack: isRightClick, player.Hotbar, player.Mode);
                    return "pick_creative";
                }

                return _containers.HandleCellClick(Mode, c.BlockIndex, isRightClick, player);
            }

            if (Mode is UiOverlayMode.SurvivalInventory or UiOverlayMode.CraftingTableScreen or UiOverlayMode.FurnaceScreen or
                UiOverlayMode.BarrelScreen or UiOverlayMode.ChestScreen)
            {
                return _containers.HandleCellClick(Mode, c.BlockIndex, isRightClick, player);
            }
        }

        if (Mode == UiOverlayMode.BlockInventory && !_containers.CursorStack.IsEmpty)
        {
            var (wx, wy, ww, wh) = ClientInventoryScreenBuilder.GetCreativeWindowBounds(width, height);
            if (mousePos.X < wx || mousePos.X > wx + ww || mousePos.Y < wy - 90.0f || mousePos.Y > wy + wh)
            {
                _containers.ClearCursor();
                return "creative_delete";
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
        IReadOnlyList<PrimedTntVisual> primedTnts,
        long worldTimeTicks,
        float skyBrightness,
        ClientChatHistory chatHistory,
        string latestDebugText,
        IReadOnlyList<MinecartVisual>? minecarts = null,
        IReadOnlyList<ArrowProjectileVisual>? arrows = null,
        IReadOnlyList<DroppedItemVisual>? droppedItems = null,
        IReadOnlyList<FallingBlockVisual>? fallingBlocks = null,
        IReadOnlyList<WorldParticleVisual>? worldParticles = null,
        IReadOnlyList<HudStatusEffectVisual>? statusEffects = null)
    {
        var width = viewport.X > 64.0f ? viewport.X : 1280.0f;
        var height = viewport.Y > 64.0f ? viewport.Y : 720.0f;
        player.Hotbar.RefreshSlotVisuals(player.Mode);
        var chunkFogDist = Math.Max(48.0f, (ActiveViewDistance + 2) * 16.0f);
        player.Camera.FarPlane = Math.Max(640.0f, chunkFogDist + 160.0f);

        var (title, subtitle) = Mode switch
        {
            UiOverlayMode.TitleMenu => ("OVER THE HORIZON", "C# EDITION"),
            UiOverlayMode.WorldSelectMenu => ("SELECT WORLD", _savedWorlds.Count == 0 ? "No saved worlds yet - click 'Create New World'!" : "Double-click a world or click 'Play Selected World'"),
            UiOverlayMode.CreateWorldMenu => ("CREATE NEW WORLD", "Enter world name, choose Game Mode, and optional Seed"),
            UiOverlayMode.MultiplayerMenu => ("PLAY MULTIPLAYER", "Enter server IP:Port or host a local server on TCP 25565"),
            UiOverlayMode.OptionsMenu => ("OPTIONS", "Controls: LMB Mine | RMB Use/Shield | MMB Pick | Q Drop | F Offhand | E Inv | F4 Mode | F5 View"),
            UiOverlayMode.PauseMenu => ("GAME MENU", IsLanOpen ? "Hosted on LAN (TCP 25565) - Press Esc to resume" : $"Mode: {player.Mode} - Press Esc to resume"),
            UiOverlayMode.BlockInventory => (ClientInventoryScreenBuilder.GetCreativeTabTitle(_creativeTabIndex), string.Empty),
            UiOverlayMode.DeathScreen => ("YOU DIED!", "Your items dropped in the world - click Respawn to continue"),
            _ => (string.Empty, string.Empty),
        };

        List<UiButtonItem>? buttons = null;
        List<UiInventoryCell>? invCells = null;
        if (Mode is not (UiOverlayMode.InGame or UiOverlayMode.ChatInput))
        {
            buttons = new List<UiButtonItem>(14);
            invCells = new List<UiInventoryCell>(64);
            BuildScreenControls(width, height, player, buttons, invCells);
        }

        var (eyeSky, eyeBlock) = worldCache.GetLight(player.Camera.Position.ToBlockPos());
        var nightVisionBoost = player.StatusEffects.HasEffect(StatusEffectType.NightVision) ? (byte)15 : (byte)0;
        var effectiveSky = (byte)Math.Clamp((int)MathF.Round(eyeSky * skyBrightness), 0, 15);
        var furnaceBoost = _containers.ComputeNearbyLitFurnaceLight(player.Camera.Position);
        var heldEmit = Math.Max(player.Hotbar.CombinedHeldLightEmission, nightVisionBoost);
        var effLevel = LightingEngine.ComputeEffectiveLightLevel(effectiveSky, Math.Max(eyeBlock, furnaceBoost), heldEmit);
        var handBright = LightingEngine.LightLevelToBrightness(effLevel);

        var activeFurnace = _containers.ActiveFurnace;
        var curStack = _containers.CursorStack;
        var cursorTile = curStack.IsEmpty ? -1 : player.Hotbar.ResolveAtlasTile(_items.GetById(curStack.ItemId).Key);

        BlockPos? targetBlock = (Mode == UiOverlayMode.InGame && player.Mode != GameMode.Spectator) ? player.CurrentTarget?.BlockPosition : null;
        BoundingBox? targetBox = null;
        if (targetBlock is { } tb && worldCache.TryGetSelectionBox(tb, out var selBounds))
        {
            targetBox = selBounds;
        }

        var showInvPlayer = Mode == UiOverlayMode.SurvivalInventory || (Mode == UiOverlayMode.BlockInventory && _creativeTabIndex == 6);
        Vector4 invPlayerRect = default;
        if (Mode == UiOverlayMode.SurvivalInventory)
        {
            var (wx, wy, _, _) = ClientInventoryScreenBuilder.GetContainerWindowBounds(width, height);
            const float cs = ClientInventoryScreenBuilder.ContainerGuiScale;
            invPlayerRect = new Vector4(wx + (26.0f * cs), wy + (8.0f * cs), 49.0f * cs, 70.0f * cs);
        }
        else if (Mode == UiOverlayMode.BlockInventory && _creativeTabIndex == 6)
        {
            var (wx, wy, _, _) = ClientInventoryScreenBuilder.GetCreativeWindowBounds(width, height);
            const float cs = ClientInventoryScreenBuilder.CreativeGuiScale;
            invPlayerRect = new Vector4(wx + (72.0f * cs), wy + (5.0f * cs), 34.0f * cs, 45.0f * cs);
        }

        RemotePlayerAvatar? localThirdPersonAvatar = null;
        if (player.CameraPerspectiveMode != 0 && player.Mode != GameMode.Spectator)
        {
            var localSwing = player.LastSwingWasOffhand ? player.OffhandSwingProgress : player.HandSwingProgress;
            localThirdPersonAvatar = new RemotePlayerAvatar(
                EntityId: -1,
                Username: UsernameInput,
                FeetPosition: player.FeetPosition,
                YawDegrees: player.Camera.YawDegrees,
                PitchDegrees: player.Camera.PitchDegrees,
                WalkPhase: player.WalkBobPhase,
                HurtFlash: player.HurtFlashStrength,
                Health: player.Health,
                Armor: player.Hotbar.BuildLocalArmorVisual(),
                LightBrightness: handBright,
                IsSpectator: false,
                IsBlockingShield: player.IsBlockingShield,
                IsDrawingBow: player.Hotbar.SelectedIsBow && player.BowChargeNormalized > 0.01f,
                BowCharge: player.BowChargeNormalized,
                HandSwingProgress: localSwing,
                LeftHandSwing: player.LastSwingWasOffhand,
                MainHandSpriteTile: player.Hotbar.SelectedItemTile,
                MainHandIsShield: player.Hotbar.SelectedIsShield,
                MainHandIsBow: player.Hotbar.SelectedIsBow,
                MainHandIsBlock3D: player.Hotbar.SelectedIsBlock3D,
                MainHandBlockTile: player.Hotbar.SelectedSideTile,
                OffhandIsShield: player.Hotbar.OffhandIsShield,
                OffhandSpriteTile: player.Hotbar.OffhandItemTile,
                IsSneaking: player.IsSneaking,
                IsRidingMinecart: player.RidingMinecartId >= 0);
        }

        return new FrameOverlayState(
            TargetedBlock: targetBlock,
            SelectedHotbarSlot: player.Hotbar.SelectedSlot,
            HotbarSlotColors: player.Hotbar.ActiveSlotColors,
            RemotePlayers: remotePlayers)
        {
            Mode = Mode,
            ViewportSize = new Vector2(width, height),
            ActiveBlockDisplayName = player.Hotbar.SelectedBlockDisplayName,
            HeldBlockColor = player.Hotbar.SelectedBlockColor,
            HandSwingProgress = player.HandSwingProgress,
            OffhandSwingProgress = player.OffhandSwingProgress,
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
            IsSpectatorMode = player.Mode == GameMode.Spectator,
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
            HeldIsShield = player.Hotbar.SelectedIsShield,
            HeldTopTile = player.Hotbar.SelectedTopTile,
            HeldSideTile = player.Hotbar.SelectedSideTile,
            HeldFrontTile = player.Hotbar.SelectedFrontTile,
            HeldBottomTile = player.Hotbar.SelectedBottomTile,
            HeldItemSpriteTile = player.Hotbar.SelectedItemTile,
            OffhandIsEmpty = player.Hotbar.OffhandIsEmpty,
            OffhandIsBlock3D = player.Hotbar.OffhandIsBlock3D,
            OffhandIsTorchStyle = player.Hotbar.OffhandIsTorchStyle,
            OffhandIsToolOrWeapon = player.Hotbar.OffhandIsToolOrWeapon,
            OffhandIsShield = player.Hotbar.OffhandIsShield,
            OffhandTopTile = player.Hotbar.OffhandTopTile,
            OffhandSideTile = player.Hotbar.OffhandSideTile,
            OffhandFrontTile = player.Hotbar.OffhandFrontTile,
            OffhandBottomTile = player.Hotbar.OffhandBottomTile,
            OffhandItemSpriteTile = player.Hotbar.OffhandItemTile,
            OffhandCount = player.Hotbar.OffhandCount,
            IsBlockingShield = player.IsBlockingShield,
            ShieldRaiseNormalized = player.ShieldRaiseNormalized,
            HeldLightBrightness = handBright,
            HeldTorchLightLevel = Math.Max(heldEmit, furnaceBoost),
            CursorScreenPos = MousePosition,
            CursorItemTile = cursorTile,
            CursorItemCount = curStack.Count,
            FurnaceBurnNormalized = activeFurnace?.NormalizedBurnRemaining ?? 0.0f,
            FurnaceCookNormalized = activeFurnace?.NormalizedCookProgress ?? 0.0f,
            WorldMobs = worldMobs,
            PrimedTnts = primedTnts,
            WorldTimeTicks = worldTimeTicks,
            TargetedSelectionBox = targetBox,
            ShowInventoryPlayerModel = showInvPlayer,
            InventoryPlayerPreviewRect = invPlayerRect,
            HotbarSlotTiles = player.Hotbar.ActiveSlotTiles,
            HotbarSlotCounts = player.Hotbar.ActiveSlotCounts,
            CreativeSearchQuery = CreativeSearchQuery,
            CreativeSelectedTab = _creativeTabIndex,
            CreativeScrollNormalized = _creativeScrollNorm,
            LocalPlayerArmor = player.Hotbar.BuildLocalArmorVisual(),
            Minecarts = minecarts,
            Arrows = arrows,
            DroppedItems = droppedItems,
            FallingBlocks = fallingBlocks,
            Particles = worldParticles,
            StatusEffects = statusEffects,
            HeldIsBow = player.Hotbar.SelectedIsBow,
            BowChargeNormalized = player.BowChargeNormalized,
            ChunkFogDistance = chunkFogDist,
            SleepFadeAlpha = player.SleepFadeAlpha,
            IsThirdPersonCamera = localThirdPersonAvatar.HasValue,
            LocalThirdPersonPlayer = localThirdPersonAvatar,
            IsNetherDimension = player.Dimension == DimensionId.Nether,
        };
    }

    private void ApplySliderAtX(in UiButtonItem b, float mouseX, ClientPlayerController player)
    {
        var t = Math.Clamp((mouseX - (b.X + 8.0f)) / Math.Max(1.0f, b.Width - 16.0f), 0.0f, 1.0f);
        switch (b.ActionId)
        {
            case "slider_view_dist":
                ActiveViewDistance = Math.Clamp((int)MathF.Round(2.0f + (t * 62.0f)), 2, 64);
                break;
            case "slider_fov":
                FovDegrees = MathF.Round(70.0f + (t * 40.0f));
                player.Camera.FieldOfViewDegrees = FovDegrees;
                break;
            case "slider_volume":
                player.MasterVolume = MathF.Round(t * 100.0f) / 100.0f;
                break;
            case "slider_sensitivity":
                player.MouseSensitivity = 0.03f + (t * 0.22f);
                break;
        }
    }

    private string ActivateAction(string id, ClientPlayerController player)
    {
        StatusMessage = string.Empty;
        if (id.StartsWith("select_world_", StringComparison.Ordinal) && int.TryParse(id.AsSpan(13), out var wIdx))
        {
            var clamped = Math.Clamp(wIdx, 0, Math.Max(0, _savedWorlds.Count - 1));
            var now = _uiClickWatch.Elapsed.TotalSeconds;
            var isDoubleClick = clamped == _lastClickedWorldIndex && (now - _lastWorldClickTimeSeconds) <= 0.48 && _savedWorlds.Count > 0;
            _selectedWorldIndex = clamped;
            _lastClickedWorldIndex = clamped;
            _lastWorldClickTimeSeconds = now;
            return isDoubleClick ? "play_selected_world" : id;
        }

        switch (id)
        {
            case "open_world_select":
                RefreshSavedWorlds();
                Mode = UiOverlayMode.WorldSelectMenu;
                return id;
            case "open_create_world":
                NewWorldName = _savedWorlds.Count == 0 ? "New World" : $"New World {(_savedWorlds.Count + 1).ToString(CultureInfo.InvariantCulture)}";
                NewWorldSeed = string.Empty;
                FocusedInputField = 0;
                Mode = UiOverlayMode.CreateWorldMenu;
                return id;
            case "toggle_create_gamemode":
                NewWorldGameMode = NewWorldGameMode switch
                {
                    GameMode.Survival => GameMode.Creative,
                    GameMode.Creative => GameMode.Spectator,
                    _ => GameMode.Survival,
                };
                return id;
            case "focus_world_name":
                FocusedInputField = 0;
                return id;
            case "focus_world_seed":
                FocusedInputField = 1;
                return id;
            case "delete_selected_world":
                if (SelectedSavedWorld is { } del)
                {
                    _ = WorldSaveManager.DeleteWorld(del.Slug);
                    RefreshSavedWorlds();
                    StatusMessage = $"Deleted world '{del.WorldName}'.";
                }

                return id;
            case "play_selected_world":
                if (_savedWorlds.Count == 0)
                {
                    Mode = UiOverlayMode.CreateWorldMenu;
                    return "open_create_world";
                }

                return "play_selected_world";
            case "resume":
                CloseContainerAndRefundCursor(player);
                Mode = UiOverlayMode.InGame;
                return id;
            case "respawn_player":
                player.Respawn();
                Mode = UiOverlayMode.InGame;
                return id;
            case "toggle_gamemode":
                _ = player.CycleNextGameMode();
                return id;
            case "toggle_xray":
                ToggleXRayMode(player);
                return id;
            case "toggle_clouds":
                CloudsEnabled = !CloudsEnabled;
                SavePersistedSettings(player);
                return id;
            case "toggle_particles":
                ParticlesMode = (ParticlesMode + 1) % 3;
                SavePersistedSettings(player);
                return id;
            case "toggle_vsync":
                VSyncEnabled = !VSyncEnabled;
                SavePersistedSettings(player);
                return id;
            case "toggle_invert_y":
                player.InvertMouseY = !player.InvertMouseY;
                SavePersistedSettings(player);
                return id;
            case "open_multiplayer":
                FocusedInputField = 0;
                Mode = UiOverlayMode.MultiplayerMenu;
                return id;
            case "open_options":
                _returnModeFromOptions = Mode;
                Mode = UiOverlayMode.OptionsMenu;
                return id;
            case "options_done":
                CloseOptionsAndReturn(player);
                return id;
            case "open_inventory":
                if (player.Mode == GameMode.Spectator) return id;
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
            default:
                return id;
        }
    }

    private void BuildScreenControls(float width, float height, ClientPlayerController player, List<UiButtonItem> buttons, List<UiInventoryCell> cells)
    {
        if (Mode == UiOverlayMode.BlockInventory)
        {
            _creativeScrollNorm = ClientInventoryScreenBuilder.BuildCreativeCatalog(width, height, MousePosition, player, _items, _creativeTabIndex, CreativeSearchQuery, _inventoryScrollRow, cells);
            return;
        }

        if (Mode is UiOverlayMode.SurvivalInventory or UiOverlayMode.CraftingTableScreen or UiOverlayMode.FurnaceScreen or
            UiOverlayMode.BarrelScreen or UiOverlayMode.ChestScreen)
        {
            ClientInventoryScreenBuilder.BuildContainerScreen(Mode, width, height, MousePosition, player, _items, _recipes, _containers.Grid2x2, _containers.Grid3x3, _containers.ActiveFurnace, cells, _containers.ActiveContainer);
            return;
        }

        ClientInventoryScreenBuilder.BuildStandardMenuButtons(
            Mode, width, height, MousePosition, player, this, _savedWorlds, _selectedWorldIndex, buttons, IsMultiplayerConnected, IsLanOpen);
    }
}
