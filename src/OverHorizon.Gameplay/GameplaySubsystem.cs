using System.Buffers;
using Arch.Core;
using OverHorizon.Core;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay.Crafting;
using OverHorizon.Gameplay.Ecs;
using OverHorizon.Gameplay.Ecs.Components;
using OverHorizon.Physics;
using OverHorizon.Shared.Hashing;
using OverHorizon.Shared.Random;

namespace OverHorizon.Gameplay;

/// <summary>
/// Horizon-style Arch ECS gameplay subsystem:
/// Phase 1 runs parallel AI and voxel-colliding kinematics over immutable world snapshots;
/// Phase 4 deterministically sorts and commits mutations by <c>EntityId</c>.
/// </summary>
public sealed class GameplaySubsystem : IGameplaySubsystem
{
    private const double TickDeltaSeconds = 1.0 / EngineConstants.TicksPerSecond;
    private const double StepJumpVelocity = 8.4;

    private static readonly QueryDescription AiEntityQuery =
        default(QueryDescription).WithAll<
            EntityIdentity,
            PositionComponent,
            VelocityComponent,
            BodyDimensionsComponent,
            OnGroundComponent,
            AiBrainComponent>();

    private static readonly QueryDescription PassiveEntityQuery =
        default(QueryDescription)
            .WithAll<EntityIdentity, PositionComponent, VelocityComponent, BodyDimensionsComponent, OnGroundComponent>()
            .WithNone<AiBrainComponent, PlayerControlledComponent>();

    private static readonly QueryDescription AllHashableEntityQuery =
        default(QueryDescription).WithAll<EntityIdentity, PositionComponent, VelocityComponent, HealthComponent>();

    private static readonly QueryDescription HealthLookupQuery =
        default(QueryDescription).WithAll<EntityIdentity, HealthComponent>();

    private static readonly QueryDescription PositionLookupQuery =
        default(QueryDescription).WithAll<EntityIdentity, PositionComponent>();

    private readonly Arch.Core.World _ecsWorld;
    private readonly IVoxelCollisionReader? _collisionWorld;
    private readonly IPhysicsSubsystem? _physics;
    private long _nextEntityId;
    private int _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameplaySubsystem"/> class.
    /// </summary>
    /// <param name="worldSeed">Deterministic 64-bit world seed.</param>
    /// <param name="collisionWorld">Optional voxel collision reader (when null, entities move in free space).</param>
    /// <param name="physics">Optional physics subsystem for gravity, fluids, and swept AABB collisions.</param>
    /// <param name="registries">Optional domain registries used to build crafting recipes.</param>
    public GameplaySubsystem(
        long worldSeed = 0x5F3759DF42424242L,
        IVoxelCollisionReader? collisionWorld = null,
        IPhysicsSubsystem? physics = null,
        CoreRegistries? registries = null)
    {
        WorldSeed = worldSeed;
        _collisionWorld = collisionWorld;
        _physics = physics;

        var effectiveRegistries = registries ?? CoreRegistries.CreateDefault();
        Recipes = BuiltInRecipes.CreateFrozen(effectiveRegistries.Items);
        _ecsWorld = Arch.Core.World.Create();
    }

    /// <inheritdoc />
    public string Name => "Gameplay";

    /// <inheritdoc />
    public int PhaseOrder => 10;

    /// <inheritdoc />
    public int EntityCount => _ecsWorld.Size;

    /// <inheritdoc />
    public long WorldSeed { get; }

    /// <inheritdoc />
    public IRegistry<ICraftingRecipe> Recipes { get; }

    /// <inheritdoc />
    public long SpawnEntity(
        ResourceKey typeKey,
        Vec3d position,
        Vec3d velocity,
        float maxHealth = 20.0f,
        AiBrainComponent? aiBrain = null,
        double width = 0.6,
        double height = 1.8,
        long? controllingSessionId = null)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        var assignedId = Interlocked.Increment(ref _nextEntityId);
        var identity = new EntityIdentity(assignedId, typeKey);
        var pos = new PositionComponent(position);
        var vel = new VelocityComponent(velocity);
        var health = new HealthComponent(maxHealth, maxHealth);
        var dims = new BodyDimensionsComponent(width, height);
        var ground = new OnGroundComponent(false);

        if (controllingSessionId is { } sid)
        {
            var ctrl = new PlayerControlledComponent(sid);
            _ = _ecsWorld.Create(in identity, in pos, in vel, in health, in dims, in ground, in ctrl);
        }
        else if (aiBrain is { } brain)
        {
            _ = _ecsWorld.Create(in identity, in pos, in vel, in health, in dims, in ground, in brain);
        }
        else
        {
            _ = _ecsWorld.Create(in identity, in pos, in vel, in health, in dims, in ground);
        }

