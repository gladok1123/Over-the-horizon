using OverHorizon.Core.Registry;

namespace OverHorizon.Gameplay.Crafting;

/// <summary>
/// Describes a Survival quick-craft or field-smelt recipe over a <see cref="Inventory.PlayerInventory"/>.
/// </summary>
/// <param name="Label">Human-readable recipe summary.</param>
/// <param name="OutputItemKey">Produced item resource key.</param>
/// <param name="OutputCount">Number of output items granted per craft.</param>
/// <param name="Ingredients">Required ingredient item keys and quantities.</param>
public sealed record QuickCraftEntry(
    string Label,
    ResourceKey OutputItemKey,
    int OutputCount,
    IReadOnlyList<(ResourceKey ItemKey, int Count)> Ingredients);
