using System.Numerics;
using OverHorizon.Core.Math;

namespace OverHorizon.Gameplay.Effects;

/// <summary>
/// Zero-GC bounded 3D particle emitter &amp; physics simulator supporting graphics density presets (<c>0=All, 1=Decreased, 2=Minimal</c>).
/// </summary>
public sealed class ParticleSystem
{
    private const int MaxCapacity = 1024;
    private readonly ActiveParticle[] _particles = new ActiveParticle[MaxCapacity];
    private int _count;
    private uint _rng = 0xA341316CU;

    /// <summary>Gets or sets particle density level (<c>0 = All, 1 = Decreased, 2 = Minimal</c>).</summary>
    public int DensityMode { get; set; }

    /// <summary>Gets active particle slice for zero-allocation rendering.</summary>
    public ReadOnlySpan<ActiveParticle> ActiveParticles => _particles.AsSpan(0, _count);

    /// <summary>Gets the current number of live particles.</summary>
    public int Count => _count;

    /// <summary>Clears all live particles.</summary>
    public void Clear() => _count = 0;

    /// <summary>Emits textured voxel debris chips when a block at <paramref name="pos"/> is broken or sprinted on.</summary>
    public void EmitBlockBreak(BlockPos pos, int atlasTileIndex, Vector3 tint, int baseCount = 14)
    {
        var count = ScaleCount(baseCount, allowInMinimal: true);
        var center = new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5);
        for (var i = 0; i < count; i++)
        {
            var ox = (NextFloat() - 0.5f) * 0.68f;
            var oy = (NextFloat() - 0.5f) * 0.68f;
            var oz = (NextFloat() - 0.5f) * 0.68f;
            var vel = new Vector3(ox * 4.8f, 1.8f + (NextFloat() * 2.6f), oz * 4.8f);
            Emit(new ActiveParticle
            {
                Position = center + new Vec3d(ox, oy, oz),
                Velocity = vel,
                Color = tint,
                Size = 0.075f + (NextFloat() * 0.045f),
                MaxLifetime = 0.48f + (NextFloat() * 0.38f),
                GravityScale = 1.0f,
                AtlasTileIndex = atlasTileIndex,
                SubUvOffset = new Vector2((NextRandom() & 3U) * 0.25f, ((NextRandom() >> 2) & 3U) * 0.25f),
                Kind = ParticleKind.BlockDebris,
            });
        }
    }

    /// <summary>Emits an explosion shockwave cloud of fire embers and billowing smoke.</summary>
    public void EmitExplosion(Vec3d center)
    {
        var count = ScaleCount(42, allowInMinimal: true);
        for (var i = 0; i < count; i++)
        {
            var dir = RandomUnitVector();
            var speed = 2.5f + (NextFloat() * 9.5f);
            var isFlame = (i & 3) == 0;
            var shade = 0.55f + (NextFloat() * 0.40f);
            Emit(new ActiveParticle
            {
                Position = center + new Vec3d(dir.X * 0.4, dir.Y * 0.4, dir.Z * 0.4),
                Velocity = dir * speed,
                Color = isFlame ? new Vector3(1.0f, 0.65f + (NextFloat() * 0.28f), 0.18f) : new Vector3(shade, shade, shade),
                Size = isFlame ? 0.16f : 0.24f + (NextFloat() * 0.16f),
                MaxLifetime = 0.55f + (NextFloat() * 0.75f),
                GravityScale = isFlame ? 0.15f : -0.08f,
                AtlasTileIndex = -1,
                Kind = isFlame ? ParticleKind.Flame : ParticleKind.ExplosionBurst,
            });
        }
    }

    /// <summary>Emits melee critical-hit golden sparks or damage sparks on an entity.</summary>
    public void EmitCombatHit(Vec3d targetCenter, bool isCritical)
    {
        var count = ScaleCount(isCritical ? 14 : 7, allowInMinimal: false);
        for (var i = 0; i < count; i++)
        {
            var dir = RandomUnitVector();
            Emit(new ActiveParticle
            {
                Position = targetCenter + new Vec3d(dir.X * 0.25, 0.6 + (dir.Y * 0.35), dir.Z * 0.25),
                Velocity = new Vector3(dir.X * 3.8f, 1.5f + MathF.Abs(dir.Y * 2.8f), dir.Z * 3.8f),
                Color = isCritical ? new Vector3(1.0f, 0.88f, 0.32f) : new Vector3(0.92f, 0.24f, 0.22f),
                Size = isCritical ? 0.085f : 0.065f,
                MaxLifetime = 0.42f + (NextFloat() * 0.22f),
                GravityScale = 0.65f,
                AtlasTileIndex = -1,
                Kind = ParticleKind.CritSpark,
            });
        }
    }

    /// <summary>Emits flame &amp; smoke wisps (Torches, Fire, Burning mobs, Primed TNT &amp; Creeper fuses).</summary>
    public void EmitFlameAndSmoke(Vec3d origin, bool smokeOnly = false)
    {
        if (DensityMode >= 2) return;
        if (!smokeOnly)
        {
            Emit(new ActiveParticle
            {
                Position = origin + new Vec3d((NextFloat() - 0.5f) * 0.22f, NextFloat() * 0.15f, (NextFloat() - 0.5f) * 0.22f),
                Velocity = new Vector3((NextFloat() - 0.5f) * 0.35f, 0.65f + (NextFloat() * 0.55f), (NextFloat() - 0.5f) * 0.35f),
                Color = new Vector3(1.0f, 0.62f + (NextFloat() * 0.30f), 0.14f),
                Size = 0.07f + (NextFloat() * 0.03f),
                MaxLifetime = 0.45f + (NextFloat() * 0.25f),
                GravityScale = -0.05f,
                AtlasTileIndex = -1,
                Kind = ParticleKind.Flame,
            });
        }

        Emit(new ActiveParticle
        {
            Position = origin + new Vec3d((NextFloat() - 0.5f) * 0.24f, 0.12f, (NextFloat() - 0.5f) * 0.24f),
            Velocity = new Vector3((NextFloat() - 0.5f) * 0.45f, 1.05f + (NextFloat() * 0.65f), (NextFloat() - 0.5f) * 0.45f),
            Color = new Vector3(0.28f, 0.28f, 0.30f),
            Size = 0.085f + (NextFloat() * 0.04f),
            MaxLifetime = 0.65f + (NextFloat() * 0.35f),
            GravityScale = -0.10f,
            AtlasTileIndex = -1,
            Kind = ParticleKind.Smoke,
        });
    }

    /// <summary>Emits water splash droplets, Redstone sparks, Bone Meal sparkles, or Void motes.</summary>
    public void EmitBurst(Vec3d origin, ParticleKind kind, int baseCount = 8)
    {
        var count = ScaleCount(baseCount, allowInMinimal: false);
        var color = kind switch
        {
            ParticleKind.WaterSplash => new Vector3(0.28f, 0.62f, 0.98f),
            ParticleKind.RedstoneSpark => new Vector3(0.95f, 0.14f, 0.12f),
            ParticleKind.GrowthSparkle => new Vector3(0.24f, 0.94f, 0.38f),
            _ => new Vector3(0.22f, 0.16f, 0.28f),
        };
        for (var i = 0; i < count; i++)
        {
            var dir = RandomUnitVector();
            var grav = kind == ParticleKind.WaterSplash ? 0.95f : -0.06f;
            Emit(new ActiveParticle
            {
                Position = origin + new Vec3d(dir.X * 0.28, MathF.Abs(dir.Y) * 0.25, dir.Z * 0.28),
                Velocity = new Vector3(dir.X * 2.2f, 0.8f + MathF.Abs(dir.Y * 2.2f), dir.Z * 2.2f),
                Color = color,
                Size = 0.065f + (NextFloat() * 0.03f),
                MaxLifetime = 0.45f + (NextFloat() * 0.35f),
                GravityScale = grav,
                AtlasTileIndex = -1,
                Kind = kind,
            });
        }
    }

    /// <summary>Steps all live particles and compacts expired entries in O(N).</summary>
    public void Step(float dt)
    {
        var clampedDt = Math.Clamp(dt, 0.001f, 0.08f);
        var drag = MathF.Pow(0.91f, clampedDt * 20.0f);
        for (var i = _count - 1; i >= 0; i--)
        {
            ref var p = ref _particles[i];
            p.Age += clampedDt;
            if (p.Age >= p.MaxLifetime)
            {
                _particles[i] = _particles[--_count];
                continue;
            }

            var vel = p.Velocity;
            vel.Y -= 16.5f * p.GravityScale * clampedDt;
            vel *= drag;
            p.Velocity = vel;
            p.Position += new Vec3d(vel.X * clampedDt, vel.Y * clampedDt, vel.Z * clampedDt);
        }
    }

    private void Emit(in ActiveParticle p)
    {
        if (_count >= MaxCapacity)
        {
            _particles[(int)(NextRandom() % MaxCapacity)] = p;
            return;
        }

        _particles[_count++] = p;
    }

    private int ScaleCount(int baseCount, bool allowInMinimal) => DensityMode switch
    {
        0 => baseCount,
        1 => Math.Max(2, baseCount / 2),
        _ => allowInMinimal ? Math.Max(2, baseCount / 4) : 0,
    };

    private Vector3 RandomUnitVector()
    {
        var z = (NextFloat() * 2.0f) - 1.0f;
        var a = NextFloat() * MathF.Tau;
        var r = MathF.Sqrt(MathF.Max(0.0f, 1.0f - (z * z)));
        return new Vector3(r * MathF.Cos(a), z, r * MathF.Sin(a));
    }

    private float NextFloat() => (NextRandom() & 0xFFFFU) / 65535.0f;

    private uint NextRandom()
    {
        _rng ^= _rng << 13;
        _rng ^= _rng >> 17;
        _rng ^= _rng << 5;
        return _rng;
    }
}