        return assignedId;
    }

    /// <inheritdoc />
    public Vec3d? TryGetEntityPosition(long entityId)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        Vec3d? found = null;
        _ecsWorld.Query(
            in PositionLookupQuery,
            (ref EntityIdentity id, ref PositionComponent pos) =>
            {
                if (id.EntityId == entityId)
                {
                    found = pos.Value;
                }
            });

        return found;
    }

    /// <inheritdoc />
    public bool TrySetEntityKinematics(long entityId, Vec3d position, Vec3d velocity, bool onGround)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        Entity? target = null;
        _ecsWorld.Query(
            in PositionLookupQuery,
            (Entity entity, ref EntityIdentity id, ref PositionComponent _) =>
            {
                if (id.EntityId == entityId)
                {
                    target = entity;
                }
            });

        if (target is not { } handle)
        {
            return false;
        }

        var nextPos = new PositionComponent(position);
        var nextVel = new VelocityComponent(velocity);
        var nextGround = new OnGroundComponent(onGround);
        _ecsWorld.Set(handle, in nextPos, in nextVel, in nextGround);
        return true;
    }

    /// <inheritdoc />
    public bool DespawnEntity(long entityId)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        Entity? target = null;
        _ecsWorld.Query(
            in PositionLookupQuery,
            (Entity entity, ref EntityIdentity id, ref PositionComponent _) =>
            {
                if (id.EntityId == entityId)
                {
                    target = entity;
                }
            });

        if (target is not { } handle)
        {
            return false;
        }

        _ecsWorld.Destroy(handle);
        return true;
    }

    /// <inheritdoc />
    public bool ApplyDamage(long entityId, float damageAmount)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (damageAmount <= 0.0f)
        {
            return false;
        }

        Entity? deadHandle = null;
        _ecsWorld.Query(
            in HealthLookupQuery,
            (Entity entity, ref EntityIdentity id, ref HealthComponent hp) =>
            {
                if (id.EntityId == entityId)
                {
                    var nextHp = Math.Max(0.0f, hp.Current - damageAmount);
                    hp = new HealthComponent(nextHp, hp.Max);
                    if (nextHp <= 0.0f)
                    {
                        deadHandle = entity;
                    }
                }
            });

        if (deadHandle is { } handle)
        {
            _ecsWorld.Destroy(handle);
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public async ValueTask TickAsync(long tickNumber, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();

        var totalEntities = _ecsWorld.Size;
        if (totalEntities == 0)
        {
            return;
        }

        var snapshots = ArrayPool<EntitySnapshot>.Shared.Rent(totalEntities);
        var mutations = ArrayPool<EntityMotionMutation>.Shared.Rent(totalEntities);

        try
        {
            var count = CaptureSnapshots(snapshots);
            if (count == 0)
            {
                return;
            }

            var seed = WorldSeed;
            var worldReader = _collisionWorld;
            var physics = _physics;

            await Parallel.ForAsync(
                0,
                count,
                cancellationToken,
                (index, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    mutations[index] = SimulateEntityStep(in snapshots[index], seed, tickNumber, worldReader, physics);
                    return ValueTask.CompletedTask;
                }).ConfigureAwait(false);

            // Phase 4: Deterministic merge ordered strictly by stable 64-bit EntityId.
            Array.Sort(mutations, 0, count);

            for (var i = 0; i < count; i++)
            {
                ref readonly var m = ref mutations[i];
                var nextPos = new PositionComponent(m.NextPosition);
                var nextVel = new VelocityComponent(m.NextVelocity);
                var nextGround = new OnGroundComponent(m.NextOnGround);
                _ecsWorld.Set(m.Handle, in nextPos, in nextVel, in nextGround);
            }
        }
        finally
        {
            ArrayPool<EntitySnapshot>.Shared.Return(snapshots, clearArray: false);
            ArrayPool<EntityMotionMutation>.Shared.Return(mutations, clearArray: false);
        }
    }

    /// <inheritdoc />
    public ulong ComputeStateHash()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        var hasher = Fnv1a64.Create();
        var total = _ecsWorld.Size;
        hasher.AppendInt32(total);

        if (total == 0)
        {
            return hasher.Value;
        }

        var records = ArrayPool<EntityHashRecord>.Shared.Rent(total);
        try
        {
            var count = 0;
            _ecsWorld.Query(
                in AllHashableEntityQuery,
                (ref EntityIdentity id, ref PositionComponent pos, ref VelocityComponent vel, ref HealthComponent hp) =>
                {
                    if (count < records.Length)
                    {
                        records[count++] = new EntityHashRecord(
                            id.EntityId,
                            id.TypeKey,
                            pos.Value,
                            vel.Value,
                            hp.Current,
                            hp.Max);
                    }
                });

            Array.Sort(records, 0, count);

            for (var i = 0; i < count; i++)
            {
                ref readonly var r = ref records[i];
                hasher.AppendInt64(r.EntityId);
                hasher.AppendString(r.TypeKey.Namespace);
                hasher.AppendString(r.TypeKey.Path);
                hasher.AppendDouble(r.Position.X);
                hasher.AppendDouble(r.Position.Y);
                hasher.AppendDouble(r.Position.Z);
                hasher.AppendDouble(r.Velocity.X);
                hasher.AppendDouble(r.Velocity.Y);
                hasher.AppendDouble(r.Velocity.Z);
                hasher.AppendDouble(r.CurrentHealth);
                hasher.AppendDouble(r.MaxHealth);
            }

            return hasher.Value;
        }
        finally
        {
            ArrayPool<EntityHashRecord>.Shared.Return(records, clearArray: true);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            Arch.Core.World.Destroy(_ecsWorld);
        }
    }

    private static EntityMotionMutation SimulateEntityStep(
        in EntitySnapshot snapshot,
        long worldSeed,
        long tickNumber,
        IVoxelCollisionReader? worldReader,
        IPhysicsSubsystem? physics)
    {
        var velocity = snapshot.Velocity;

        if (snapshot.HasAi)
        {
            var interval = Math.Max(1, snapshot.AiBrain.DecisionIntervalTicks);
            if ((tickNumber + snapshot.EntityId) % interval == 0)
            {
                var rng = Xoroshiro128PlusPlus.FromEntityTick(worldSeed, tickNumber, snapshot.EntityId);
                var dirX = (rng.NextDouble() * 2.0) - 1.0;
                var dirZ = (rng.NextDouble() * 2.0) - 1.0;
                var desired = new Vec3d(dirX, 0.0, dirZ).Normalize() * snapshot.AiBrain.WanderSpeed;
                velocity = new Vec3d(desired.X, velocity.Y, desired.Z);
            }
        }

        if (worldReader is not null && physics is not null)
        {
            var step = physics.StepEntity(
                worldReader,
                snapshot.Position,
                velocity,
                snapshot.Dimensions.Width,
                snapshot.Dimensions.Height,
                TickDeltaSeconds);

            var outVel = step.Velocity;
            if (snapshot.HasAi && step.OnGround && step.CollidedHorizontally)
            {
                outVel = new Vec3d(outVel.X, StepJumpVelocity, outVel.Z);
            }

            return new EntityMotionMutation(snapshot.Handle, snapshot.EntityId, step.Position, outVel, step.OnGround);
        }

        var nextPosition = snapshot.Position + (velocity * TickDeltaSeconds);
        return new EntityMotionMutation(snapshot.Handle, snapshot.EntityId, nextPosition, velocity, snapshot.WasOnGround);
    }

    private int CaptureSnapshots(EntitySnapshot[] buffer)
    {
        var index = 0;

        _ecsWorld.Query(
            in AiEntityQuery,
            (
                Entity entity,
                ref EntityIdentity id,
                ref PositionComponent pos,
                ref VelocityComponent vel,
                ref BodyDimensionsComponent dims,
                ref OnGroundComponent ground,
                ref AiBrainComponent brain) =>
            {
                if (index < buffer.Length)
                {
                    buffer[index++] = new EntitySnapshot(
                        entity,
                        id.EntityId,
                        pos.Value,
                        vel.Value,
                        dims,
                        ground.IsOnGround,
                        HasAi: true,
                        brain);
                }
            });

        _ecsWorld.Query(
            in PassiveEntityQuery,
            (
                Entity entity,
                ref EntityIdentity id,
                ref PositionComponent pos,
                ref VelocityComponent vel,
                ref BodyDimensionsComponent dims,
                ref OnGroundComponent ground) =>
            {
                if (index < buffer.Length)
                {
                    buffer[index++] = new EntitySnapshot(
                        entity,
                        id.EntityId,
                        pos.Value,
                        vel.Value,
                        dims,
                        ground.IsOnGround,
                        HasAi: false,
                        default);
                }
            });

        return index;
    }
}
