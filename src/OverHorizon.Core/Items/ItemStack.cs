namespace OverHorizon.Core.Items;

/// <summary>
/// Immutable value-type item stack with copy-on-write operations (ADR 0006).
/// Raw ID <c>0</c> or count <c>&lt;= 0</c> always normalizes to <see cref="Empty"/>.
/// </summary>
public readonly record struct ItemStack
{
    /// <summary>
    /// Initializes a new normalized instance of the <see cref="ItemStack"/> struct.
    /// </summary>
    /// <param name="itemId">Dense item registry ID (<c>0</c> = air/empty).</param>
    /// <param name="count">Stack count.</param>
    public ItemStack(ushort itemId, int count)
    {
        if (itemId == 0 || count <= 0)
        {
            ItemId = 0;
            Count = 0;
        }
        else
        {
            ItemId = itemId;
            Count = count;
        }
    }

    /// <summary>
    /// Gets the canonical empty stack (<c>ItemId == 0, Count == 0</c>).
    /// </summary>
    public static ItemStack Empty => default;

    /// <summary>
    /// Gets the dense item registry ID (<c>0</c> when empty).
    /// </summary>
    public ushort ItemId { get; }

    /// <summary>
    /// Gets the number of items in the stack (<c>0</c> when empty).
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Gets a value indicating whether this stack contains zero items.
    /// </summary>
    public bool IsEmpty => Count == 0;

    /// <summary>
    /// Returns a new <see cref="ItemStack"/> with an updated count (copy-on-write).
    /// </summary>
    /// <param name="newCount">Desired count (<c>&lt;= 0</c> yields <see cref="Empty"/>).</param>
    /// <returns>Updated immutable <see cref="ItemStack"/>.</returns>
    public ItemStack WithCount(int newCount) => new(ItemId, newCount);

    /// <summary>
    /// Determines whether two non-empty stacks represent the same item type and can merge.
    /// </summary>
    /// <param name="other">Other stack.</param>
    /// <returns><see langword="true"/> if both are non-empty with identical <see cref="ItemId"/>.</returns>
    public bool CanMergeWith(ItemStack other) => !IsEmpty && !other.IsEmpty && ItemId == other.ItemId;

    /// <summary>
    /// Splits up to <paramref name="amount"/> items into a taken stack and a remaining stack without mutation.
    /// </summary>
    /// <param name="amount">Requested quantity to split.</param>
    /// <returns>Tuple of <c>(Taken, Remaining)</c> stacks.</returns>
    public (ItemStack Taken, ItemStack Remaining) Split(int amount)
    {
        if (IsEmpty || amount <= 0)
        {
            return (Empty, this);
        }

        var actual = System.Math.Min(amount, Count);
        return (new ItemStack(ItemId, actual), new ItemStack(ItemId, Count - actual));
    }
}
