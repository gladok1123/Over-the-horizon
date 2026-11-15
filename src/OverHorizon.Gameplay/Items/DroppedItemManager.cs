using OverHorizon.Core;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay.Inventory;
using OverHorizon.Physics;

namespace OverHorizon.Gameplay.Items;

/// <summary>
/// Manages 3D <see cref="DroppedItemEntity"/> instances across Singleplayer and Multiplayer with gravity, bounce,
/// water floatation, stack merging, magnetic player pickup, and snapshot synchronization.
/// </summary>
public sealed class DroppedItemManager
{
    private const int MaxDroppedEntities = 192;
    private const float DespawnSeconds = 300.0f;
    private const double Gravity = 18.0;
    private const double HalfWidth = 0.15;
    private const double Height = 0.30;
    private const double SpatialCellInvSize = 0.5; // 2.0m Spatial Hash Grid cells

    private readonly List<DroppedItemEntity> _items = [];
    private readonly Dictionary<long, int> _spatialHeadByCell = new(64);
    private int[] _spatialNext = new int[MaxDroppedEntities];
    private int _nextId = 1;
    private uint _rng = 0x5A17E903U;

    /// <summary>Gets all active 3D dropped item entities.</summary>
    public IReadOnlyList<DroppedItemEntity> Items => _items;

    /// <summary>Clears all active dropped items.</summary>
    public void Clear() => _items.Clear();

    /// <summary>Spawns a dropped item at the center of a broken or detached voxel block with a gentle random pop.</summary>
    /// <param name="blockPos">Origin voxel block position.</param>
    /// <param name="stack">Dropped item stack.</param>
    public DroppedItemEntity? SpawnBlockDrop(BlockPos blockPos, ItemStack stack)
    {
        if (stack.IsEmpty) return null;
        var angle = ((NextRandom() & 0xFFU) / 255.0) * Math.Tau;
        var speed = 0.85 + (((NextRandom() >> 8) & 0xFFU) / 255.0 * 0.95);
        var pos = new Vec3d(
            blockPos.X + 0.35 + (((NextRandom() >> 16) & 0xFFU) / 255.0 * 0.30),
            blockPos.Y + 0.22,
            blockPos.Z + 0.35 + (((NextRandom() >> 24) & 0xFFU) / 255.0 * 0.30));
        var vel = new Vec3d(Math.Cos(angle) * speed, 2.75, Math.Sin(angle) * speed);
        return AddEntity(new DroppedItemEntity(_nextId++, stack, pos, vel, pickupDelaySeconds: 0.42f));
    }

    /// <summary>Spawns loot or death-dropped items scattered around an entity position.</summary>
    /// <param name="origin">World-space origin.</param>
    /// <param name="stack">Item stack to spawn.</param>
    /// <param name="pickupDelaySeconds">Initial pickup delay in seconds.</param>
    public DroppedItemEntity? SpawnScatterDrop(Vec3d origin, ItemStack stack, float pickupDelaySeconds = 0.55f)
    {
        if (stack.IsEmpty) return null;
        var angle = ((NextRandom() & 0xFFU) / 255.0) * Math.Tau;
        var speed = 1.4 + (((NextRandom() >> 8) & 0xFFU) / 255.0 * 2.1);
        var vel = new Vec3d(Math.Cos(angle) * speed, 3.2, Math.Sin(angle) * speed);
        return AddEntity(new DroppedItemEntity(_nextId++, stack, origin + new Vec3d(0.0, 0.35, 0.0), vel, pickupDelaySeconds));
    }

    /// <summary>Throws an item stack forward from a player's eye level (pressing <c>Q</c> or dropping from GUI).</summary>
    /// <param name="eyePos">Player eye position.</param>
    /// <param name="forward">Normalized look direction.</param>
    /// <param name="stack">Thrown item stack.</param>
    public DroppedItemEntity? SpawnPlayerThrow(Vec3d eyePos, System.Numerics.Vector3 forward, ItemStack stack)
    {
        if (stack.IsEmpty) return null;
        var dir = new Vec3d(forward.X, forward.Y, forward.Z).Normalize();
        var vel = (dir * 6.8) + new Vec3d(0.0, 1.65, 0.0);
        return AddEntity(new DroppedItemEntity(_nextId++, stack, eyePos + new Vec3d(0.0, -0.26, 0.0) + (dir * 0.38), vel, pickupDelaySeconds: 1.35f));
    }

    /// <summary>Replaces client-side mirrored drops from an authoritative multiplayer server snapshot.</summary>
    /// <param name="snapshots">Server snapshot tuples.</param>
    public void ReplaceFromServerSnapshots(ReadOnlySpan<(int Id, ushort ItemId, int Count, Vec3d Position, Vec3d Velocity)> snapshots)
    {
        _items.Clear();
        for (var i = 0; i < snapshots.Length; i++)
        {
            var s = snapshots[i];
            if (s.ItemId == 0 || s.Count <= 0) continue;
            _items.Add(new DroppedItemEntity(s.Id, new ItemStack(s.ItemId, s.Count), s.Position, s.Velocity, pickupDelaySeconds: 0.0f));
        }
    }

