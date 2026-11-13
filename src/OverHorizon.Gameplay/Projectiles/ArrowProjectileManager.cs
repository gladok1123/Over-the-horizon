using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay.Mobs;
using OverHorizon.Gameplay.Survival;
using OverHorizon.Physics;

namespace OverHorizon.Gameplay.Projectiles;

/// <summary>
/// Simulates 3D ballistic <see cref="ArrowEntity"/> trajectories with gravity, air &amp; water drag,
/// swept ray-vs-voxel/mob/player collision, tip shake on impact, block-break drop release, and ground arrow recovery.
/// </summary>
public sealed class ArrowProjectileManager
{
    /// <summary>Full bow charge duration in seconds.</summary>
    public const float FullDrawSeconds = 1.0f;

    /// <summary>Maximum launch speed of a fully charged bow arrow in blocks/second.</summary>
    public const double MaxLaunchSpeed = 30.0;

    private const int MaxActiveArrows = 64;
    private const float MaxArrowLifetimeSeconds = 60.0f;
    private const double ArrowGravity = 20.0;

    private readonly IBlockRegistry _blocks;
    private readonly List<ArrowEntity> _arrows = [];
    private int _nextId = 1;

    /// <summary>Initializes a new instance of the <see cref="ArrowProjectileManager"/> class.</summary>
    public ArrowProjectileManager(IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        _blocks = blocks;
    }

    /// <summary>Gets all currently active arrows in the world.</summary>
    public IReadOnlyList<ArrowEntity> Arrows => _arrows;

    /// <summary>Clears all active arrows (used on world transitions).</summary>
    public void Clear() => _arrows.Clear();

    /// <summary>Replaces client-side mirrored arrows from an authoritative multiplayer server snapshot.</summary>
    public void ReplaceFromServerSnapshots(
        ReadOnlySpan<(int Id, Vec3d Pos, float Yaw, float Pitch, float Shake, byte Flags)> snapshots)
    {
        _arrows.Clear();
        for (var i = 0; i < snapshots.Length; i++)
        {
            var s = snapshots[i];
            var a = new ArrowEntity(s.Id, s.Pos, Vec3d.Zero, baseDamage: 4.0f, isCritical: (s.Flags & 2) != 0, firedByPlayer: true, canBePickedUp: false)
            {
                YawDegrees = s.Yaw,
                PitchDegrees = s.Pitch,
                ShakeTimer = s.Shake,
                IsStuckInBlock = (s.Flags & 1) != 0,
            };
            _arrows.Add(a);
        }
    }

    /// <summary>Computes vanilla nonlinear bow charge (<c>0.0..1.0</c>) from draw duration in seconds.</summary>
    public static float ComputeChargeNormalized(float drawSeconds)
    {
        if (drawSeconds <= 0.0f) return 0.0f;
        var f = Math.Clamp(drawSeconds / FullDrawSeconds, 0.0f, 1.0f);
        return Math.Clamp(((f * f) + (f * 2.0f)) / 3.0f, 0.0f, 1.0f);
    }

    /// <summary>Spawns a player-fired arrow scaled by normalized bow charge (<c>0.1..1.0</c>).</summary>
    public ArrowEntity SpawnPlayerArrow(Vec3d eyePos, System.Numerics.Vector3 aimDir, float chargeNormalized, bool canBePickedUp)
    {
        var charge = Math.Clamp(chargeNormalized, 0.12f, 1.0f);
        var dir = new Vec3d(aimDir.X, aimDir.Y, aimDir.Z).Normalize();
        var vel = dir * (charge * MaxLaunchSpeed);
        var isCrit = charge >= 0.96f;
        var dmg = (2.0f + (charge * 7.0f)) + (isCrit ? 1.5f : 0.0f);
        return AddArrow(new ArrowEntity(_nextId++, eyePos + (dir * 0.35), vel, dmg, isCrit, firedByPlayer: true, canBePickedUp: canBePickedUp));
    }

