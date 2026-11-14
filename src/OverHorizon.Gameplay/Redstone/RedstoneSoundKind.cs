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

    /// <summary>Wooden door swung open by Redstone.</summary>
    WoodenDoorOpen = 4,

    /// <summary>Wooden door swung closed by Redstone.</summary>
    WoodenDoorClose = 5,

    /// <summary>Iron door swung open by Redstone.</summary>
    IronDoorOpen = 6,

    /// <summary>Iron door swung closed by Redstone.</summary>
    IronDoorClose = 7,

    /// <summary>Wooden trapdoor opened by Redstone.</summary>
    WoodenTrapdoorOpen = 8,

    /// <summary>Wooden trapdoor closed by Redstone.</summary>
    WoodenTrapdoorClose = 9,

    /// <summary>Iron trapdoor opened by Redstone.</summary>
    IronTrapdoorOpen = 10,

    /// <summary>Iron trapdoor closed by Redstone.</summary>
    IronTrapdoorClose = 11,
}
