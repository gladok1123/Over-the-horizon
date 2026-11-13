using System.Numerics;
using OverHorizon.Core.Math;

namespace OverHorizon.Gameplay.Effects;

/// <summary>
/// Compact value-type particle state for zero-GC simulation and camera-billboard rendering.
/// </summary>
public struct ActiveParticle
{
    /// <summary>World-space center position.</summary>
    public Vec3d Position;

    /// <summary>Linear velocity in blocks/second.</summary>
    public Vector3 Velocity;

    /// <summary>RGB tint color.</summary>
    public Vector3 Color;

    /// <summary>Billboard half-size in world blocks.</summary>
    public float Size;

    /// <summary>Elapsed age in seconds.</summary>
    public float Age;

    /// <summary>Maximum lifetime in seconds.</summary>
    public float MaxLifetime;

    /// <summary>Downward gravity multiplier.</summary>
    public float GravityScale;

    /// <summary>Atlas tile index (<c>&gt;= 0</c> for textured block debris, <c>-1</c> for untextured color quad).</summary>
    public int AtlasTileIndex;

    /// <summary>Sub-tile UV offset (<c>0..0.75</c>) for textured block debris chips.</summary>
    public Vector2 SubUvOffset;

    /// <summary>Particle behavior kind.</summary>
    public ParticleKind Kind;
}