    /// <summary>Spawns a hostile Skeleton-fired ballistic arrow.</summary>
    public ArrowEntity SpawnSkeletonArrow(Vec3d origin, Vec3d launchVelocity) =>
        AddArrow(new ArrowEntity(_nextId++, origin, launchVelocity, baseDamage: 3.5f, isCritical: false, firedByPlayer: false, canBePickedUp: false));

    /// <summary>
    /// Advances all arrows, resolving voxel sticking, mob hits, player hits, and ground arrow pickups.
    /// </summary>
    public float Step(
        double deltaSeconds,
        IVoxelCollisionReader collisionWorld,
        Func<BlockPos, BlockState> getBlock,
        WorldMobManager mobManager,
        IRegistry<ItemDefinition> items,
        Vec3d playerFeetPos,
        GameMode playerMode,
        List<ItemStack> collectedDropsAndPickups,
        List<Vec3d> arrowImpactSoundPositions,
        bool isPlayerBlockingShield = false,
        System.Numerics.Vector3 playerLookForward = default,
        List<Vec3d>? shieldDeflectedPositions = null)
    {
        ArgumentNullException.ThrowIfNull(collisionWorld);
        ArgumentNullException.ThrowIfNull(getBlock);
        ArgumentNullException.ThrowIfNull(mobManager);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(collectedDropsAndPickups);
        ArgumentNullException.ThrowIfNull(arrowImpactSoundPositions);

        var dt = Math.Clamp(deltaSeconds, 0.001, 0.08);
        var damageToPlayer = 0.0f;
        var playerBox = BoundingBox.FromEntityFeet(playerFeetPos, 0.6, 1.8);
        var arrowItemId = (ushort)Math.Max(0, items.GetId(BuiltInItems.Arrow));

        for (var i = _arrows.Count - 1; i >= 0; i--)
        {
            var arrow = _arrows[i];
            arrow.AgeSeconds += (float)dt;
            arrow.ShakeTimer = MathF.Max(0.0f, arrow.ShakeTimer - (float)dt);

            if (arrow.AgeSeconds >= MaxArrowLifetimeSeconds || arrow.Position.Y < EngineConstants.WorldMinY - 16)
            {
                _arrows.RemoveAt(i);
                continue;
            }

            if (arrow.IsStuckInBlock)
            {
                // Release arrow back into flight if its supporting voxel was mined or blown up!
                var stuckState = getBlock(arrow.StuckBlockPos);
                if (stuckState.IsAir || !_blocks.GetStateMetadata(stuckState).IsSolid)
                {
                    arrow.IsStuckInBlock = false;
                    arrow.Velocity = new Vec3d(0.0, -0.5, 0.0);
                }
                else if (arrow.CanBePickedUp && arrow.AgeSeconds >= 0.35f && playerMode == GameMode.Survival &&
                         playerBox.Inflate(0.75, 0.55, 0.75).Contains(arrow.Position))
                {
                    if (arrowItemId > 0)
                    {
                        collectedDropsAndPickups.Add(new ItemStack(arrowItemId, 1));
                    }

                    _arrows.RemoveAt(i);
                }

                continue;
            }

            var start = arrow.Position;
            var end = start + (arrow.Velocity * dt);

            // 1. Check mob hit (player-fired arrows or friendly-fire arrows after 0.12s)
            if ((arrow.FiredByPlayer || arrow.AgeSeconds > 0.20f) &&
                mobManager.TryHitMobAlongSegment(start, end, arrow.BaseDamage, arrow.Velocity, items, out _, out var mobDrops))
            {
                for (var d = 0; d < mobDrops.Count; d++)
                {
                    collectedDropsAndPickups.Add(mobDrops[d]);
                }

                arrowImpactSoundPositions.Add(end);
                _arrows.RemoveAt(i);
                continue;
            }

            // 2. Check player hit (Skeleton arrows or self-arcing arrows after 0.45s in Survival)
            if (playerMode == GameMode.Survival && (!arrow.FiredByPlayer || arrow.AgeSeconds >= 0.45f) &&
                SegmentIntersectsBox(start, end, playerBox.Inflate(0.12, 0.08, 0.12)))
            {
                if (WorldMobManager.IsFrontalShieldBlock(isPlayerBlockingShield, playerLookForward, playerFeetPos, start))
                {
                    shieldDeflectedPositions?.Add(end);
                    arrow.Velocity = new Vec3d(-arrow.Velocity.X * 0.35, 2.2, -arrow.Velocity.Z * 0.35);
                    arrow.FiredByPlayer = true;
                    arrow.SyncAnglesFromVelocity();
                    continue;
                }

                damageToPlayer += arrow.BaseDamage;
                arrowImpactSoundPositions.Add(end);
                _arrows.RemoveAt(i);
                continue;
            }

            // 3. Swept voxel collision along [start -> end]
            if (TryTraceSolidBlockHit(start, end, getBlock, out var hitBlockPos, out var hitPoint))
            {
                arrow.Position = hitPoint;
                arrow.Velocity = Vec3d.Zero;
                arrow.IsStuckInBlock = true;
                arrow.StuckBlockPos = hitBlockPos;
                arrow.ShakeTimer = 0.35f;
                arrowImpactSoundPositions.Add(hitPoint);
                continue;
            }

            // 4. Advance free ballistic flight (gravity + air/water drag + velocity-aligned pitch/yaw!)
            arrow.Position = end;
            var inWater = collisionWorld.IsColumnLoaded(end.ToBlockPos()) && collisionWorld.GetFluidLevelOrNegative(end.ToBlockPos()) >= 0;
            var dragPerTick = inWater ? 0.60 : 0.99;
            var drag = Math.Pow(dragPerTick, dt * 20.0);
            var nextVy = (arrow.Velocity.Y * drag) - (ArrowGravity * dt);
            arrow.Velocity = new Vec3d(arrow.Velocity.X * drag, Math.Max(-55.0, nextVy), arrow.Velocity.Z * drag);
            arrow.SyncAnglesFromVelocity();
        }

        return damageToPlayer;
    }