    /// <summary>
    /// Steps physics, voxel collision, water buoyancy, stack merging, and optional local-player magnetic pickup.
    /// </summary>
    /// <returns>Number of distinct item stacks collected into <paramref name="collectorInventory"/> this step.</returns>
    public int Step(
        double deltaSeconds,
        IVoxelCollisionReader collisionWorld,
        IRegistry<ItemDefinition> itemRegistry,
        PlayerInventory? collectorInventory = null,
        Vec3d collectorFeetPos = default,
        GameMode collectorMode = GameMode.Survival)
    {
        ArgumentNullException.ThrowIfNull(collisionWorld);
        ArgumentNullException.ThrowIfNull(itemRegistry);

        var dt = Math.Clamp(deltaSeconds, 0.001, 0.08);
        var collectedCount = 0;
        var canCollect = collectorInventory is not null && collectorMode != GameMode.Spectator;
        var magnetTarget = collectorFeetPos + new Vec3d(0.0, 0.65, 0.0);

        for (var i = _items.Count - 1; i >= 0; i--)
        {
            var entity = _items[i];
            entity.AgeSeconds += (float)dt;
            entity.PickupDelaySeconds = MathF.Max(0.0f, entity.PickupDelaySeconds - (float)dt);

            if (entity.Stack.IsEmpty || entity.AgeSeconds >= DespawnSeconds || entity.Position.Y < EngineConstants.WorldMinY - 32)
            {
                _items.RemoveAt(i);
                continue;
            }

            // Magnetic pull & pickup check
            if (canCollect && entity.PickupDelaySeconds <= 0.0f)
            {
                var toPlayer = magnetTarget - entity.Position;
                var distSq = toPlayer.LengthSquared;
                if (distSq <= 1.25 * 1.25)
                {
                    var remainder = collectorInventory!.AddItem(entity.Stack, itemRegistry);
                    if (remainder.Count < entity.Stack.Count)
                    {
                        collectedCount++;
                    }

                    if (remainder.IsEmpty)
                    {
                        _items.RemoveAt(i);
                        continue;
                    }

                    entity.Stack = remainder;
                }
                else if (distSq <= 2.35 * 2.35)
                {
                    var pull = toPlayer.Normalize() * (11.5 * dt);
                    entity.Velocity += pull;
                }
            }

            StepSingleDropPhysics(entity, dt, collisionWorld);
        }

        MergeNearbyStacks(itemRegistry);
        return collectedCount;
    }

    /// <summary>Attempts to collect nearby ready drops into an arbitrary multiplayer player's position, invoking <paramref name="tryAccept"/>.</summary>
    public void CollectForMultiplayerPlayer(
        Vec3d playerFeet,
        GameMode mode,
        Func<ItemStack, ItemStack> tryAccept,
        List<(int EntityId, ItemStack Picked)> pickedOut)
    {
        if (mode == GameMode.Spectator) return;
        var center = playerFeet + new Vec3d(0.0, 0.65, 0.0);
        for (var i = _items.Count - 1; i >= 0; i--)
        {
            var e = _items[i];
            if (e.PickupDelaySeconds > 0.0f || e.Stack.IsEmpty) continue;
            if ((center - e.Position).LengthSquared > 1.55 * 1.55) continue;

            var before = e.Stack;
            var rem = tryAccept(before);
            var taken = before.Count - rem.Count;
            if (taken > 0)
            {
                pickedOut.Add((e.Id, new ItemStack(before.ItemId, taken)));
            }

            if (rem.IsEmpty) _items.RemoveAt(i);
            else e.Stack = rem;
        }
    }

    private DroppedItemEntity AddEntity(DroppedItemEntity entity)
    {
        if (_items.Count >= MaxDroppedEntities)
        {
            _items.RemoveAt(0);
        }

        _items.Add(entity);
        return entity;
    }

