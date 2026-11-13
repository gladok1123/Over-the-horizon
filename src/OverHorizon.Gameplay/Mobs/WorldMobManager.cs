using System.Numerics;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay.Ai;
using OverHorizon.Physics;

namespace OverHorizon.Gameplay.Mobs;

/// <summary>
/// Simulates peaceful (<see cref="MobType.Pig"/>, <see cref="MobType.Cow"/>, <see cref="MobType.Sheep"/>, <see cref="MobType.Chicken"/>)
/// and hostile (<see cref="MobType.Zombie"/>, <see cref="MobType.Skeleton"/>, <see cref="MobType.Spider"/>, <see cref="MobType.Creeper"/>)
/// 3D mobs, light-gated spawning, undead sunlight burning, Creative-mode pacifism, explosion blast damage, and melee combat.
/// </summary>
public sealed partial class WorldMobManager
{
    private const int MaxPeacefulMobs = 18;
    private const int MaxHostileMobs = 18;
    private const double DespawnDistance = 88.0;

    private readonly List<ActiveMob> _mobs = [];
    private int _nextMobId = 1;
    private uint _rng = 0x5A17C9E3U;
    private float _spawnTimer;

    /// <summary>Gets all currently active world mobs.</summary>
    public IReadOnlyList<ActiveMob> Mobs => _mobs;

    /// <summary>Spawns a specific mob directly (used by world spawner, <c>/summon</c>, and unit tests).</summary>
    public ActiveMob SpawnMob(MobType type, Vec3d feetPosition, float yawDegrees = 0.0f)
    {
        var mob = new ActiveMob(_nextMobId++, type, feetPosition, yawDegrees);
        _mobs.Add(mob);
        return mob;
    }

    /// <summary>Clears all active mobs.</summary>
    public void Clear() => _mobs.Clear();

    /// <summary>Replaces client-side mirrored mobs from an authoritative multiplayer server snapshot.</summary>
    public void ReplaceFromServerSnapshots(
        ReadOnlySpan<(int Id, byte TypeByte, Vec3d FeetPos, float Yaw, float WalkPhase, float HurtFlash, float CreeperFuse, byte Flags)> snapshots)
    {
        _mobs.Clear();
        for (var i = 0; i < snapshots.Length; i++)
        {
            var s = snapshots[i];
            var m = new ActiveMob(s.Id, (MobType)Math.Clamp((int)s.TypeByte, 0, 7), s.FeetPos, s.Yaw)
            {
                WalkPhase = s.WalkPhase,
                HurtFlashTimer = s.HurtFlash,
                CreeperFuseTimer = s.CreeperFuse,
                IsSheared = (s.Flags & 1) != 0,
                BurnTimer = (s.Flags & 2) != 0 ? 1.0f : 0.0f,
            };
            _mobs.Add(m);
        }
    }