    private ArrowEntity AddArrow(ArrowEntity arrow)
    {
        if (_arrows.Count >= MaxActiveArrows)
        {
            _arrows.RemoveAt(0);
        }

        _arrows.Add(arrow);
        return arrow;
    }

    private bool TryTraceSolidBlockHit(Vec3d start, Vec3d end, Func<BlockPos, BlockState> getBlock, out BlockPos hitBlock, out Vec3d hitPoint)
    {
        var delta = end - start;
        var dist = delta.Length;
        var steps = Math.Max(1, (int)Math.Ceiling(dist / 0.20));
        for (var s = 1; s <= steps; s++)
        {
            var t = (double)s / steps;
            var sample = start + (delta * t);
            var bp = sample.ToBlockPos();
            if (bp.Y < EngineConstants.WorldMinY || bp.Y >= EngineConstants.WorldMaxY) continue;

            var st = getBlock(bp);
            if (st.IsAir) continue;
            var meta = _blocks.GetStateMetadata(st);
            if (!meta.IsSolid) continue;

            if (BlockAttachmentAndShapeRules.TryGetLocalSelectionBox(_blocks, st, out var localBox))
            {
                var worldBox = new BoundingBox(bp.X + localBox.MinX, bp.Y + localBox.MinY, bp.Z + localBox.MinZ, bp.X + localBox.MaxX, bp.Y + localBox.MaxY, bp.Z + localBox.MaxZ);
                if (worldBox.Contains(sample))
                {
                    hitBlock = bp;
                    hitPoint = start + (delta * Math.Max(0.0, t - (0.06 / Math.Max(0.2, dist))));
                    return true;
                }
            }
        }

        hitBlock = default;
        hitPoint = end;
        return false;
    }

    private static bool SegmentIntersectsBox(Vec3d start, Vec3d end, BoundingBox box)
    {
        var d = end - start;
        var len = d.Length;
        if (len < 1e-6) return box.Contains(start);
        var tMin = 0.0;
        var tMax = 1.0;
        return Slab(start.X, d.X, box.MinX, box.MaxX, ref tMin, ref tMax) &&
               Slab(start.Y, d.Y, box.MinY, box.MaxY, ref tMin, ref tMax) &&
               Slab(start.Z, d.Z, box.MinZ, box.MaxZ, ref tMin, ref tMax);
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
