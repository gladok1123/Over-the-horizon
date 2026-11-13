using System.Numerics;
using OverHorizon.Core.Math;

namespace OverHorizon.Gameplay.Effects;

/// <summary>
/// Compact value-type particle state for zero-GC simulation and camera-billboard rendering.
/// </summary>
public struct ActiveParticle
{
    /// <summary>Gets or sets world-space center position.</summary>
    public Vec3d Position { get; set; }

    /// <summary>Gets or sets linear velocity in blocks/second.</summary>
    public Vector3 Velocity { get; set; }

    /// <summary>Gets or sets RGB tint color.</summary>
    public Vector3 Color { get; set; }

    /// <summary>Gets or sets billboard half-size in world blocks.</summary>
    public float Size { get; set; }

    /// <summary>Gets or sets elapsed age in seconds.</summary>
    public float Age { get; set; }

    /// <summary>Gets or sets maximum lifetime in seconds.</summary>
    public float MaxLifetime { get; set; }

    /// <summary>Gets or sets downward gravity multiplier.</summary>
    public float GravityScale { get; set; }

    /// <summary>Gets or sets atlas tile index (<c>&gt;= 0</c> for textured block debris, <c>-1</c> for untextured color quad).</summary>
    public int AtlasTileIndex { get; set; }

    /// <summary>Gets or sets sub-tile UV offset (<c>0..0.75</c>) for textured block debris chips.</summary>
    public Vector2 SubUvOffset { get; set; }

    /// <summary>Gets or sets particle behavior kind.</summary>
    public ParticleKind Kind { get; set; }
}
