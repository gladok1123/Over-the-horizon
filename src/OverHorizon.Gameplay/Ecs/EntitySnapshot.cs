using OverHorizon.Core.Math;
using OverHorizon.Gameplay.Ecs.Components;

namespace OverHorizon.Gameplay.Ecs;

internal readonly record struct EntitySnapshot(
    Arch.Core.Entity Handle,
    long EntityId,
    Vec3d Position,
    Vec3d Velocity,
    BodyDimensionsComponent Dimensions,
    bool WasOnGround,
    bool HasAi,
    AiBrainComponent AiBrain);
