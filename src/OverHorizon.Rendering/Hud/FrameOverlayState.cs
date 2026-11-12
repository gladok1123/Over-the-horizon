using System.Numerics;
using OverHorizon.Core.Math;

namespace OverHorizon.Rendering.Hud;

/// <summary>
/// Per-frame dynamic 3D world overlays (remote player avatars, targeted block box, sky sun/clouds, 1st-person held block)
/// and 2D UI/HUD state passed to <see cref="IRenderSubsystem"/>.
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
    IReadOnlyList<UiInventoryCell>? InventoryCells = null);
