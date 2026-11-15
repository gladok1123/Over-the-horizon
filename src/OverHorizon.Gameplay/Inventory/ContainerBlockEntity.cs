using OverHorizon.Core.Items;
using OverHorizon.Core.Math;

namespace OverHorizon.Gameplay.Inventory;

/// <summary>
/// Stateful <c>27</c>-slot (<c>9x3</c>) storage block-entity backing vanilla Barrels and Chests.
/// Tracks the number of players currently viewing it so the block can drive the original <c>open</c>
/// block-state property and the authentic <c>block/barrel/{open,close}</c> / <c>block/chest/{open,close}</c> sound cues.
/// </summary>
public sealed class ContainerBlockEntity
{
    private readonly ItemStack[] _slots = new ItemStack[SlotCount];

    /// <summary>Initializes a new instance of the <see cref="ContainerBlockEntity"/> class at <paramref name="position"/>.</summary>
    /// <param name="position">World block coordinate of the container.</param>
    public ContainerBlockEntity(BlockPos position)
    {
        Position = position;
    }

    /// <summary>Vanilla Barrel and small Chest storage capacity (<c>9</c> columns x <c>3</c> rows).</summary>
    public const int SlotCount = 27;

    /// <summary>Gets the world block coordinate of this container.</summary>
    public BlockPos Position { get; }

    /// <summary>Gets or sets how many viewers currently have this container open (drives the <c>open</c> block property).</summary>
    public int ViewerCount { get; set; }

    /// <summary>Gets whether at least one viewer currently has this container open.</summary>
    public bool IsOpen => ViewerCount > 0;

    /// <summary>Gets whether every slot is empty (used to skip empty containers when saving).</summary>
    public bool IsEmpty
    {
        get
        {
            for (var i = 0; i < SlotCount; i++)
            {
                if (!_slots[i].IsEmpty)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>Gets the item stack stored in <paramref name="slotIndex"/>.</summary>
    /// <param name="slotIndex">Zero-based slot index in <c>[0, 27)</c>.</param>
    /// <returns>The stored stack, or <see cref="ItemStack.Empty"/> when out of range.</returns>
    public ItemStack GetSlot(int slotIndex) => (uint)slotIndex < (uint)SlotCount ? _slots[slotIndex] : ItemStack.Empty;

    /// <summary>Overwrites <paramref name="slotIndex"/> with <paramref name="stack"/>.</summary>
    /// <param name="slotIndex">Zero-based slot index in <c>[0, 27)</c>.</param>
    /// <param name="stack">Stack to store.</param>
    public void SetSlot(int slotIndex, ItemStack stack)
    {
        if ((uint)slotIndex < (uint)SlotCount)
        {
            _slots[slotIndex] = stack;
        }
    }

    /// <summary>Marks one more viewer as having the screen open.</summary>
    public void AddViewer() => ViewerCount++;

    /// <summary>
    /// Removes one viewer and reports whether the container is now fully closed
    /// (so the caller can flip the <c>open</c> block property back and play the close cue).
    /// </summary>
    /// <returns><see langword="true"/> when the last viewer closed the container.</returns>
    public bool RemoveViewer()
    {
        if (ViewerCount > 0)
        {
            ViewerCount--;
        }

        return ViewerCount <= 0;
    }

    /// <summary>
    /// Hands every stored stack to <paramref name="receive"/> and clears the container
    /// (vanilla behaviour when a Barrel or Chest is broken).
    /// </summary>
    /// <param name="receive">Sink invoked once per non-empty stack.</param>
    public void DropAllContents(Action<ItemStack> receive)
    {
        ArgumentNullException.ThrowIfNull(receive);

        for (var i = 0; i < SlotCount; i++)
        {
            if (_slots[i].IsEmpty)
            {
                continue;
            }

            receive(_slots[i]);
            _slots[i] = ItemStack.Empty;
        }
    }
}
