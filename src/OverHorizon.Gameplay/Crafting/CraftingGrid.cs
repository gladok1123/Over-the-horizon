using OverHorizon.Core.Items;

namespace OverHorizon.Gameplay.Crafting;

/// <summary>
/// Represents a 2x2 (inventory) or 3x3 (crafting table) crafting input matrix.
/// </summary>
public sealed class CraftingGrid
{
    private readonly ItemStack[] _cells;

    /// <summary>
    /// Initializes a new instance of the <see cref="CraftingGrid"/> class.
    /// </summary>
    /// <param name="width">Grid width (<c>2</c> or <c>3</c>).</param>
    /// <param name="height">Grid height (<c>2</c> or <c>3</c>).</param>
    public CraftingGrid(int width, int height)
    {
        if (width is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Crafting grid width must be in [1, 3].");
        }

        if (height is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Crafting grid height must be in [1, 3].");
        }

        Width = width;
        Height = height;
        _cells = new ItemStack[width * height];
    }

    /// <summary>Gets the grid width.</summary>
    public int Width { get; }

    /// <summary>Gets the grid height.</summary>
    public int Height { get; }

    /// <summary>Creates a standard 2x2 inventory crafting grid.</summary>
    /// <returns>Empty 2x2 <see cref="CraftingGrid"/>.</returns>
    public static CraftingGrid Create2x2() => new(2, 2);

    /// <summary>Creates a standard 3x3 crafting table grid.</summary>
    /// <returns>Empty 3x3 <see cref="CraftingGrid"/>.</returns>
    public static CraftingGrid Create3x3() => new(3, 3);

    /// <summary>Reads the stack at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    /// <param name="x">Column in <c>[0, Width)</c>.</param>
    /// <param name="y">Row in <c>[0, Height)</c>.</param>
    /// <returns>Stored <see cref="ItemStack"/>.</returns>
    public ItemStack GetSlot(int x, int y)
    {
        ValidateCoords(x, y);
        return _cells[(y * Width) + x];
    }

    /// <summary>Sets the stack at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    /// <param name="x">Column in <c>[0, Width)</c>.</param>
    /// <param name="y">Row in <c>[0, Height)</c>.</param>
    /// <param name="stack">Stack value.</param>
    public void SetSlot(int x, int y, ItemStack stack)
    {
        ValidateCoords(x, y);
        _cells[(y * Width) + x] = stack;
    }

    /// <summary>Resets all slots in the crafting grid to <see cref="ItemStack.Empty"/>.</summary>
    public void Clear() => Array.Clear(_cells);

    /// <summary>
    /// Computes the minimal bounding sub-rectangle enclosing all non-empty slots.
    /// </summary>
    /// <returns>Tuple <c>(MinX, MinY, TrimmedWidth, TrimmedHeight)</c>; widths are <c>0</c> if empty.</returns>
    public (int MinX, int MinY, int TrimmedWidth, int TrimmedHeight) GetTrimmedBounds()
    {
        var minX = Width;
        var minY = Height;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                if (!_cells[(y * Width) + x].IsEmpty)
                {
                    if (x < minX)
                    {
                        minX = x;
                    }

                    if (x > maxX)
                    {
                        maxX = x;
                    }

                    if (y < minY)
                    {
                        minY = y;
                    }

                    if (y > maxY)
                    {
                        maxY = y;
                    }
                }
            }
        }

        return maxX < 0
            ? (0, 0, 0, 0)
            : (minX, minY, (maxX - minX) + 1, (maxY - minY) + 1);
    }

    /// <summary>
    /// Consumes 1 item from every non-empty slot after crafting completes.
    /// </summary>
    public void ConsumeOnePerOccupiedSlot()
    {
        for (var i = 0; i < _cells.Length; i++)
        {
            if (!_cells[i].IsEmpty)
            {
                _cells[i] = _cells[i].WithCount(_cells[i].Count - 1);
            }
        }
    }

    private void ValidateCoords(int x, int y)
    {
        if ((uint)x >= (uint)Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        if ((uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }
    }
}
