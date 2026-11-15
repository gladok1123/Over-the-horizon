using OverHorizon.Core.Math;

namespace OverHorizon.Gameplay.Ecs.Components;

/// <summary>
/// World-space double-precision position component.
/// </summary>
/// <param name="Value">Current 3D position in blocks.</param>
public record struct PositionComponent(Vec3d Value);
