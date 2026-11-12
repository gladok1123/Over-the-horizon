using OverHorizon.Core.Math;

namespace OverHorizon.Gameplay.Ecs;

internal readonly record struct EntityMotionMutation(
    Arch.Core.Entity Handle,
    long EntityId,
    Vec3d NextPosition,
    Vec3d NextVelocity,
    bool NextOnGround) : IComparable<EntityMotionMutation>
{
    public int CompareTo(EntityMotionMutation other) => EntityId.CompareTo(other.EntityId);
}
