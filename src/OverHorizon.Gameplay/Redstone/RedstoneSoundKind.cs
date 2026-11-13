namespace OverHorizon.Gameplay.Redstone;

/// <summary>
/// Kind of mechanical or electrical sound event emitted by <see cref="RedstoneEngine"/>.
/// </summary>
public enum RedstoneSoundKind : byte
{
    /// <summary>Lever, button, or pressure plate click.</summary>
    Click = 0,

    /// <summary>Piston extending.</summary>
    PistonExtend = 1,

    /// <summary>Piston retracting.</summary>
    PistonRetract = 2,

    /// <summary>Redstone torch burnout fizz.</summary>
    TorchFizz = 3,
}
