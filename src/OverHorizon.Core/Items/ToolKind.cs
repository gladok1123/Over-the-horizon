namespace OverHorizon.Core.Items;

/// <summary>
/// Harvesting tool or melee weapon archetype for an <see cref="ItemDefinition"/>.
/// </summary>
public enum ToolKind : byte
{
    /// <summary>Not a harvesting tool or melee weapon.</summary>
    None = 0,

    /// <summary>Melee sword (high entity/PvP damage, slices cobwebs &amp; foliage).</summary>
    Sword = 1,

    /// <summary>Pickaxe (mines stone, deepslate, furnace, and mineral ores).</summary>
    Pickaxe = 2,

    /// <summary>Axe (chops logs, planks, fences, chests, and crafting tables).</summary>
    Axe = 3,

    /// <summary>Shovel (excavates dirt, grass, sand, gravel, and snow).</summary>
    Shovel = 4,

    /// <summary>Shears (harvests leaves, cobwebs, wool, and shears live sheep).</summary>
    Shears = 5,

    /// <summary>Hoe (tills grass/dirt into farmland and harvests hay/leaves fast).</summary>
    Hoe = 6,
}
