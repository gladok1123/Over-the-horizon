namespace OverHorizon.Core.Items;

/// <summary>
/// Creative &amp; Survival catalog tab classification for registered items and blocks.
/// </summary>
public enum ItemCategory : byte
{
    /// <summary>Structural and decorative building blocks.</summary>
    BuildingBlocks = 0,

    /// <summary>Natural terrain, flora, logs, leaves, and mineral ores.</summary>
    NaturalBlocks = 1,

    /// <summary>Utility, light-emitting, crafting, beds, and explosive blocks.</summary>
    FunctionalBlocks = 2,

    /// <summary>PvP melee weapons, harvesting tools, armor, shields, shears, and igniters.</summary>
    CombatAndTools = 3,

    /// <summary>Edible food items, fluid buckets, and crafting materials/ingots.</summary>
    FoodAndMaterials = 4,

    /// <summary>Redstone dust, torches, repeaters, levers, buttons, pressure plates, lamps, and pistons.</summary>
    Redstone = 5,
}
