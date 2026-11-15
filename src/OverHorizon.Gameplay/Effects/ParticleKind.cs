namespace OverHorizon.Gameplay.Effects;

/// <summary>
/// Visual particle archetypes simulated by <see cref="ParticleSystem"/>.
/// </summary>
public enum ParticleKind : byte
{
    /// <summary>Textured 3D block debris chip sampling a 4x4 sub-patch of a block's atlas tile.</summary>
    BlockDebris = 0,

    /// <summary>Warm rising torch/fire/furnace flame ember.</summary>
    Flame = 1,

    /// <summary>Rising gray/dark fuse, fire, or explosion smoke puff.</summary>
    Smoke = 2,

    /// <summary>Bright shockwave burst from TNT or Creeper detonation.</summary>
    ExplosionBurst = 3,

    /// <summary>Golden critical-hit star spark.</summary>
    CritSpark = 4,

    /// <summary>Blue water splash droplet.</summary>
    WaterSplash = 5,

    /// <summary>Glowing red Redstone dust/torch/repeater spark.</summary>
    RedstoneSpark = 6,

    /// <summary>Emerald green Bone Meal / Composter growth sparkle.</summary>
    GrowthSparkle = 7,

    /// <summary>Dark purple/ash Void spore drifting below Bedrock.</summary>
    VoidMote = 8,
}
