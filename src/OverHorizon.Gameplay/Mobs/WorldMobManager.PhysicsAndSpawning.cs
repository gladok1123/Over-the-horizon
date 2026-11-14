using OverHorizon.Core;
using OverHorizon.Core.Math;
using OverHorizon.Physics;

namespace OverHorizon.Gameplay.Mobs;

/// <summary>
/// Swept-voxel mob physics, knockback trajectory preservation, spider climbing, fall damage, and natural spawning for <see cref="WorldMobManager"/>.
/// </summary>
public sealed partial class WorldMobManager
{
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

    private static void StepMobPhysics(
        IVoxelCollisionReader world,
        ActiveMob mob,
        float dt,
        List<(MobType Type, Vec3d Position, bool Died, float FallDamage)>? mobLandingHurtEvents)
    {
        var yawRad = mob.YawDegrees * (Math.PI / 180.0);
        var wishVx = Math.Sin(yawRad) * mob.DesiredSpeed;
        var wishVz = Math.Cos(yawRad) * mob.DesiredSpeed;

        double vx;
        double vz;
        if (mob.KnockbackTimer > 0.01f || !mob.OnGround)
        {
            var airDrag = Math.Pow(0.91, dt * 20.0);
            vx = mob.Velocity.X * airDrag;
            vz = mob.Velocity.Z * airDrag;
            if (mob.KnockbackTimer <= 0.01f)
            {
                vx = (vx * 0.90) + (wishVx * 0.10);
                vz = (vz * 0.90) + (wishVz * 0.10);
            }
        }
        else
        {
            var blend = Math.Min(1.0, dt * 10.0);
            vx = (mob.Velocity.X * (1.0 - blend)) + (wishVx * blend);
            vz = (mob.Velocity.Z * (1.0 - blend)) + (wishVz * blend);
        }

        var vy = mob.Type == MobType.Chicken ? Math.Max(mob.Velocity.Y, -2.2) : mob.Velocity.Y;

        var prevPos = mob.FeetPosition;
        var wasOnGround = mob.OnGround;
        var step = KinematicsIntegrator.StepEntity(world, mob.FeetPosition, new Vec3d(vx, vy, vz), mob.Width, mob.Height, dt, swimUp: true);
        var nextVel = step.Velocity;
        if (mob.Type == MobType.Chicken && nextVel.Y < -2.2)
        {
            nextVel = new Vec3d(nextVel.X, -2.2, nextVel.Z);
        }

        if (step.InFluid || mob.Type == MobType.Chicken || mob.IsImmuneToFallDamage)
        {
            mob.FallDistance = 0.0;
        }
        else
        {
            var dy = step.Position.Y - prevPos.Y;
            if (!step.OnGround && dy < 0.0)
            {
                mob.FallDistance += -dy;
            }
            else if (step.OnGround)
            {
                if (!wasOnGround && mob.FallDistance > 3.0)
                {
                    var fallDmg = MathF.Ceiling((float)(mob.FallDistance - 3.0));
                    mob.Health = MathF.Max(0.0f, mob.Health - fallDmg);
                    mob.HurtFlashTimer = 1.0f;
                    mobLandingHurtEvents?.Add((mob.Type, step.Position, mob.IsDead, fallDmg));
                }

                mob.FallDistance = 0.0;
            }
        }

        // Magma cubes never walk: they hop forward in bouncing arcs like vanilla slimes.
        if (mob.Type == MobType.MagmaCube && step.OnGround && mob.KnockbackTimer <= 0.01f)
        {
            mob.HopCooldownTimer -= dt;
            if (mob.HopCooldownTimer <= 0.0f)
            {
                mob.HopCooldownTimer = 0.55f + ((NextRandom() % 40U) * 0.01f);
                nextVel = new Vec3d(wishVx * 1.35, 7.4, wishVz * 1.35);
            }
            else
            {
                nextVel = new Vec3d(nextVel.X * 0.55, nextVel.Y, nextVel.Z * 0.55);
            }
        }

        if (step.CollidedHorizontally && mob.DesiredSpeed > 0.2f && !mob.IsDead && mob.KnockbackTimer <= 0.01f)
        {
            if (mob.Type == MobType.Spider)
            {
                nextVel = new Vec3d(wishVx * 0.5, 3.2, wishVz * 0.5);
            }
            else if (step.OnGround)
            {
                nextVel = new Vec3d(wishVx, 8.8, wishVz);
            }
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
        float skyBrightness,
        DimensionId dimension)
    {
        var isNether = dimension == DimensionId.Nether;
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

            var tryUnderground = !isNether && hostileCount < MaxHostileMobs && (attempt & 1) == 1;
            var baseY = isNether
                ? (int)Math.Clamp(playerFeetPos.Y - 18 + (int)(NextRandom() % 36U), DimensionRules.NetherFloorY + 2, DimensionRules.NetherCeilingY - 4)
                : tryUnderground
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
                if (!isNether && !tryUnderground && peacefulCount < MaxPeacefulMobs && info.IsGrassTop && info.SkyLight >= 9)
                {
                    _ = SpawnMob((MobType)(NextRandom() % 4U), new Vec3d(wx + 0.5, pos.Y + 1.0, wz + 0.5), NextRandom() % 360U);
                    peacefulCount++;
                    break;
                }

                if (hostileCount < MaxHostileMobs && effectiveLight <= 7.0f)
                {
                    // The Nether has its own bestiary: zombified piglins and hopping magma cubes.
                    var species = isNether
                        ? ((NextRandom() & 3U) == 0U ? MobType.MagmaCube : MobType.ZombifiedPiglin)
                        : (MobType)(4 + (NextRandom() % 4U));

                    _ = SpawnMob(species, new Vec3d(wx + 0.5, pos.Y + 1.0, wz + 0.5), NextRandom() % 360U);
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
