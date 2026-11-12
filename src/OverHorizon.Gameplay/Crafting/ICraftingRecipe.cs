using OverHorizon.Core.Items;
using OverHorizon.Core.Registry;

namespace OverHorizon.Gameplay.Crafting;

/// <summary>
/// Represents a deterministic crafting recipe evaluated against a <see cref="CraftingGrid"/>.
/// </summary>
public interface ICraftingRecipe
{
    /// <summary>Gets the unique recipe resource key.</summary>
    ResourceKey Key { get; }

    /// <summary>Gets the crafted output <see cref="ItemStack"/>.</summary>
    ItemStack Output { get; }

    /// <summary>
    /// Determines whether <paramref name="grid"/> satisfies this recipe's pattern or ingredient multiset.
    /// </summary>
    /// <param name="grid">Input crafting grid.</param>
    /// <returns><see langword="true"/> if matched.</returns>
    bool Matches(CraftingGrid grid);
}
