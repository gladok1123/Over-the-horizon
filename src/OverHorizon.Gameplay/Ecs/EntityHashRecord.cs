using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;

namespace OverHorizon.Gameplay.Ecs;

internal readonly record struct EntityHashRecord(
    long EntityId,
    ResourceKey TypeKey,
    Vec3d Position,
    Vec3d Velocity,
    float CurrentHealth,
    float MaxHealth) : IComparable<EntityHashRecord>
{
    public int CompareTo(EntityHashRecord other) => EntityId.CompareTo(other.EntityId);
}
