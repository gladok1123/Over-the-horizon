using System.Numerics;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;

namespace OverHorizon.Gameplay.Mobs;

/// <summary>
/// Melee combat raycasting, sheep shearing, explosion blast damage, bed proximity checks, and loot drops for <see cref="WorldMobManager"/>.
/// </summary>
public sealed partial class WorldMobManager
{
    /// <summary>Checks whether any hostile mob is within <paramref name="horizRadius"/> / <paramref name="vertRadius"/> of <paramref name="bedCenter"/> (Bed sleep check).</summary>
    public bool HasHostileMobNear(Vec3d bedCenter, double horizRadius = 8.0, double vertRadius = 5.0)
    {
        for (var i = 0; i < _mobs.Count; i++)
        {
            var m = _mobs[i];
            if (!m.IsHostile || m.IsDead)
            {
                continue;
            }

            var dx = m.FeetPosition.X - bedCenter.X;
            var dy = Math.Abs(m.FeetPosition.Y - bedCenter.Y);
            var dz = m.FeetPosition.Z - bedCenter.Z;
            if (dy <= vertRadius && ((dx * dx) + (dz * dz)) <= horizRadius * horizRadius)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Applies radial blast damage and knockback from a TNT or Creeper explosion to all nearby mobs.</summary>
    public int ApplyExplosionBlast(Vec3d center, float radius = 8.0f, float maxDamage = 26.0f)
    {
        var affected = 0;
        for (var i = _mobs.Count - 1; i >= 0; i--)
        {
            var m = _mobs[i];
            var delta = new Vec3d(m.FeetPosition.X - center.X, (m.FeetPosition.Y + (m.Height * 0.5)) - center.Y, m.FeetPosition.Z - center.Z);
            var dist = delta.Length;
            if (dist >= radius)
            {
                continue;
            }

            var exposure = (float)Math.Clamp(1.0 - (dist / radius), 0.0, 1.0);
            var dmg = ((exposure * exposure) + exposure) * 0.5f * maxDamage;
            m.Health = MathF.Max(0.0f, m.Health - dmg);
            m.HurtFlashTimer = 1.0f;

            var dir = dist > 1e-3 ? delta * (1.0 / dist) : new Vec3d(0.0, 1.0, 0.0);
            var kb = exposure * 12.5;
            m.Velocity = new Vec3d(m.Velocity.X + (dir.X * kb), Math.Max(m.Velocity.Y, 4.8 + (exposure * 5.0)), m.Velocity.Z + (dir.Z * kb));
            m.KnockbackTimer = 0.45f;
            m.OnGround = false;
            affected++;

            if (m.IsDead)
            {
                _mobs.RemoveAt(i);
            }
        }

        return affected;
    }

    /// <summary>
    /// Performs a 3D melee weapon raycast from <paramref name="rayOrigin"/> along <paramref name="rayDirection"/>.
    /// </summary>
    public bool TryAttackAlongRay(
        Vec3d rayOrigin,
        Vector3 rayDirection,
        double maxReach,
        float weaponDamage,
        bool isCriticalStrike,
        IRegistry<ItemDefinition> items,
        out ActiveMob? hitMob,
        out List<ItemStack> drops,
        bool isSprintAttack = false)
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
        var kbStrength = isSprintAttack ? 12.8 : 8.8;
        var kbLift = isSprintAttack ? 4.65 : 4.25;
        var kx = horizLen > 1e-4f ? (rayDirection.X / horizLen) * kbStrength : 0.0;
        var kz = horizLen > 1e-4f ? (rayDirection.Z / horizLen) * kbStrength : 0.0;
        hitMob.Velocity = new Vec3d((hitMob.Velocity.X * 0.35) + kx, kbLift, (hitMob.Velocity.Z * 0.35) + kz);
        hitMob.KnockbackTimer = 0.44f;
        hitMob.OnGround = false;

        if (hitMob.IsDead)
        {
            PopulateDeathDrops(hitMob, items, drops);
            _ = _mobs.Remove(hitMob);
        }

        return true;
    }

    /// <summary>Tests a ballistic projectile segment against active mobs and applies arrow damage, knockback, and death drops.</summary>
    public bool TryHitMobAlongSegment(
        Vec3d start,
        Vec3d end,
        float arrowDamage,
        Vec3d arrowVelocity,
        IRegistry<ItemDefinition> items,
        out ActiveMob? hitMob,
        out List<ItemStack> drops)
    {
        ArgumentNullException.ThrowIfNull(items);
        hitMob = null;
        drops = [];

        var delta = end - start;
        var len = delta.Length;
        if (len <= 1e-6)
        {
            return false;
        }

        var dir = new Vector3((float)(delta.X / len), (float)(delta.Y / len), (float)(delta.Z / len));
        var bestT = len + 0.001;
        for (var i = 0; i < _mobs.Count; i++)
        {
            var candidate = _mobs[i];
            if (candidate.IsDead) continue;
            if (TryIntersectRayBox(start, dir, len, candidate.Hitbox.Inflate(0.22, 0.16, 0.22), out var t) && t < bestT)
            {
                bestT = t;
                hitMob = candidate;
            }
        }

        if (hitMob is null)
        {
            return false;
        }

        hitMob.Health = MathF.Max(0.0f, hitMob.Health - MathF.Max(1.0f, arrowDamage));
        hitMob.HurtFlashTimer = 1.0f;
        hitMob.InvulnerabilityTimer = 0.20f;
        if (!hitMob.IsHostile)
        {
            hitMob.PanicTimer = 4.0f;
        }

        var hLen = Math.Sqrt((arrowVelocity.X * arrowVelocity.X) + (arrowVelocity.Z * arrowVelocity.Z));
        var kx = hLen > 1e-4 ? (arrowVelocity.X / hLen) * 8.2 : 0.0;
        var kz = hLen > 1e-4 ? (arrowVelocity.Z / hLen) * 8.2 : 0.0;
        hitMob.Velocity = new Vec3d((hitMob.Velocity.X * 0.3) + kx, 4.1, (hitMob.Velocity.Z * 0.3) + kz);
        hitMob.KnockbackTimer = 0.40f;
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

    private void PopulateDeathDrops(ActiveMob mob, IRegistry<ItemDefinition> items, List<ItemStack> drops)
    {
        void Add(ResourceKey key, int min, int max)
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
                Add(mob.IsOnFire ? BuiltInItems.CookedPorkchop : BuiltInItems.RawPorkchop, 1, 3);
                break;
            case MobType.Cow:
                Add(mob.IsOnFire ? BuiltInItems.CookedBeef : BuiltInItems.RawBeef, 1, 3);
                Add(BuiltInItems.Leather, 0, 2);
                break;
            case MobType.Sheep:
                Add(mob.IsOnFire ? BuiltInItems.CookedMutton : BuiltInItems.RawMutton, 1, 2);
                if (!mob.IsSheared)
                {
                    Add(OverHorizon.Core.Blocks.BuiltInBlocks.WhiteWool, 1, 1);
                }

                break;
            case MobType.Chicken:
                Add(mob.IsOnFire ? BuiltInItems.CookedChicken : BuiltInItems.RawChicken, 1, 1);
                Add(BuiltInItems.Feather, 1, 2);
                break;
            case MobType.Zombie:
                Add(BuiltInItems.RottenFlesh, 1, 2);
                break;
            case MobType.Skeleton:
                Add(BuiltInItems.Bone, 1, 2);
                Add(BuiltInItems.Arrow, 0, 2);
                break;
            case MobType.Spider:
                Add(BuiltInItems.String, 1, 2);
                Add(BuiltInItems.SpiderEye, 0, 1);
                break;
            case MobType.Creeper:
                Add(BuiltInItems.Gunpowder, 1, 2);
                break;
            case MobType.ZombifiedPiglin:
                Add(BuiltInItems.RottenFlesh, 1, 2);
                Add(BuiltInItems.GoldIngot, 0, 1);
                break;
            case MobType.MagmaCube:
                Add(BuiltInBlocks.MagmaBlock, 0, 1);
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
}