    /// <summary>
    /// Advances mob spawning, undead sunlight burning, A* pathfinding AI, swept-voxel kinematics, Shield blocking, and Creeper/Skeleton attacks.
    /// </summary>
    public float Step(
        IVoxelCollisionReader collisionWorld,
        Func<BlockPos, (byte SkyLight, byte BlockLight, bool IsGrassTop, bool IsSolidGround, bool IsClearSpace)> spawnProbe,
        Vec3d playerFeetPos,
        GameMode playerMode,
        int playerArmorDefense,
        float skyBrightness,
        float deltaSeconds,
        List<Vec3d>? creeperDetonations = null,
        Action<Vec3d, Vec3d>? onSkeletonShootArrow = null,
        List<(MobType Type, Vec3d Position, bool Died, float FallDamage)>? mobLandingHurtEvents = null,
        List<Vec3d>? creeperFuseStartedPositions = null,
        bool isPlayerBlockingShield = false,
        Vector3 playerLookForward = default,
        List<Vec3d>? shieldBlockedPositions = null,
        Func<BlockPos, bool>? isFireBlock = null)
    {
        ArgumentNullException.ThrowIfNull(collisionWorld);
        ArgumentNullException.ThrowIfNull(spawnProbe);

        var dt = Math.Clamp(deltaSeconds, 0.001f, 0.1f);
        _spawnTimer += dt;
        if (_spawnTimer >= 0.85f)
        {
            _spawnTimer = 0.0f;
            TryNaturalSpawns(spawnProbe, playerFeetPos, skyBrightness);
        }

        var totalDamageToPlayer = 0.0f;
        var playerCanBeTargeted = playerMode == GameMode.Survival;
        var pathQueriesRemaining = 2;

        for (var i = _mobs.Count - 1; i >= 0; i--)
        {
            var mob = _mobs[i];
            if (mob.IsDead || mob.FeetPosition.DistanceTo(playerFeetPos) > DespawnDistance)
            {
                _mobs.RemoveAt(i);
                continue;
            }

            mob.HurtFlashTimer = MathF.Max(0.0f, mob.HurtFlashTimer - (dt * 3.2f));
            mob.InvulnerabilityTimer = MathF.Max(0.0f, mob.InvulnerabilityTimer - dt);
            mob.AttackCooldownTimer = MathF.Max(0.0f, mob.AttackCooldownTimer - dt);
            mob.PanicTimer = MathF.Max(0.0f, mob.PanicTimer - dt);
            mob.KnockbackTimer = MathF.Max(0.0f, mob.KnockbackTimer - dt);
            mob.PathRecomputeTimer = MathF.Max(0.0f, mob.PathRecomputeTimer - dt);

            if (isFireBlock is not null && isFireBlock(mob.FeetPosition.ToBlockPos()))
            {
                mob.BurnTimer = MathF.Max(mob.BurnTimer, 4.0f);
            }

            UpdateSunlightBurning(collisionWorld, spawnProbe, mob, skyBrightness, dt, mobLandingHurtEvents);
            if (!mob.IsDead)
            {
                UpdateMobAiAndAttack(
                    collisionWorld,
                    mob,
                    playerFeetPos,
                    playerCanBeTargeted,
                    dt,
                    ref pathQueriesRemaining,
                    ref totalDamageToPlayer,
                    creeperDetonations,
                    onSkeletonShootArrow,
                    creeperFuseStartedPositions,
                    isPlayerBlockingShield,
                    playerLookForward,
                    shieldBlockedPositions);
                StepMobPhysics(collisionWorld, mob, dt, mobLandingHurtEvents);
            }

            if (mob.IsDead)
            {
                _mobs.RemoveAt(i);
            }
        }

        if (totalDamageToPlayer > 0.0f && playerArmorDefense > 0)
        {
            var reduction = Math.Clamp(playerArmorDefense * 0.04f, 0.0f, 0.80f);
            totalDamageToPlayer *= 1.0f - reduction;
        }

        return playerCanBeTargeted ? totalDamageToPlayer : 0.0f;
    }

    private static void UpdateSunlightBurning(
        IVoxelCollisionReader world,
        Func<BlockPos, (byte SkyLight, byte BlockLight, bool IsGrassTop, bool IsSolidGround, bool IsClearSpace)> probe,
        ActiveMob mob,
        float skyBrightness,
        float dt,
        List<(MobType Type, Vec3d Position, bool Died, float FallDamage)>? mobHurtEvents)
    {
        var feetBlock = mob.FeetPosition.ToBlockPos();
        if (world.IsColumnLoaded(feetBlock) && world.GetFluidLevelOrNegative(feetBlock) >= 0)
        {
            mob.BurnTimer = 0.0f;
            mob.BurnDamageTickTimer = 0.0f;
            return;
        }

        if (mob.Type is MobType.Zombie or MobType.Skeleton && skyBrightness >= 0.65f)
        {
            var headPos = new Vec3d(mob.FeetPosition.X, mob.FeetPosition.Y + 1.5, mob.FeetPosition.Z).ToBlockPos();
            if (probe(headPos).SkyLight >= 15)
            {
                mob.BurnTimer = 2.5f;
            }
        }

        if (mob.BurnTimer > 0.0f)
        {
            mob.BurnTimer = MathF.Max(0.0f, mob.BurnTimer - dt);
            mob.BurnDamageTickTimer += dt;
            if (mob.BurnDamageTickTimer >= 0.95f)
            {
                mob.BurnDamageTickTimer -= 0.95f;
                mob.Health = MathF.Max(0.0f, mob.Health - 1.0f);
                mob.HurtFlashTimer = MathF.Max(mob.HurtFlashTimer, 0.75f);

                // Negative FallDamage (-1.0f) signals a fire/sunlight burn tick so clients play MobHurt/Death + Fizz!
                mobHurtEvents?.Add((mob.Type, mob.FeetPosition, mob.IsDead, -1.0f));
            }
        }
        else
        {
            mob.BurnDamageTickTimer = 0.0f;
        }
    }

