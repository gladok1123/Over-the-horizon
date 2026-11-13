using OverHorizon.Core.Registry;

namespace OverHorizon.Core.Items;

/// <summary>
/// Immutable descriptor of an item type including stacking, block placement, PvP/mob weapon damage,
/// tool tier &amp; speed, food nutrition, dynamic held light emission, and armor defense.
/// </summary>
/// <param name="Key">Unique item resource identifier.</param>
/// <param name="MaxStackSize">Maximum stack count in a single inventory slot (<c>0..99</c>).</param>
/// <param name="PlacedBlockKey">Optional block placed when using this item.</param>
/// <param name="DisplayName">Human-readable English display label.</param>
/// <param name="Category">Creative &amp; catalog tab classification.</param>
/// <param name="ToolType">Tool harvesting or melee weapon archetype.</param>
/// <param name="AttackDamage">Melee PvP &amp; mob attack damage in half-hearts (<c>1.0 = 0.5 hearts</c>).</param>
/// <param name="MiningSpeedMultiplier">Harvest speed multiplier on matching block materials.</param>
/// <param name="FoodNutrition">Hunger points restored when eaten (<c>0..20</c>).</param>
/// <param name="FoodSaturation">Saturation points restored when eaten.</param>
/// <param name="BonusHealPoints">Instant bonus HP healed when eaten (e.g. Golden Apple).</param>
/// <param name="UseAction">Right-click primary usage behavior.</param>
/// <param name="HeldLightEmission">Dynamic block light (<c>0..15</c>) emitted when held in the player's hand.</param>
/// <param name="ToolTier">Harvest tier (<c>0=hand/wood/gold, 1=stone, 2=iron, 3=diamond, 4=netherite</c>).</param>
/// <param name="ArmorDefense">Armor defense points (<c>0..8</c>) when worn.</param>
/// <param name="ArmorSlotOffset">Armor slot offset (<c>-1=not armor, 0=helmet, 1=chestplate, 2=leggings, 3=boots</c>).</param>
public sealed record ItemDefinition(
    ResourceKey Key,
    int MaxStackSize = 64,
    ResourceKey? PlacedBlockKey = null,
    string DisplayName = "",
    ItemCategory Category = ItemCategory.BuildingBlocks,
    ToolKind ToolType = ToolKind.None,
    float AttackDamage = 1.0f,
    float MiningSpeedMultiplier = 1.0f,
    int FoodNutrition = 0,
    float FoodSaturation = 0.0f,
    float BonusHealPoints = 0.0f,
    ItemUseAction UseAction = ItemUseAction.None,
    byte HeldLightEmission = 0,
    int ToolTier = 0,
    int ArmorDefense = 0,
    int ArmorSlotOffset = -1)
{
    /// <summary>Gets the effective human-readable display name.</summary>
    public string EffectiveName => string.IsNullOrEmpty(DisplayName) ? FormatKeyPath(Key.Path) : DisplayName;

    private static string FormatKeyPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "Unknown";
        }

        Span<char> buf = stackalloc char[System.Math.Min(64, path.Length)];
        var capitalize = true;
        for (var i = 0; i < buf.Length; i++)
        {
            var c = path[i];
            if (c == '_')
            {
                buf[i] = ' ';
                capitalize = true;
            }
            else if (capitalize)
            {
                buf[i] = char.ToUpperInvariant(c);
                capitalize = false;
            }
            else
            {
                buf[i] = c;
            }
        }

        return new string(buf);
    }
}
