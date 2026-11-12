using OverHorizon.Core.Math;

namespace OverHorizon.Rendering.Hud;

/// <summary>
/// Render-thread snapshot of a connected remote player's 3D pose and limb animation phase.
/// </summary>
/// <param name="EntityId">Authoritative entity ID.</param>
/// <param name="Username">Player username (used for deterministic outfit palette).</param>
/// <param name="FeetPosition">World-space feet position.</param>
/// <param name="YawDegrees">Horizontal yaw in degrees.</param>
/// <param name="PitchDegrees">Vertical pitch in degrees.</param>
/// <param name="WalkPhase">Accumulated limb swing phase in radians.</param>
public readonly record struct RemotePlayerAvatar(
    long EntityId,
    string Username,
    Vec3d FeetPosition,
    float YawDegrees,
    float PitchDegrees,
    float WalkPhase);
