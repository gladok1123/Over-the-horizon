namespace OverHorizon.Audio;

/// <summary>
/// Built-in clean-room synthesized voxel sound effect identifiers.
/// </summary>
public enum SoundCue : byte
{
    /// <summary>Crisp mineral tap footstep.</summary>
    FootstepStone = 0,

    /// <summary>Soft rustling turf footstep.</summary>
    FootstepGrass = 1,

    /// <summary>Hollow timber knock footstep.</summary>
    FootstepWood = 2,

    /// <summary>Crunchy block fracture burst.</summary>
    BlockBreak = 3,

    /// <summary>Solid block placement thud.</summary>
    BlockPlace = 4,

    /// <summary>Band-limited fluid splash.</summary>
    WaterSplash = 5,

    /// <summary>Short UI click.</summary>
    UiClick = 6,
}
