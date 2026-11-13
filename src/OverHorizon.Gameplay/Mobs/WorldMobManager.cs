using OverHorizon.Core.Math;
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

    /// <summary>
    /// Advances mob spawning, undead sunlight burning, AI behavior, swept-voxel kinematics, and Survival melee/Creeper attacks.
    /// </summary>
    public float Step(
        IVoxelCollisionReader collisionWorld,
        Func<BlockPos, (byte SkyLight, byte BlockLight, bool IsGrassTop, bool IsSolidGround, bool IsClearSpace)> spawnProbe,
        Vec3d playerFeetPos,
        GameMode playerMode,
        int playerArmorDefense,
        float skyBrightness,
        float deltaSeconds,
        List<Vec3d>? creeperDetonations = null)
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

            UpdateSunlightBurning(collisionWorld, spawnProbe, mob, skyBrightness, dt);
            if (!mob.IsDead)
            {
                UpdateMobAiAndAttack(mob, playerFeetPos, playerCanBeTargeted, dt, ref totalDamageToPlayer, creeperDetonations);
                StepMobPhysics(collisionWorld, mob, dt);
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
        float dt)
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
            }
        }
        else
        {
            mob.BurnDamageTickTimer = 0.0f;
        }
    }

    private void UpdateMobAiAndAttack(
        ActiveMob mob,
        Vec3d playerFeetPos,
        bool playerCanBeTargeted,
        float dt,
        ref float totalDamageToPlayer,
        List<Vec3d>? creeperDetonations)
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
            UpdatePassiveWander(mob, dt, walkSpeed: 1.4f);
            return;
        }

        var dx = playerFeetPos.X - mob.FeetPosition.X;
        var dy = playerFeetPos.Y - mob.FeetPosition.Y;
        var dz = playerFeetPos.Z - mob.FeetPosition.Z;
        var distSq = (dx * dx) + (dy * dy) + (dz * dz);
        var aggroRange = mob.Type == MobType.Spider ? 18.0 : 22.0;

        if (distSq <= aggroRange * aggroRange)
        {
            mob.IsTargetingPlayer = true;
            mob.YawDegrees = (float)(Math.Atan2(dx, dz) * (180.0 / Math.PI));
            var dist = Math.Sqrt(distSq);

            if (mob.Type == MobType.Creeper)
            {
                if (dist <= 3.0)
                {
                    mob.DesiredSpeed = 0.0f;
                    mob.CreeperFuseTimer += dt;
                    if (mob.CreeperFuseTimer >= 1.5f)
                    {
                        var blastFactor = (float)Math.Clamp(1.0 - (dist / 4.5), 0.25, 1.0);
                        totalDamageToPlayer += mob.AttackDamage * blastFactor;
                        creeperDetonations?.Add(new Vec3d(mob.FeetPosition.X, mob.FeetPosition.Y + 0.8, mob.FeetPosition.Z));
                        mob.Health = 0.0f;
                    }
                }
                else
                {
                    mob.CreeperFuseTimer = MathF.Max(0.0f, mob.CreeperFuseTimer - dt);
                    mob.DesiredSpeed = 2.65f;
                }

                return;
            }

            mob.DesiredSpeed = mob.Type == MobType.Spider ? 3.55f : 2.75f;
            if (dist <= 1.65 && Math.Abs(dy) <= 1.8 && mob.AttackCooldownTimer <= 0.0f)
            {
                totalDamageToPlayer += mob.AttackDamage;
                mob.AttackCooldownTimer = 1.05f;
            }
        }
        else
        {
            mob.IsTargetingPlayer = false;
            mob.CreeperFuseTimer = 0.0f;
            UpdatePassiveWander(mob, dt, walkSpeed: 1.45f);
        }
    }

    private void UpdatePassiveWander(ActiveMob mob, float dt, float walkSpeed)
    {
        mob.WanderChangeTimer -= dt;
        if (mob.WanderChangeTimer <= 0.0f)
        {
            mob.WanderChangeTimer = 1.8f + ((NextRandom() % 300U) * 0.01f);
            if ((NextRandom() % 100U) < 38U)
            {
                mob.DesiredSpeed = 0.0f;
            }
            else
            {
                mob.YawDegrees = NextRandom() % 360U;
                mob.DesiredSpeed = walkSpeed;
            }
        }
    }

    private static void StepMobPhysics(IVoxelCollisionReader world, ActiveMob mob, float dt)
    {
        var yawRad = mob.YawDegrees * (Math.PI / 180.0);
        var wishVx = Math.Sin(yawRad) * mob.DesiredSpeed;
        var wishVz = Math.Cos(yawRad) * mob.DesiredSpeed;
        var blend = Math.Min(1.0, dt * 10.0);
        var vx = (mob.Velocity.X * (1.0 - blend)) + (wishVx * blend);
        var vz = (mob.Velocity.Z * (1.0 - blend)) + (wishVz * blend);

        var prevPos = mob.FeetPosition;
        var step = KinematicsIntegrator.StepEntity(world, mob.FeetPosition, new Vec3d(vx, mob.Velocity.Y, vz), mob.Width, mob.Height, dt, swimUp: true);
        var nextVel = step.Velocity;

        if (step.OnGround && step.CollidedHorizontally && mob.DesiredSpeed > 0.2f)
        {
            nextVel = new Vec3d(wishVx, 8.8, wishVz);
        }

        mob.FeetPosition = step.Position;
        mob.Velocity = nextVel;
        mob.OnGround = step.OnGround;

        var movedX = mob.FeetPosition.X - prevPos.X;
        var movedZ = mob.FeetPosition.Z - prevPos.Z;
        var horizDist = Math.Sqrt((movedX * movedX) + (movedZ * movedZ));
        if (horizDist > 1e-4)
        {
            mob.WalkPhase += (float)(horizDist * 4.2);
        }
    }

    private void TryNaturalSpawns(
        Func<BlockPos, (byte SkyLight, byte BlockLight, bool IsGrassTop, bool IsSolidGround, bool IsClearSpace)> spawnProbe,
        Vec3d playerFeetPos,
        float skyBrightness)
    {
        var peacefulCount = 0;
        var hostileCount = 0;
        for (var i = 0; i < _mobs.Count; i++)
        {
            if (_mobs[i].IsHostile)
            {
                hostileCount++;
            }
            else
            {
                peacefulCount++;
            }
        }

        for (var attempt = 0; attempt < 14; attempt++)
        {
            var angle = ((NextRandom() % 360U) * Math.PI) / 180.0;
            var dist = 14.0 + (NextRandom() % 34U);
            var wx = (int)Math.Floor(playerFeetPos.X + (Math.Cos(angle) * dist));
            var wz = (int)Math.Floor(playerFeetPos.Z + (Math.Sin(angle) * dist));

            var tryUnderground = hostileCount < MaxHostileMobs && (attempt & 1) == 1;
            var baseY = tryUnderground
                ? (int)Math.Clamp(playerFeetPos.Y - 26 + (int)(NextRandom() % 44U), -48, 62)
                : (int)Math.Clamp(playerFeetPos.Y - 10 + (int)(NextRandom() % 24U), 58, 110);

            for (var scan = 0; scan < 12; scan++)
            {
                var pos = new BlockPos(wx, baseY + scan, wz);
                var info = spawnProbe(pos);
                if (!info.IsSolidGround || !info.IsClearSpace)
                {
                    continue;
                }

                var effectiveLight = MathF.Max(info.SkyLight * skyBrightness, info.BlockLight);
                if (!tryUnderground && peacefulCount < MaxPeacefulMobs && info.IsGrassTop && info.SkyLight >= 9)
                {
                    _ = SpawnMob((MobType)(NextRandom() % 4U), new Vec3d(wx + 0.5, pos.Y + 1.0, wz + 0.5), NextRandom() % 360U);
                    peacefulCount++;
                    break;
                }

                if (hostileCount < MaxHostileMobs && effectiveLight <= 7.0f)
                {
                    _ = SpawnMob((MobType)(4 + (NextRandom() % 4U)), new Vec3d(wx + 0.5, pos.Y + 1.0, wz + 0.5), NextRandom() % 360U);
                    hostileCount++;
                    break;
                }
            }
        }
    }

    private uint NextRandom()
    {
        var x = _rng;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _rng = x;
        return x;
    }
}
