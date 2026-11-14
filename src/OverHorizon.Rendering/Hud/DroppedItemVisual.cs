using OverHorizon.Core.Math;

namespace OverHorizon.Rendering.Hud;

/// <summary>Render-thread snapshot of a 3D dropped item entity spinning and bobbing in the world.</summary>
public readonly record struct DroppedItemVisual(
    Vec3d Position,
    float SpinRadians,
    int Count,
    bool IsBlock3D,
    int TopTile,
    int SideTile,
    int SpriteTile,
    float LightBrightness = 1.0f);
