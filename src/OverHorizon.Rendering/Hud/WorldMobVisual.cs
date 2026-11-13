using OverHorizon.Core.Math;

namespace OverHorizon.Rendering.Hud;

/// <summary>
/// Per-frame 3D world mob visual state passed from gameplay simulation to <c>SilkOpenGlOverlayRenderer</c>.
/// </summary>
public readonly record struct WorldMobVisual(
    Vec3d FeetPosition,
    float YawDegrees,
    float WalkPhase,
    byte SpeciesIndex,
    float HurtFlash = 0.0f,
    float CreeperFuse = 0.0f,
    bool IsSheared = false,
    float LightBrightness = 1.0f,
    bool IsOnFire = false);
