using System.Numerics;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Physics;

namespace OverHorizon.Gameplay.Mobs;

/// <summary>
/// Simulates peaceful (<see cref="MobType.Pig"/>, <see cref="MobType.Cow"/>, <see cref="MobType.Sheep"/>, <see cref="MobType.Chicken"/>)
/// and hostile (<see cref="MobType.Zombie"/>, <see cref="MobType.Skeleton"/>, <see cref="MobType.Spider"/>, <see cref="MobType.Creeper"/>)
/// 3D mobs, light-gated spawning, Creative-mode pacifism, and raycast sword melee combat.
/// </summary>
public sealed class WorldMobManager
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

    /// <summary>Spawns a specific mob directly (used by world spawner and deterministic unit tests).</summary>
    public ActiveMob SpawnMob(MobType type, Vec3d feetPosition, float yawDegrees = 0.0f)
    {
        var mob = new ActiveMob(_nextMobId++, type, feetPosition, yawDegrees);
        _mobs.Add(mob);
        return mob;
    }

    /// <summary>Clears all active mobs.</summary>
    public void Clear() => _mobs.Clear();

    /// <summary>
    /// Advances mob spawning, AI behavior, swept-voxel kinematics, and Survival melee attacks.
    /// Hostile mobs strictly ignore and NEVER attack players in <see cref="GameMode.Creative"/>.
    /// </summary>
    public float Step(
        IVoxelCollisionReader collisionWorld,
        Func<BlockPos, (byte SkyLight, byte BlockLight, bool IsGrassTop, bool IsSolidGround, bool IsClearSpace)> spawnProbe,
        Vec3d playerFeetPos,
        GameMode playerMode,
        int playerArmorDefense,
        float skyBrightness,
        float deltaSeconds)
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

            UpdateMobAiAndAttack(mob, playerFeetPos, playerCanBeTargeted, dt, ref totalDamageToPlayer);
            StepMobPhysics(collisionWorld, mob, dt);

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

    /// <summary>
    /// Performs a 3D melee weapon raycast from <paramref name="rayOrigin"/> along <paramref name="rayDirection"/>
    /// up to <paramref name="maxReach"/>, hitting the closest living mob, applying weapon damage &amp; knockback,
    /// and returning loot drops if killed.
    /// </summary>
    public bool TryAttackAlongRay(
        Vec3d rayOrigin,
        Vector3 rayDirection,
        double maxReach,
        float weaponDamage,
        bool isCriticalStrike,
        IRegistry<ItemDefinition> items,
        out ActiveMob? hitMob,
        out List<ItemStack> drops)
    {
        ArgumentNullException.ThrowIfNull(items);

        hitMob = null;
        drops = [];

        var bestT = maxReach + 0.001;
        for (var i = 0; i < _mobs.Count; i++)
        {
            var candidate = _mobs[i];
            if (candidate.IsDead)
            {
                continue;
            }

            // Expand hitbox slightly by 0.18m (like Minecraft's targeting margin) so sword swings feel crisp & accurate!
            var paddedBox = candidate.Hitbox.Inflate(0.18, 0.14, 0.18);
            if (TryIntersectRayBox(rayOrigin, rayDirection, maxReach, paddedBox, out var hitDist) && hitDist < bestT)
            {
                bestT = hitDist;
                hitMob = candidate;
            }
        }

        if (hitMob is null)
        {
            return false;
        }

        if (hitMob.InvulnerabilityTimer > 0.05f)
        {
            return true;
        }

        var dmg = Math.Max(1.0f, weaponDamage) * (isCriticalStrike ? 1.5f : 1.0f);
        hitMob.Health = MathF.Max(0.0f, hitMob.Health - dmg);
        hitMob.HurtFlashTimer = 1.0f;
        hitMob.InvulnerabilityTimer = 0.28f;

        if (!hitMob.IsHostile)
        {
            hitMob.PanicTimer = 4.0f;
            hitMob.YawDegrees = (float)(Math.Atan2(rayDirection.X, rayDirection.Z) * (180.0 / Math.PI));
        }

        var horizLen = MathF.Sqrt((rayDirection.X * rayDirection.X) + (rayDirection.Z * rayDirection.Z));
        var kx = horizLen > 1e-4f ? (rayDirection.X / horizLen) * 6.8 : 0.0;
        var kz = horizLen > 1e-4f ? (rayDirection.Z / horizLen) * 6.8 : 0.0;
        hitMob.Velocity = new Vec3d(kx, 4.6, kz);
        hitMob.OnGround = false;

        if (hitMob.IsDead)
        {
            PopulateDeathDrops(hitMob, items, drops);
            _ = _mobs.Remove(hitMob);
        }

        return true;
    }

    /// <summary>Shears a targeted <see cref="MobType.Sheep"/> within reach, yielding <c>1..3</c> White Wool.</summary>
    public bool TryShearAlongRay(
        Vec3d rayOrigin,
        Vector3 rayDirection,
        double maxReach,
        IRegistry<ItemDefinition> items,
        out ItemStack woolDrop)
    {
        woolDrop = ItemStack.Empty;
        for (var i = 0; i < _mobs.Count; i++)
        {
            var mob = _mobs[i];
            if (mob.Type != MobType.Sheep || mob.IsSheared || mob.IsDead)
            {
                continue;
            }

            if (TryIntersectRayBox(rayOrigin, rayDirection, maxReach, mob.Hitbox.Inflate(0.18, 0.14, 0.18), out _))
            {
                mob.IsSheared = true;
                var woolId = (ushort)items.GetId(OverHorizon.Core.Blocks.BuiltInBlocks.WhiteWool);
                var count = 1 + (int)(NextRandom() % 3U);
                woolDrop = woolId > 0 ? new ItemStack(woolId, count) : ItemStack.Empty;
                return !woolDrop.IsEmpty;
            }
        }

        return false;
    }

    private void UpdateMobAiAndAttack(
        ActiveMob mob,
        Vec3d playerFeetPos,
        bool playerCanBeTargeted,
        float dt,
        ref float totalDamageToPlayer)
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

        // CRITICAL RULE: Hostile mobs NEVER aggro or attack when the player is in Creative Mode!
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
        var vy = mob.Velocity.Y;

        var prevPos = mob.FeetPosition;
        var step = KinematicsIntegrator.StepEntity(world, mob.FeetPosition, new Vec3d(vx, vy, vz), mob.Width, mob.Height, dt, swimUp: true);
        var nextVel = step.Velocity;

        // Auto-hop 1-block terrain steps while walking/chasing!
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
                    var kind = (MobType)(NextRandom() % 4U);
                    _ = SpawnMob(kind, new Vec3d(wx + 0.5, pos.Y + 1.0, wz + 0.5), NextRandom() % 360U);
                    peacefulCount++;
                    break;
                }

                if (hostileCount < MaxHostileMobs && effectiveLight <= 7.0f)
                {
                    var kind = (MobType)(4 + (NextRandom() % 4U));
                    _ = SpawnMob(kind, new Vec3d(wx + 0.5, pos.Y + 1.0, wz + 0.5), NextRandom() % 360U);
                    hostileCount++;
                    break;
                }
            }
        }
    }

    private void PopulateDeathDrops(ActiveMob mob, IRegistry<ItemDefinition> items, List<ItemStack> drops)
    {
        void Add(OverHorizon.Core.Registry.ResourceKey key, int min, int max)
        {
            var id = items.GetId(key);
            var qty = min + (int)(NextRandom() % (uint)(max - min + 1));
            if (id > 0 && qty > 0)
            {
                drops.Add(new ItemStack((ushort)id, qty));
            }
        }

        switch (mob.Type)
        {
            case MobType.Pig:
                Add(BuiltInItems.RawPorkchop, 1, 3);
                break;
            case MobType.Cow:
                Add(BuiltInItems.RawBeef, 1, 3);
                Add(BuiltInItems.Leather, 0, 2);
                break;
            case MobType.Sheep:
                Add(BuiltInItems.RawMutton, 1, 2);
                if (!mob.IsSheared)
                {
                    Add(OverHorizon.Core.Blocks.BuiltInBlocks.WhiteWool, 1, 1);
                }

                break;
            case MobType.Chicken:
                Add(BuiltInItems.RawChicken, 1, 1);
                Add(BuiltInItems.Feather, 1, 2);
                break;
            case MobType.Zombie:
                Add(BuiltInItems.RottenFlesh, 1, 2);
                break;
            case MobType.Skeleton:
                Add(BuiltInItems.Bone, 1, 2);
                break;
            case MobType.Spider:
                Add(BuiltInItems.String, 1, 2);
                Add(BuiltInItems.SpiderEye, 0, 1);
                break;
            case MobType.Creeper:
                Add(BuiltInItems.Gunpowder, 1, 2);
                break;
        }
    }

    private static bool TryIntersectRayBox(Vec3d origin, Vector3 dir, double maxDist, BoundingBox box, out double hitT)
    {
        hitT = 0.0;
        var tMin = 0.0;
        var tMax = maxDist;

        if (!SlabAxis(origin.X, dir.X, box.MinX, box.MaxX, ref tMin, ref tMax) ||
            !SlabAxis(origin.Y, dir.Y, box.MinY, box.MaxY, ref tMin, ref tMax) ||
            !SlabAxis(origin.Z, dir.Z, box.MinZ, box.MaxZ, ref tMin, ref tMax))
        {
            return false;
        }

        hitT = Math.Max(0.0, tMin);
        return hitT <= maxDist;
    }

    private static bool SlabAxis(double ro, double rd, double bMin, double bMax, ref double tMin, ref double tMax)
    {
        if (Math.Abs(rd) < 1e-8)
        {
            return ro >= bMin && ro <= bMax;
        }

        var inv = 1.0 / rd;
        var t1 = (bMin - ro) * inv;
        var t2 = (bMax - ro) * inv;
        if (t1 > t2)
        {
            (t1, t2) = (t2, t1);
        }

        tMin = Math.Max(tMin, t1);
        tMax = Math.Min(tMax, t2);
        return tMin <= tMax;
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