    private static void StepSingleDropPhysics(DroppedItemEntity e, double dt, IVoxelCollisionReader world)
    {
        var bp = e.Position.ToBlockPos();
        if (!world.IsColumnLoaded(bp))
        {
            return;
        }

        var inWater = world.GetFluidLevelOrNegative(bp) >= 0;
        var vy = e.Velocity.Y;
        if (inWater)
        {
            vy = Math.Min(1.6, (vy * 0.85) + (5.5 * dt));
        }
        else
        {
            vy = Math.Max(-32.0, vy - (Gravity * dt));
        }

        var desiredVel = new Vec3d(e.Velocity.X, vy, e.Velocity.Z);
        var box = new BoundingBox(
            e.Position.X - HalfWidth,
            e.Position.Y,
            e.Position.Z - HalfWidth,
            e.Position.X + HalfWidth,
            e.Position.Y + Height,
            e.Position.Z + HalfWidth);

        var reqMove = desiredVel * dt;
        var sweep = VoxelSweptCollider.MoveAndCollide(world, box, reqMove);
        e.Position = new Vec3d((sweep.Box.MinX + sweep.Box.MaxX) * 0.5, sweep.Box.MinY, (sweep.Box.MinZ + sweep.Box.MaxZ) * 0.5);
        e.OnGround = sweep.OnGround;

        // Framerate-independent exponential air/ground/water drag so 30Hz, 60Hz, and 240Hz throw identical ballistic arcs!
        var dragXZ = Math.Pow(0.98, dt * 20.0);
        if (e.OnGround)
        {
            dragXZ = Math.Pow(0.58, dt * 20.0);
        }
        else if (inWater)
        {
            dragXZ = Math.Pow(0.84, dt * 20.0);
        }

        var hitX = Math.Abs(sweep.ActualMove.X - reqMove.X) > 1e-5;
        var hitZ = Math.Abs(sweep.ActualMove.Z - reqMove.Z) > 1e-5;
        var hitY = Math.Abs(sweep.ActualMove.Y - reqMove.Y) > 1e-5;
        var nextVx = hitX ? -desiredVel.X * 0.25 : desiredVel.X * dragXZ;
        var nextVz = hitZ ? -desiredVel.Z * 0.25 : desiredVel.Z * dragXZ;
        var nextVy = desiredVel.Y;
        if (hitY)
        {
            nextVy = desiredVel.Y < -1.8 ? -desiredVel.Y * 0.28 : 0.0;
        }

        e.Velocity = new Vec3d(nextVx, nextVy, nextVz);
    }

    private static long SpatialCellKey(int cx, int cy, int cz) =>
        (((long)cx & 0x1FFFFFL) << 42) | (((long)cy & 0x1FFFFFL) << 21) | ((long)cz & 0x1FFFFFL);

    private void MergeNearbyStacks(IRegistry<ItemDefinition> itemRegistry)
    {
        var count = _items.Count;
        if (count < 2)
        {
            return;
        }

        if (_spatialNext.Length < count)
        {
            _spatialNext = new int[Math.Max(count, _spatialNext.Length * 2)];
        }

        // O(N) zero-allocation intrusive Spatial Hash Grid replaces O(N^2) all-pairs distance checks!
        _spatialHeadByCell.Clear();
        for (var i = 0; i < count; i++)
        {
            var p = _items[i].Position;
            var key = SpatialCellKey(
                (int)Math.Floor(p.X * SpatialCellInvSize),
                (int)Math.Floor(p.Y * SpatialCellInvSize),
                (int)Math.Floor(p.Z * SpatialCellInvSize));
            _spatialNext[i] = _spatialHeadByCell.TryGetValue(key, out var head) ? head : -1;
            _spatialHeadByCell[key] = i;
        }

        var anyMergedToEmpty = false;
        for (var i = 0; i < count; i++)
        {
            var a = _items[i];
            if (a.Stack.IsEmpty) continue;
            var maxStack = itemRegistry.TryGetById(a.Stack.ItemId, out var def) ? Math.Max(1, def.MaxStackSize) : 64;
            if (maxStack <= 1 || a.Stack.Count >= maxStack) continue;

            var cx = (int)Math.Floor(a.Position.X * SpatialCellInvSize);
            var cy = (int)Math.Floor(a.Position.Y * SpatialCellInvSize);
            var cz = (int)Math.Floor(a.Position.Z * SpatialCellInvSize);

            for (var dy = -1; dy <= 1 && a.Stack.Count < maxStack; dy++)
            {
                for (var dz = -1; dz <= 1 && a.Stack.Count < maxStack; dz++)
                {
                    for (var dx = -1; dx <= 1 && a.Stack.Count < maxStack; dx++)
                    {
                        if (!_spatialHeadByCell.TryGetValue(SpatialCellKey(cx + dx, cy + dy, cz + dz), out var j))
                        {
                            continue;
                        }

                        while (j >= 0 && a.Stack.Count < maxStack)
                        {
                            if (j > i)
                            {
                                var b = _items[j];
                                if (!b.Stack.IsEmpty &&
                                    a.Stack.CanMergeWith(b.Stack) &&
                                    (a.Position - b.Position).LengthSquared <= 1.15 * 1.15)
                                {
                                    var transfer = Math.Min(maxStack - a.Stack.Count, b.Stack.Count);
                                    a.Stack = a.Stack.WithCount(a.Stack.Count + transfer);
                                    b.Stack = b.Stack.WithCount(b.Stack.Count - transfer);
                                    if (b.Stack.IsEmpty)
                                    {
                                        anyMergedToEmpty = true;
                                    }
                                }
                            }

                            j = _spatialNext[j];
                        }
                    }
                }
            }
        }

        if (anyMergedToEmpty)
        {
            for (var i = _items.Count - 1; i >= 0; i--)
            {
                if (_items[i].Stack.IsEmpty)
                {
                    _items.RemoveAt(i);
                }
            }
        }
    }

    private uint NextRandom()
    {
        _rng ^= _rng << 13;
        _rng ^= _rng >> 17;
        _rng ^= _rng << 5;
        return _rng;
    }
}
