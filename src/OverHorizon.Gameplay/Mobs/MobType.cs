namespace OverHorizon.Gameplay.Mobs;

/// <summary>
/// Built-in peaceful and hostile mob species in Over-the-Horizon.
/// </summary>
public enum MobType : byte
{
    /// <summary>Peaceful farm pig (drops raw porkchop).</summary>
    Pig = 0,

    /// <summary>Peaceful farm cow (drops raw beef &amp; leather).</summary>
    Cow = 1,

    /// <summary>Peaceful woolly sheep (drops raw mutton &amp; white wool; shearable).</summary>
    Sheep = 2,

    /// <summary>Peaceful small chicken (drops raw chicken &amp; feather).</summary>
    Chicken = 3,

    /// <summary>Hostile undead zombie (spawns in dark caves/night, drops rotten flesh).</summary>
    Zombie = 4,

    /// <summary>Hostile skeletal archer/warrior (spawns in dark caves/night, drops bone).</summary>
    Skeleton = 5,

    /// <summary>Hostile cave/mineshaft spider (climbs &amp; leaps, drops string &amp; spider eye).</summary>
    Spider = 6,

    /// <summary>Hostile silent creeper (fuses near Survival players, drops gunpowder).</summary>
    Creeper = 7,

    /// <summary>Nether-exclusive undead piglin (spawns in the Nether, drops rotten flesh; immune to fire).</summary>
    ZombifiedPiglin = 8,

    /// <summary>Nether-exclusive hopping lava cube (spawns in the Nether, drops magma cream; immune to fire and fall damage).</summary>
    MagmaCube = 9,
}
