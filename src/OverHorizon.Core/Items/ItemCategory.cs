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

    /// <summary>Utility, light-emitting, crafting, and explosive blocks.</summary>
    FunctionalBlocks = 2,

    /// <summary>PvP melee weapons, harvesting tools, shears, and igniters.</summary>
    CombatAndTools = 3,

    /// <summary>Edible food items, fluid buckets, and crafting materials/ingots.</summary>
    FoodAndMaterials = 4,
}
