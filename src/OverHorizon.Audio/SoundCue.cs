namespace OverHorizon.Audio;

/// <summary>
/// Built-in voxel gameplay, combat, survival, and UI sound effect identifiers.
/// </summary>
public enum SoundCue : byte
{
    /// <summary>Real recorded stone/mineral footstep.</summary>
    FootstepStone = 0,

    /// <summary>Real recorded grass/turf footstep.</summary>
    FootstepGrass = 1,

    /// <summary>Real recorded wooden plank/log footstep.</summary>
    FootstepWood = 2,

    /// <summary>Real recorded block mining/fracture impact.</summary>
    BlockBreak = 3,

    /// <summary>Real recorded solid block placement thud.</summary>
    BlockPlace = 4,

    /// <summary>Real recorded water splash.</summary>
    WaterSplash = 5,

    /// <summary>Real recorded crisp UI button click.</summary>
    UiClick = 6,

    /// <summary>Real recorded weapon/sword swing whoosh.</summary>
    SwordSwing = 7,

    /// <summary>Real recorded melee PvP hit impact.</summary>
    PlayerHit = 8,

    /// <summary>Real recorded player damage/fall hurt sound.</summary>
    PlayerHurt = 9,

    /// <summary>Real recorded food eating crunch.</summary>
    EatFood = 10,

    /// <summary>Real recorded item pickup/craft pop.</summary>
    ItemPop = 11,

    /// <summary>Real recorded underwater submersion bubble sound.</summary>
    UnderwaterEnter = 12,
}
