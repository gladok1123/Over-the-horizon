using System.Numerics;
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

/// <summary>Render-thread snapshot of a smooth continuous-Y falling block entity (Sand / Gravel).</summary>
public readonly record struct FallingBlockVisual(
    Vec3d FeetPosition,
    int TopTile,
    int BottomTile,
    int SideTile,
    float LightBrightness = 1.0f);

/// <summary>Render-thread snapshot of a 3D billboard world particle.</summary>
public readonly record struct WorldParticleVisual(
    Vec3d Position,
    float Size,
    Vector4 Color,
    int TileIndex = -1,
    float UvOffsetX = 0.25f,
    float UvOffsetY = 0.25f);

/// <summary>Active potion/food status effect badge displayed in the top-right HUD corner.</summary>
public readonly record struct HudStatusEffectVisual(
    string Label,
    int RemainingSeconds,
    Vector3 AccentColor);
