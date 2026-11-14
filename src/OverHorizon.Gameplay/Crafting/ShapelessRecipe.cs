using System.Collections.Immutable;
using OverHorizon.Core.Items;
using OverHorizon.Core.Registry;

namespace OverHorizon.Gameplay.Crafting;

/// <summary>
/// Order- and position-independent shapeless crafting recipe matching an exact multiset of item IDs.
/// </summary>
public sealed class ShapelessRecipe : ICraftingRecipe
{
    private readonly ImmutableArray<ushort> _sortedIngredients;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShapelessRecipe"/> class.
    /// </summary>
    /// <param name="key">Unique recipe key.</param>
    /// <param name="ingredients">1 to 9 non-zero required item IDs.</param>
    /// <param name="output">Non-empty crafted output.</param>
    public ShapelessRecipe(ResourceKey key, ReadOnlySpan<ushort> ingredients, ItemStack output)
    {
        if (ingredients.IsEmpty || ingredients.Length > 9)
        {
            throw new ArgumentException("Shapeless recipe must have between 1 and 9 ingredients.", nameof(ingredients));
        }

        if (output.IsEmpty)
        {
            throw new ArgumentException("Recipe output cannot be empty.", nameof(output));
        }

        var copy = ingredients.ToArray();
        for (var i = 0; i < copy.Length; i++)
        {
            if (copy[i] == 0)
            {
                throw new ArgumentException("Shapeless ingredient IDs must be non-zero.", nameof(ingredients));
            }
        }

        Array.Sort(copy);
        Key = key;
        _sortedIngredients = [.. copy];
        Output = output;
    }

    /// <inheritdoc />
    public ResourceKey Key { get; }

    /// <inheritdoc />
    public ItemStack Output { get; }

    /// <inheritdoc />
    public bool Matches(CraftingGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        Span<ushort> found = stackalloc ushort[9];
        var count = 0;

        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                var stack = grid.GetSlot(x, y);
                if (!stack.IsEmpty)
                {
                    if (count >= _sortedIngredients.Length)
                    {
                        return false;
                    }

                    found[count++] = stack.ItemId;
                }
            }
        }

        if (count != _sortedIngredients.Length)
        {
            return false;
        }

        var slice = found[..count];
        slice.Sort();
        return slice.SequenceEqual(_sortedIngredients.AsSpan());
    }
}