    private void UpdateMobAiAndAttack(
        IVoxelCollisionReader world,
        ActiveMob mob,
        Vec3d playerFeetPos,
        bool playerCanBeTargeted,
        float dt,
        ref int pathQueriesRemaining,
        ref float totalDamageToPlayer,
        List<Vec3d>? creeperDetonations,
        Action<Vec3d, Vec3d>? onSkeletonShootArrow,
        List<Vec3d>? creeperFuseStartedPositions,
        bool isPlayerBlockingShield,
        Vector3 playerLookForward,
        List<Vec3d>? shieldBlockedPositions)
    {
        if (!mob.IsHostile)
        {
            mob.IsTargetingPlayer = false;
            if (mob.PanicTimer > 0.0f)
            {
                mob.DesiredSpeed = 4.2f;
                return;
            }

            UpdatePassiveWander(mob, dt, walkSpeed: mob.Type == MobType.Chicken ? 1.8f : 1.5f);
            return;
        }

        if (!playerCanBeTargeted)
        {
            mob.IsTargetingPlayer = false;
            mob.CreeperFuseTimer = 0.0f;
            mob.CreeperFuseHissTriggered = false;
            mob.PathWaypoints = null;
            UpdatePassiveWander(mob, dt, walkSpeed: 1.4f);
            return;
        }

        var dx = playerFeetPos.X - mob.FeetPosition.X;
        var dy = playerFeetPos.Y - mob.FeetPosition.Y;
        var dz = playerFeetPos.Z - mob.FeetPosition.Z;
        var distSq = (dx * dx) + (dy * dy) + (dz * dz);
        var aggroRange = mob.Type == MobType.Spider ? 20.0 : 24.0;

        if (distSq <= aggroRange * aggroRange)
        {
            mob.IsTargetingPlayer = true;
            var dist = Math.Sqrt(distSq);

            // Smart A* Voxel Pathfinding around walls, doorways, ledges, and obstacles!
            var steerDx = dx;
            var steerDz = dz;
            if (dist > 1.85)
            {
                if (mob.PathRecomputeTimer <= 0.0f && pathQueriesRemaining > 0)
                {
                    pathQueriesRemaining--;
                    mob.PathRecomputeTimer = 0.60f + ((NextRandom() & 31U) * 0.01f);
                    mob.PathWaypoints = VoxelPathfinder.FindPath(
                        world,
                        mob.FeetPosition.ToBlockPos(),
                        playerFeetPos.ToBlockPos(),
                        maxExpandedNodes: 96);
                    mob.PathWaypointIndex = mob.PathWaypoints is { Count: > 1 } ? 1 : 0;
                }

                if (mob.PathWaypoints is { Count: > 1 } path)
                {
                    while (mob.PathWaypointIndex < path.Count - 1)
                    {
                        var wp = path[mob.PathWaypointIndex];
                        var wdx = (wp.X + 0.5) - mob.FeetPosition.X;
                        var wdz = (wp.Z + 0.5) - mob.FeetPosition.Z;
                        if ((wdx * wdx) + (wdz * wdz) < 0.36)
                        {
                            mob.PathWaypointIndex++;
                            continue;
                        }

                        break;
                    }

                    if (mob.PathWaypointIndex < path.Count)
                    {
                        var wp = path[mob.PathWaypointIndex];
                        steerDx = (wp.X + 0.5) - mob.FeetPosition.X;
                        steerDz = (wp.Z + 0.5) - mob.FeetPosition.Z;
                    }
                }
            }

            mob.YawDegrees = (float)(Math.Atan2(steerDx, steerDz) * (180.0 / Math.PI));

            if (mob.Type == MobType.Creeper)
            {
                if (dist <= 3.0)
                {
                    mob.DesiredSpeed = 0.0f;
                    if (!mob.CreeperFuseHissTriggered)
                    {
                        mob.CreeperFuseHissTriggered = true;
                        creeperFuseStartedPositions?.Add(mob.FeetPosition);
                    }

                    mob.CreeperFuseTimer += dt;
                    if (mob.CreeperFuseTimer >= 1.5f)
                    {
                        var blastFactor = (float)Math.Clamp(1.0 - (dist / 4.5), 0.25, 1.0);
                        var blocked = IsFrontalShieldBlock(isPlayerBlockingShield, playerLookForward, playerFeetPos, mob.FeetPosition);
                        if (blocked)
                        {
                            shieldBlockedPositions?.Add(playerFeetPos);
                            totalDamageToPlayer += mob.AttackDamage * blastFactor * 0.15f;
                        }
                        else
                        {
                            totalDamageToPlayer += mob.AttackDamage * blastFactor;
                        }

                        creeperDetonations?.Add(new Vec3d(mob.FeetPosition.X, mob.FeetPosition.Y + 0.8, mob.FeetPosition.Z));
                        mob.Health = 0.0f;
                    }
                }
                else
                {
                    mob.CreeperFuseTimer = MathF.Max(0.0f, mob.CreeperFuseTimer - dt);
                    if (mob.CreeperFuseTimer <= 0.01f)
                    {
                        mob.CreeperFuseHissTriggered = false;
                    }

                    mob.DesiredSpeed = 2.65f;
                }

                return;
            }

            if (mob.Type == MobType.Skeleton && onSkeletonShootArrow is not null && dist <= 16.5)
            {
                var shootOrigin = new Vec3d(mob.FeetPosition.X, mob.FeetPosition.Y + 1.52, mob.FeetPosition.Z);
                var playerChest = new Vec3d(playerFeetPos.X, playerFeetPos.Y + 1.35, playerFeetPos.Z);
                if (HasClearLineOfSight(world, shootOrigin, playerChest))
                {
                    mob.YawDegrees = (float)(Math.Atan2(dx, dz) * (180.0 / Math.PI));
                    mob.DesiredSpeed = dist > 9.5 ? 2.45f : 0.0f;
                    if (mob.AttackCooldownTimer <= 0.0f && dist >= 1.5)
                    {
                        var aimTarget = new Vec3d(playerFeetPos.X, playerFeetPos.Y + 1.35 + (dist * 0.14), playerFeetPos.Z);
                        var dir = (aimTarget - shootOrigin).Normalize();
                        onSkeletonShootArrow(shootOrigin, dir * 21.5);
                        mob.AttackCooldownTimer = 1.95f;
                    }

                    return;
                }

                // Sightline obstructed by wall/door: keep advancing along A* path instead of shooting into solid stone!
                mob.DesiredSpeed = 2.55f;
                return;
            }

            mob.DesiredSpeed = mob.Type == MobType.Spider ? 3.55f : 2.75f;
            if (dist <= 1.65 && Math.Abs(dy) <= 1.8 && mob.AttackCooldownTimer <= 0.0f)
            {
                mob.AttackCooldownTimer = 1.05f;
                if (IsFrontalShieldBlock(isPlayerBlockingShield, playerLookForward, playerFeetPos, mob.FeetPosition))
                {
                    shieldBlockedPositions?.Add(playerFeetPos);
                    var pushBack = (mob.FeetPosition - playerFeetPos).Normalize();
                    mob.Velocity = new Vec3d(pushBack.X * 5.2, 2.8, pushBack.Z * 5.2);
                    mob.KnockbackTimer = 0.32f;
                }
                else
                {
                    totalDamageToPlayer += mob.AttackDamage;
                }
            }
        }
        else
        {
            mob.IsTargetingPlayer = false;
            mob.CreeperFuseTimer = 0.0f;
            mob.CreeperFuseHissTriggered = false;
            mob.PathWaypoints = null;
            UpdatePassiveWander(mob, dt, walkSpeed: 1.45f);
        }
    }

