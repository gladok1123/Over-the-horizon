using OverHorizon.Core.Registry;

namespace OverHorizon.Gameplay.Ecs.Components;

/// <summary>
/// Stable, monotonically assigned 64-bit entity identity independent of recycled ECS slot indices.
/// </summary>
/// <param name="EntityId">Unique deterministic 64-bit entity ID.</param>
/// <param name="TypeKey">Registered entity archetype key.</param>
public readonly record struct EntityIdentity(long EntityId, ResourceKey TypeKey);
