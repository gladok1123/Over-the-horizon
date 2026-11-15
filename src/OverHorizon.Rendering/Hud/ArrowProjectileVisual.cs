using OverHorizon.Core.Math;

namespace OverHorizon.Rendering.Hud;

/// <summary>
/// Render-thread snapshot of an active 3D ballistic Arrow projectile in flight or lodged in terrain.
/// </summary>
public readonly record struct ArrowProjectileVisual(
    int EntityId,
    Vec3d Position,
    float YawDegrees,
    float PitchDegrees,
    float ShakeStrength = 0.0f,
    float LightBrightness = 1.0f,
    bool IsCritical = false);
