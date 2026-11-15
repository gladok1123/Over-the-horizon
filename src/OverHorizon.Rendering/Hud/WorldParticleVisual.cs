using System.Numerics;
using OverHorizon.Core.Math;

namespace OverHorizon.Rendering.Hud;

/// <summary>Render-thread snapshot of a 3D billboard world particle.</summary>
public readonly record struct WorldParticleVisual(
    Vec3d Position,
    float Size,
    Vector4 Color,
    int TileIndex = -1,
    float UvOffsetX = 0.25f,
    float UvOffsetY = 0.25f);
