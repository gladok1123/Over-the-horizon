using OverHorizon.Core.Math;

namespace OverHorizon.Rendering.Hud;

/// <summary>Render-thread snapshot of a smooth continuous-Y falling block entity (Sand / Gravel).</summary>
public readonly record struct FallingBlockVisual(
    Vec3d FeetPosition,
    int TopTile,
    int BottomTile,
    int SideTile,
    float LightBrightness = 1.0f);
