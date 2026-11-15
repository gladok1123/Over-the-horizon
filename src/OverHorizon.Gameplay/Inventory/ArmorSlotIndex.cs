namespace OverHorizon.Gameplay.Inventory;

/// <summary>
/// Indices of the four armor equipment slots inside <see cref="PlayerInventory"/>.
/// </summary>
public enum ArmorSlotIndex : byte
{
    /// <summary>Boots slot (offset 0, inventory index 36).</summary>
    Feet = 0,

    /// <summary>Leggings slot (offset 1, inventory index 37).</summary>
    Legs = 1,

    /// <summary>Chestplate slot (offset 2, inventory index 38).</summary>
    Chest = 2,

    /// <summary>Helmet slot (offset 3, inventory index 39).</summary>
    Head = 3,
}
