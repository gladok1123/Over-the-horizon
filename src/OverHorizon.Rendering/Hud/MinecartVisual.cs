using OverHorizon.Core.Math;

namespace OverHorizon.Rendering.Hud;

/// <summary>
/// Render-thread snapshot of an active 3D Minecart vehicle in the world.
/// </summary>
public readonly record struct MinecartVisual(
    int EntityId,
    Vec3d Position,
    float YawDegrees,
    float SlopePitchDegrees,
    float HurtWobble,
    float LightBrightness = 1.0f);
