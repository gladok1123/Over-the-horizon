using System.Numerics;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay.Survival;
using OverHorizon.Physics;

namespace OverHorizon.Gameplay.Vehicles;

/// <summary>
/// Simulates vanilla-style <see cref="MinecartEntity"/> movement along straight, curved, sloped, and Redstone-powered rails,
/// entity bump pushing, player mounting/riding/dismounting, and left-click breaking.
/// </summary>
public sealed class MinecartManager
{
    private const double MaxPoweredRailSpeed = 8.0;
    private const int MaxActiveMinecarts = 48;

    private readonly IBlockRegistry _blocks;
    private readonly List<MinecartEntity> _carts = [];
    private int _nextId = 1;

    /// <summary>Initializes a new instance of the <see cref="MinecartManager"/> class.</summary>
    public MinecartManager(IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        _blocks = blocks;
    }

    /// <summary>Gets all active Minecarts in the world.</summary>
    public IReadOnlyList<MinecartEntity> Minecarts => _carts;

    /// <summary>Gets the Minecart currently ridden by the local player, or <see langword="null"/>.</summary>
    public MinecartEntity? RiddenMinecart
    {
        get
        {
            for (var i = 0; i < _carts.Count; i++)
            {
                if (_carts[i].HasPlayerPassenger) return _carts[i];
            }

            return null;
        }
    }

    /// <summary>Clears all active Minecarts (used on world transitions).</summary>
    public void Clear() => _carts.Clear();

    /// <summary>Places a new <see cref="MinecartEntity"/> onto a rail block if none overlaps <paramref name="railPos"/>.</summary>
    public MinecartEntity? TrySpawnOnRail(BlockPos railPos, Func<BlockPos, BlockState> getBlock)
    {
        ArgumentNullException.ThrowIfNull(getBlock);

        var st = getBlock(railPos);
        var meta = _blocks.GetStateMetadata(st);
        if (!RailPlacementRules.IsRailBlock(meta.BlockKey))
        {
            return null;
        }

        var spawnPos = new Vec3d(railPos.X + 0.5, railPos.Y + 0.0625, railPos.Z + 0.5);
        for (var i = 0; i < _carts.Count; i++)
        {
            if (_carts[i].FeetPosition.DistanceTo(spawnPos) < 0.85)
            {
                return null;
            }
        }

        if (_carts.Count >= MaxActiveMinecarts)
        {
            _carts.RemoveAt(0);
        }

        var shape = RailPlacementRules.GetRailShape(meta);
        var yaw = shape.Contains("east", StringComparison.Ordinal) || shape.Contains("west", StringComparison.Ordinal) ? 90.0f : 0.0f;
        var cart = new MinecartEntity(_nextId++, spawnPos, yaw) { IsOnRail = true };
        _carts.Add(cart);
        return cart;
    }

    /// <summary>Attempts to mount an empty Minecart targeted by the player's crosshair ray.</summary>
    public MinecartEntity? TryMountAlongRay(Vec3d rayOrigin, Vector3 rayDir, double maxReach)
    {
        var hit = RaycastMinecart(rayOrigin, rayDir, maxReach);
        if (hit is null || hit.HasPlayerPassenger)
        {
            return null;
        }

        DismountPlayer(out _);
        hit.HasPlayerPassenger = true;
        return hit;
    }

    /// <summary>Breaks a targeted Minecart along <paramref name="rayDir"/>, returning <see langword="true"/> if removed.</summary>
    public bool TryBreakAlongRay(Vec3d rayOrigin, Vector3 rayDir, double maxReach, out Vec3d brokenPosition)
    {
        var hit = RaycastMinecart(rayOrigin, rayDir, maxReach);
        if (hit is null)
        {
            brokenPosition = Vec3d.Zero;
            return false;
        }

        brokenPosition = hit.FeetPosition;
        hit.HasPlayerPassenger = false;
        _ = _carts.Remove(hit);
        return true;
    }

    /// <summary>Dismounts the local player from any ridden Minecart and computes a safe stand-up position.</summary>
    public bool DismountPlayer(out Vec3d dismountFeetPos)
    {
        dismountFeetPos = Vec3d.Zero;
        var ridden = RiddenMinecart;
        if (ridden is null)
        {
            return false;
        }

        ridden.HasPlayerPassenger = false;
        dismountFeetPos = new Vec3d(ridden.FeetPosition.X, ridden.FeetPosition.Y + 0.35, ridden.FeetPosition.Z);
        return true;
    }

