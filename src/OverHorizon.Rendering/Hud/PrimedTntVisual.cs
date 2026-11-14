using OverHorizon.Core.Math;

namespace OverHorizon.Rendering.Hud;

/// <summary>
/// Per-frame 3D physics-simulated primed TNT entity visual state.
/// </summary>
public readonly record struct PrimedTntVisual(
    Vec3d FeetPosition,
    bool IsFlashingWhite,
    float LightBrightness = 1.0f);
