using OverHorizon.Core.Registry;

namespace OverHorizon.Core.Items;

/// <summary>
/// Immutable descriptor of an item type and its stacking or block-placement rules.
/// </summary>
/// <param name="Key">Unique item resource identifier.</param>
/// <param name="MaxStackSize">Maximum stack count in a single inventory slot (<c>1..99</c>).</param>
/// <param name="PlacedBlockKey">Optional block placed when using this item.</param>
public sealed record ItemDefinition(
    ResourceKey Key,
    int MaxStackSize = 64,
    ResourceKey? PlacedBlockKey = null);
