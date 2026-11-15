using OverHorizon.Core.Math;

namespace OverHorizon.Gameplay.Ecs.Components;

/// <summary>
/// World-space double-precision linear velocity component (blocks per second).
/// </summary>
/// <param name="Value">Current 3D velocity vector.</param>
public record struct VelocityComponent(Vec3d Value);
