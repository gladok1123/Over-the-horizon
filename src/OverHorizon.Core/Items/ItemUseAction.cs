namespace OverHorizon.Core.Items;

/// <summary>
/// Right-click primary action behavior for an <see cref="ItemDefinition"/>.
/// </summary>
public enum ItemUseAction : byte
{
    /// <summary>No right-click action.</summary>
    None = 0,

    /// <summary>Places <see cref="ItemDefinition.PlacedBlockKey"/> onto the targeted voxel face.</summary>
    PlaceBlock = 1,

    /// <summary>Consumes 1 item to restore hunger, saturation, and optional regeneration.</summary>
    EatFood = 2,

    /// <summary>Scoops a water source block into a <c>water_bucket</c>.</summary>
    EmptyBucket = 3,

    /// <summary>Pours a water source block onto the targeted face and returns an empty <c>bucket</c>.</summary>
    WaterBucket = 4,

    /// <summary>Ignites targeted TNT or strikes sparks.</summary>
    IgniteTnt = 5,

    /// <summary>Tills targeted Dirt or Grass Block into Farmland (Hoe).</summary>
    TillSoil = 6,

    /// <summary>Equips this armor piece into the matching player armor slot (<c>36..39</c>).</summary>
    EquipArmor = 7,

    /// <summary>Places a rideable 3D <c>Minecart</c> onto a targeted <c>Rail</c> or <c>PoweredRail</c>.</summary>
    PlaceMinecart = 8,

    /// <summary>Draws a <c>Bow</c> while right-mouse is held and releases a ballistic 3D <c>Arrow</c>.</summary>
    DrawBow = 9,
}
