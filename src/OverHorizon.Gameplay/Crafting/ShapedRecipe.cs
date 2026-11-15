using System.Collections.Immutable;
using OverHorizon.Core.Items;
using OverHorizon.Core.Registry;

namespace OverHorizon.Gameplay.Crafting;

/// <summary>
/// Position-independent (bounding-box trimmed) shaped crafting recipe for 2x2 and 3x3 grids.
/// </summary>
public sealed class ShapedRecipe : ICraftingRecipe
{
    private readonly ImmutableArray<ushort> _pattern;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShapedRecipe"/> class.
    /// </summary>
    /// <param name="key">Unique recipe key.</param>
    /// <param name="patternWidth">Pattern width in <c>[1, 3]</c>.</param>
    /// <param name="patternHeight">Pattern height in <c>[1, 3]</c>.</param>
    /// <param name="pattern">Row-major item IDs of length <c>patternWidth * patternHeight</c> (<c>0</c> = empty).</param>
    /// <param name="output">Non-empty output <see cref="ItemStack"/>.</param>
    /// <param name="allowHorizontalMirror">Whether horizontally mirrored placement is also accepted.</param>
    public ShapedRecipe(
        ResourceKey key,
        int patternWidth,
        int patternHeight,
        ReadOnlySpan<ushort> pattern,
        ItemStack output,
        bool allowHorizontalMirror = true)
    {
        if (patternWidth is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(patternWidth));
        }

        if (patternHeight is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(patternHeight));
        }

        if (pattern.Length != patternWidth * patternHeight)
        {
            throw new ArgumentException("Pattern length must equal patternWidth * patternHeight.", nameof(pattern));
        }

        if (output.IsEmpty)
        {
            throw new ArgumentException("Recipe output cannot be empty.", nameof(output));
        }

        Key = key;
        PatternWidth = patternWidth;
        PatternHeight = patternHeight;
        _pattern = [.. pattern];
        Output = output;
        AllowHorizontalMirror = allowHorizontalMirror;
    }

    /// <inheritdoc />
    public ResourceKey Key { get; }

    /// <summary>Gets the width of the recipe pattern.</summary>
    public int PatternWidth { get; }

    /// <summary>Gets the height of the recipe pattern.</summary>
    public int PatternHeight { get; }

    /// <summary>Gets a value indicating whether horizontal mirroring is allowed.</summary>
    public bool AllowHorizontalMirror { get; }

    /// <inheritdoc />
    public ItemStack Output { get; }

    /// <inheritdoc />
    public bool Matches(CraftingGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        var (minX, minY, trimmedW, trimmedH) = grid.GetTrimmedBounds();
        if (trimmedW != PatternWidth || trimmedH != PatternHeight)
        {
            return false;
        }

        if (MatchesAt(grid, minX, minY, mirrorX: false))
        {
            return true;
        }

        return AllowHorizontalMirror && PatternWidth > 1 && MatchesAt(grid, minX, minY, mirrorX: true);
    }

    private bool MatchesAt(CraftingGrid grid, int offsetX, int offsetY, bool mirrorX)
    {
        for (var py = 0; py < PatternHeight; py++)
        {
            for (var px = 0; px < PatternWidth; px++)
            {
                var patternX = mirrorX ? (PatternWidth - 1) - px : px;
                var expectedId = _pattern[(py * PatternWidth) + patternX];
                var actualId = grid.GetSlot(offsetX + px, offsetY + py).ItemId;

                if (expectedId != actualId)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
