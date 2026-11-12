using System.Numerics;
using OverHorizon.Core.Math;

namespace OverHorizon.Rendering.Hud;

/// <summary>
/// Per-frame dynamic 3D world overlays, underwater optics, Survival HUD stats, and 2D menu state passed to <see cref="IRenderSubsystem"/>.
/// </summary>
public sealed record FrameOverlayState(
    BlockPos? TargetedBlock,
    int SelectedHotbarSlot,
    ReadOnlyMemory<Vector3> HotbarSlotColors,
    IReadOnlyList<RemotePlayerAvatar> RemotePlayers,
    UiOverlayMode Mode = UiOverlayMode.InGame,
    Vector2 ViewportSize = default,
    Vector3 HeldBlockColor = default,
    float WalkBobPhase = 0.0f,
    float HandSwingProgress = 0.0f,
    float CloudDriftOffset = 0.0f,
    string ActiveBlockDisplayName = "",
    string DebugOverlayText = "",
    string MenuTitle = "",
    string MenuSubtitle = "",
    string SplashText = "",
    string StatusMessage = "",
    string ChatInputBuffer = "",
    IReadOnlyList<string>? RecentChatLines = null,
    IReadOnlyList<UiButtonItem>? Buttons = null,
    IReadOnlyList<UiInventoryCell>? InventoryCells = null,
    bool IsUnderwater = false,
    bool IsSurvivalMode = false,
    float Health = 20.0f,
    float MaxHealth = 20.0f,
    int Hunger = 20,
    float AirSupplyNormalized = 1.0f,
    float MiningProgress = 0.0f,
    float HurtFlashStrength = 0.0f,
    bool HeldIsToolOrWeapon = false,
    bool HeldIsEmpty = false,
    ReadOnlyMemory<int> HotbarSlotTiles = default,
    ReadOnlyMemory<int> HotbarSlotCounts = default,
    string CreativeSearchQuery = "");