    /// <summary>Returns <see langword="true"/> when an active Shield blocks an attack originating from <paramref name="attackerPos"/>.</summary>
    public static bool IsFrontalShieldBlock(bool isBlockingShield, Vector3 defenderLookForward, Vec3d defenderFeet, Vec3d attackerPos)
    {
        if (!isBlockingShield) return false;
        var toAttackerX = (float)(attackerPos.X - defenderFeet.X);
        var toAttackerZ = (float)(attackerPos.Z - defenderFeet.Z);
        var len = MathF.Sqrt((toAttackerX * toAttackerX) + (toAttackerZ * toAttackerZ));
        if (len < 1e-4f) return true;
        var fwdXZ = new Vector2(defenderLookForward.X, defenderLookForward.Z);
        var fwdLen = fwdXZ.Length();
        if (fwdLen < 1e-4f) return true;
        var dot = ((fwdXZ.X / fwdLen) * (toAttackerX / len)) + ((fwdXZ.Y / fwdLen) * (toAttackerZ / len));
        return dot >= -0.05f;
    }

    /// <summary>
    /// Performs a zero-allocation sub-voxel 3D raycast from <paramref name="origin"/> to <paramref name="target"/>,
    /// returning <see langword="true"/> only when no solid voxel or closed Door/Trapdoor slab blocks the shot.
    /// </summary>
    public static bool HasClearLineOfSight(IVoxelCollisionReader world, Vec3d origin, Vec3d target)
    {
        ArgumentNullException.ThrowIfNull(world);

        var delta = target - origin;
        var length = delta.Length;
        if (length <= 1e-4)
        {
            return true;
        }

        var dir = delta * (1.0 / length);
        var steps = Math.Max(4, (int)Math.Ceiling(length / 0.16));
        var stepDist = length / steps;
        var lastChecked = new BlockPos(int.MinValue, int.MinValue, int.MinValue);
        var hasCachedBox = false;
        BoundingBox cachedBox = default;

        for (var i = 1; i < steps; i++)
        {
            var p = origin + (dir * (i * stepDist));
            var bp = p.ToBlockPos();
            if (bp != lastChecked)
            {
                lastChecked = bp;
                hasCachedBox = world.TryGetCollisionBox(bp, out cachedBox);
            }

            if (hasCachedBox && cachedBox.Contains(p))
            {
                return false;
            }
        }

        return true;
    }
}