    /// <summary>Advances all Minecarts along rails or ground and applies player bump/rider inputs.</summary>
    public void Step(
        double deltaSeconds,
        IVoxelCollisionReader collisionWorld,
        Func<BlockPos, BlockState> getBlock,
        Vec3d playerFeetPos,
        Vector3 riderLookForward,
        float riderForwardInput)
    {
        ArgumentNullException.ThrowIfNull(collisionWorld);
        ArgumentNullException.ThrowIfNull(getBlock);

        var dt = Math.Clamp(deltaSeconds, 0.001, 0.08);
        for (var i = 0; i < _carts.Count; i++)
        {
            var cart = _carts[i];
            cart.HurtWobbleTimer = MathF.Max(0.0f, cart.HurtWobbleTimer - (float)dt);

            if (!cart.HasPlayerPassenger)
            {
                ApplyEntityBumpPush(cart, playerFeetPos);
            }

            if (TryFindSupportingRail(cart.FeetPosition, getBlock, out var railPos, out var railMeta))
            {
                StepOnRail(cart, railPos, railMeta, getBlock, riderLookForward, riderForwardInput, dt);
            }
            else
            {
                StepDerailed(cart, collisionWorld, dt);
            }
        }
    }

    private static void ApplyEntityBumpPush(MinecartEntity cart, Vec3d playerFeetPos)
    {
        var dx = cart.FeetPosition.X - playerFeetPos.X;
        var dy = Math.Abs(cart.FeetPosition.Y - playerFeetPos.Y);
        var dz = cart.FeetPosition.Z - playerFeetPos.Z;
        var distSq = (dx * dx) + (dz * dz);
        if (dy <= 1.25 && distSq is > 1e-5 and < 0.72)
        {
            var dist = Math.Sqrt(distSq);
            var pushStrength = (0.85 - dist) * 5.2;
            cart.Velocity = new Vec3d(cart.Velocity.X + ((dx / dist) * pushStrength), cart.Velocity.Y, cart.Velocity.Z + ((dz / dist) * pushStrength));
        }
    }

    private void StepOnRail(
        MinecartEntity cart,
        BlockPos railPos,
        BlockStateMetadata railMeta,
        Func<BlockPos, BlockState> getBlock,
        Vector3 riderLookForward,
        float riderForwardInput,
        double dt)
    {
        cart.IsOnRail = true;
        var shape = RailPlacementRules.GetRailShape(railMeta);
        var isPoweredBlock = railMeta.BlockKey == BuiltInBlocks.PoweredRail;
        var isEnergized = isPoweredBlock && railMeta.Properties.TryGetValue("powered", out var pv) && string.Equals(pv, "true", StringComparison.OrdinalIgnoreCase);

        var vx = cart.Velocity.X;
        var vz = cart.Velocity.Z;

        // Rider W/S steering input nudges the cart smoothly along the rider's look direction!
        if (cart.HasPlayerPassenger && MathF.Abs(riderForwardInput) > 0.05f)
        {
            var look2D = new Vec3d(riderLookForward.X, 0.0, riderLookForward.Z).Normalize();
            vx += look2D.X * riderForwardInput * 4.8 * dt;
            vz += look2D.Z * riderForwardInput * 4.8 * dt;
        }

        // Slope gravity acceleration along ascending rails!
        const double slopeGravity = 9.5;
        switch (shape)
        {
            case "ascending_east": vx -= slopeGravity * dt; break;
            case "ascending_west": vx += slopeGravity * dt; break;
            case "ascending_north": vz += slopeGravity * dt; break;
            case "ascending_south": vz -= slopeGravity * dt; break;
        }

        // Constrain velocity vector onto the rail's tangent / corner curve!
        ConstrainVelocityToShape(shape, cart.FeetPosition, railPos, ref vx, ref vz);

        var speed = Math.Sqrt((vx * vx) + (vz * vz));
        if (isPoweredBlock)
        {
            if (isEnergized)
            {
                if (speed > 0.04)
                {
                    var boosted = Math.Min(MaxPoweredRailSpeed, speed + (14.0 * dt));
                    vx = (vx / speed) * boosted;
                    vz = (vz / speed) * boosted;
                }
                else
                {
                    // Vanilla kickstart: if backed against a solid block, launch away from the wall!
                    var (endA, endB) = RailPlacementRules.GetEndsForShape(shape);
                    if (BlockAttachmentAndShapeRules.IsFullSupportBlock(_blocks, getBlock(railPos.Offset(endA))))
                    {
                        var n = endB.Normal();
                        vx = n.X * 3.8;
                        vz = n.Z * 3.8;
                    }
                    else if (BlockAttachmentAndShapeRules.IsFullSupportBlock(_blocks, getBlock(railPos.Offset(endB))))
                    {
                        var n = endA.Normal();
                        vx = n.X * 3.8;
                        vz = n.Z * 3.8;
                    }
                }
            }
            else
            {
                // Unpowered PoweredRail acts as a crisp vanilla brake!
                var brake = Math.Pow(0.06, dt * 4.0);
                vx *= brake;
                vz *= brake;
                if (speed < 0.08)
                {
                    vx = 0.0;
                    vz = 0.0;
                }
            }
        }
        else
        {
            var drag = cart.HasPlayerPassenger ? Math.Pow(0.995, dt * 20.0) : Math.Pow(0.965, dt * 20.0);
            vx *= drag;
            vz *= drag;
        }

        // Clamp max speed and integrate along rail!
        speed = Math.Sqrt((vx * vx) + (vz * vz));
        if (speed > MaxPoweredRailSpeed)
        {
            vx = (vx / speed) * MaxPoweredRailSpeed;
            vz = (vz / speed) * MaxPoweredRailSpeed;
        }

        var nextX = cart.FeetPosition.X + (vx * dt);
        var nextZ = cart.FeetPosition.Z + (vz * dt);
        SnapPositionAndHeightToRail(railPos, shape, ref nextX, ref nextZ, out var nextY, out var slopePitch);

        // Stop cleanly against solid wall blocks at end of track (and bounce slightly if fast)!
        var checkBlock = new BlockPos((int)Math.Floor(nextX), (int)Math.Floor(nextY + 0.25), (int)Math.Floor(nextZ));
        var hitMeta = _blocks.GetStateMetadata(getBlock(checkBlock));
        if (hitMeta.IsSolid && !RailPlacementRules.IsRailBlock(hitMeta.BlockKey))
        {
            nextX = cart.FeetPosition.X;
            nextZ = cart.FeetPosition.Z;
            vx = 0.0;
            vz = 0.0;
        }

        cart.FeetPosition = new Vec3d(nextX, nextY, nextZ);
        cart.Velocity = new Vec3d(vx, 0.0, vz);
        cart.PitchDegrees = slopePitch;
        if ((vx * vx) + (vz * vz) > 1e-4)
        {
            cart.YawDegrees = (float)(Math.Atan2(vx, vz) * (180.0 / Math.PI));
        }
    }

