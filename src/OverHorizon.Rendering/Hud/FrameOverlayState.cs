using System.Numerics;
using OverHorizon.Core.Math;

namespace OverHorizon.Rendering.Hud;

/// <summary>
/// Per-frame dynamic 3D avatar, targeted block highlight, and 2D hotbar/crosshair state passed to <see cref="IRenderSubsystem"/>.
/// </summary>
/// <param name="TargetedBlock">Currently raycast-targeted solid block, or <see langword="null"/> if none.</param>
/// <param name="SelectedHotbarSlot">Active hotbar slot index (<c>0..8</c>).</param>
/// <param name="HotbarSlotColors">Representative RGB colors for the 9 hotbar slots.</param>
/// <param name="RemotePlayers">Currently visible remote multiplayer friends.</param>
public sealed record FrameOverlayState(
    BlockPos? TargetedBlock,
    int SelectedHotbarSlot,
    ReadOnlyMemory<Vector3> HotbarSlotColors,
    IReadOnlyList<RemotePlayerAvatar> RemotePlayers);
