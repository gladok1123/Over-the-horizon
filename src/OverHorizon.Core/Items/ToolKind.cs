namespace OverHorizon.Core.Items;

/// <summary>
/// Tool harvesting and PvP weapon archetype.
/// </summary>
public enum ToolKind : byte
{
    /// <summary>Not a specialized tool or weapon.</summary>
    None = 0,

    /// <summary>Accelerates mining stone, brick, ore, and obsidian blocks.</summary>
    Pickaxe = 1,

    /// <summary>Accelerates chopping logs, planks, bookshelves, and crafting tables.</summary>
    Axe = 2,

    /// <summary>Accelerates digging dirt, grass, sand, gravel, and snow.</summary>
    Shovel = 3,

    /// <summary>High-damage PvP melee weapon.</summary>
    Sword = 4,

    /// <summary>Rapidly harvests leaves and wool blocks.</summary>
    Shears = 5,
}