    private static void ConstrainVelocityToShape(string shape, Vec3d pos, BlockPos railPos, ref double vx, ref double vz)
    {
        switch (shape)
        {
            case "north_south" or "ascending_north" or "ascending_south":
                vx = 0.0;
                break;
            case "east_west" or "ascending_east" or "ascending_west":
                vz = 0.0;
                break;
            case "south_east":
                TurnCorner(pos.X - railPos.X, pos.Z - railPos.Z, cornerSignX: 1.0, cornerSignZ: 1.0, ref vx, ref vz);
                break;
            case "south_west":
                TurnCorner(pos.X - railPos.X, pos.Z - railPos.Z, cornerSignX: -1.0, cornerSignZ: 1.0, ref vx, ref vz);
                break;
            case "north_west":
                TurnCorner(pos.X - railPos.X, pos.Z - railPos.Z, cornerSignX: -1.0, cornerSignZ: -1.0, ref vx, ref vz);
                break;
            case "north_east":
                TurnCorner(pos.X - railPos.X, pos.Z - railPos.Z, cornerSignX: 1.0, cornerSignZ: -1.0, ref vx, ref vz);
                break;
        }
    }

    private static void TurnCorner(double lx, double lz, double cornerSignX, double cornerSignZ, ref double vx, ref double vz)
    {
        var speed = Math.Sqrt((vx * vx) + (vz * vz));
        if (speed < 1e-5) return;

        // Entering along Z toward the corner -> deflect smoothly into cornerSignX along X (and vice versa)!
        if (Math.Abs(vz) >= Math.Abs(vx))
        {
            if (vz * cornerSignZ < 0.0)
            {
                vx = cornerSignX * speed * 0.70710678;
                vz = cornerSignZ * speed * 0.70710678;
            }
        }
        else if (vx * cornerSignX < 0.0)
        {
            vx = cornerSignX * speed * 0.70710678;
            vz = cornerSignZ * speed * 0.70710678;
        }

        _ = lx;
        _ = lz;
    }

    private static void SnapPositionAndHeightToRail(
        BlockPos railPos,
        string shape,
        ref double x,
        ref double z,
        out double y,
        out float pitchDeg)
    {
        pitchDeg = 0.0f;
        var baseY = railPos.Y + 0.0625;
        var u = Math.Clamp(x - railPos.X, 0.0, 1.0);
        var v = Math.Clamp(z - railPos.Z, 0.0, 1.0);

        switch (shape)
        {
            case "north_south":
                x = railPos.X + 0.5;
                y = baseY;
                break;
            case "east_west":
                z = railPos.Z + 0.5;
                y = baseY;
                break;
            case "ascending_east":
                z = railPos.Z + 0.5;
                y = baseY + u;
                pitchDeg = -35.0f;
                break;
            case "ascending_west":
                z = railPos.Z + 0.5;
                y = baseY + (1.0 - u);
                pitchDeg = 35.0f;
                break;
            case "ascending_north":
                x = railPos.X + 0.5;
                y = baseY + (1.0 - v);
                pitchDeg = 35.0f;
                break;
            case "ascending_south":
                x = railPos.X + 0.5;
                y = baseY + v;
                pitchDeg = -35.0f;
                break;
            default:
                y = baseY;
                break;
        }
    }

    private static void StepDerailed(MinecartEntity cart, IVoxelCollisionReader world, double dt)
    {
        cart.IsOnRail = false;
        cart.PitchDegrees = 0.0f;
        var step = KinematicsIntegrator.StepEntity(world, cart.FeetPosition, cart.Velocity, MinecartEntity.Width, MinecartEntity.Height, dt);
        var damp = step.OnGround ? 0.55 : 0.96;
        cart.FeetPosition = step.Position;
        cart.Velocity = new Vec3d(step.Velocity.X * damp, step.Velocity.Y, step.Velocity.Z * damp);
    }

    private bool TryFindSupportingRail(Vec3d pos, Func<BlockPos, BlockState> getBlock, out BlockPos railPos, out BlockStateMetadata railMeta)
    {
        var bx = (int)Math.Floor(pos.X);
        var by = (int)Math.Floor(pos.Y + 0.20);
        var bz = (int)Math.Floor(pos.Z);

        var p0 = new BlockPos(bx, by, bz);
        var m0 = _blocks.GetStateMetadata(getBlock(p0));
        if (RailPlacementRules.IsRailBlock(m0.BlockKey))
        {
            railPos = p0;
            railMeta = m0;
            return true;
        }

        var pDown = new BlockPos(bx, by - 1, bz);
        var mDown = _blocks.GetStateMetadata(getBlock(pDown));
        if (RailPlacementRules.IsRailBlock(mDown.BlockKey) &&
            RailPlacementRules.GetRailShape(mDown).StartsWith("ascending_", StringComparison.Ordinal))
        {
            railPos = pDown;
            railMeta = mDown;
            return true;
        }

        railPos = default;
        railMeta = default!;
        return false;
    }

    private MinecartEntity? RaycastMinecart(Vec3d origin, Vector3 dir, double maxReach)
    {
        MinecartEntity? best = null;
        var bestT = maxReach + 0.01;
        for (var i = 0; i < _carts.Count; i++)
        {
            var c = _carts[i];
            if (IntersectRayBox(origin, dir, maxReach, c.Hitbox.Inflate(0.15, 0.15, 0.15), out var t) && t < bestT)
            {
                bestT = t;
                best = c;
            }
        }

        return best;
    }

    private static bool IntersectRayBox(Vec3d origin, Vector3 dir, double maxDist, BoundingBox box, out double hitT)
    {
        hitT = 0.0;
        var tMin = 0.0;
        var tMax = maxDist;
        if (!Slab(origin.X, dir.X, box.MinX, box.MaxX, ref tMin, ref tMax) ||
            !Slab(origin.Y, dir.Y, box.MinY, box.MaxY, ref tMin, ref tMax) ||
            !Slab(origin.Z, dir.Z, box.MinZ, box.MaxZ, ref tMin, ref tMax))
        {
            return false;
        }

        hitT = Math.Max(0.0, tMin);
        return hitT <= maxDist;
    }

    private static bool Slab(double ro, double rd, double bMin, double bMax, ref double tMin, ref double tMax)
    {
        if (Math.Abs(rd) < 1e-8) return ro >= bMin && ro <= bMax;
        var inv = 1.0 / rd;
        var t1 = (bMin - ro) * inv;
        var t2 = (bMax - ro) * inv;
        if (t1 > t2) (t1, t2) = (t2, t1);
        tMin = Math.Max(tMin, t1);
        tMax = Math.Min(tMax, t2);
        return tMin <= tMax;
    }
}
