using OverHorizon.Core.Items;
using OverHorizon.Core.Registry;
using OverHorizon.Shared.Hashing;

namespace OverHorizon.Gameplay.Inventory;

/// <summary>
/// Standard 41-slot player inventory (9 hotbar + 27 storage + 4 armor + 1 offhand) backed by immutable <see cref="ItemStack"/>s.
/// </summary>
public sealed class PlayerInventory
{
    /// <summary>Number of quick-access hotbar slots (<c>0..8</c>).</summary>
    public const int HotbarSlotCount = 9;

    /// <summary>Total main inventory slots (<c>0..35</c>, hotbar + 3x9 storage).</summary>
    public const int MainSlotCount = 36;

    /// <summary>Starting slot index of the 4 armor slots (<c>36..39</c>).</summary>
    public const int ArmorSlotStart = 36;

    /// <summary>Number of armor slots.</summary>
    public const int ArmorSlotCount = 4;

    /// <summary>Slot index of the offhand slot (<c>40</c>).</summary>
    public const int OffhandSlotIndex = 40;

    /// <summary>Total slot capacity (<c>41</c>).</summary>
    public const int TotalSlotCount = 41;

    private readonly ItemStack[] _slots = new ItemStack[TotalSlotCount];
    private int _selectedHotbarSlot;

    /// <summary>
    /// Gets or sets the currently selected hotbar slot index in <c>[0, 8]</c>.
    /// </summary>
    public int SelectedHotbarSlot
    {
        get => _selectedHotbarSlot;
        set
        {
            if ((uint)value >= HotbarSlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Selected hotbar slot must be in [0, 8].");
            }

            _selectedHotbarSlot = value;
        }
    }

    /// <summary>Gets the <see cref="ItemStack"/> currently held in the selected main-hand hotbar slot.</summary>
    public ItemStack MainHandItem => _slots[_selectedHotbarSlot];

    /// <summary>Gets or sets the <see cref="ItemStack"/> in the offhand slot.</summary>
    public ItemStack OffhandItem
    {
        get => _slots[OffhandSlotIndex];
        set => _slots[OffhandSlotIndex] = value;
    }

    /// <summary>
    /// Reads the <see cref="ItemStack"/> at <paramref name="slotIndex"/> (<c>0..40</c>).
    /// </summary>
    /// <param name="slotIndex">Slot index in <c>[0, 40]</c>.</param>
    /// <returns>Stored immutable <see cref="ItemStack"/>.</returns>
    public ItemStack GetSlot(int slotIndex)
    {
        ValidateSlotIndex(slotIndex);
        return _slots[slotIndex];
    }

    /// <summary>
    /// Replaces the <see cref="ItemStack"/> at <paramref name="slotIndex"/> (<c>0..40</c>).
    /// </summary>
    /// <param name="slotIndex">Slot index in <c>[0, 40]</c>.</param>
    /// <param name="stack">New stack value.</param>
    public void SetSlot(int slotIndex, ItemStack stack)
    {
        ValidateSlotIndex(slotIndex);
        _slots[slotIndex] = stack;
    }

    /// <summary>Reads an armor slot.</summary>
    /// <param name="armorSlot">Armor slot identifier.</param>
    /// <returns>Equipped <see cref="ItemStack"/>.</returns>
    public ItemStack GetArmor(ArmorSlotIndex armorSlot) =>
        _slots[ArmorSlotStart + (int)armorSlot];

    /// <summary>Sets an armor slot.</summary>
    /// <param name="armorSlot">Armor slot identifier.</param>
    /// <param name="stack">Armor stack.</param>
    public void SetArmor(ArmorSlotIndex armorSlot, ItemStack stack) =>
        _slots[ArmorSlotStart + (int)armorSlot] = stack;

    /// <summary>
    /// Swaps the contents of two inventory slots.
    /// </summary>
    /// <param name="slotA">First slot index.</param>
    /// <param name="slotB">Second slot index.</param>
    public void SwapSlots(int slotA, int slotB)
    {
        ValidateSlotIndex(slotA);
        ValidateSlotIndex(slotB);
        (_slots[slotA], _slots[slotB]) = (_slots[slotB], _slots[slotA]);
    }

    /// <summary>
    /// Splits up to <paramref name="amount"/> items out of <paramref name="slotIndex"/>, updating the slot with the remainder.
    /// </summary>
    /// <param name="slotIndex">Target slot index.</param>
    /// <param name="amount">Maximum items to extract.</param>
    /// <returns>Extracted <see cref="ItemStack"/>.</returns>
    public ItemStack SplitSlot(int slotIndex, int amount)
    {
        ValidateSlotIndex(slotIndex);
        var (taken, remaining) = _slots[slotIndex].Split(amount);
        _slots[slotIndex] = remaining;
        return taken;
    }

    /// <summary>
    /// Inserts <paramref name="incoming"/> into the 36 main slots (merging partial stacks first, then filling empty slots)
    /// and returns any leftover <see cref="ItemStack"/> that did not fit.
    /// </summary>
    /// <param name="incoming">Items to insert.</param>
    /// <param name="items">Item registry providing <see cref="ItemDefinition.MaxStackSize"/>.</param>
    /// <returns>Unplaced remainder (<see cref="ItemStack.Empty"/> if fully inserted).</returns>
    public ItemStack AddItem(ItemStack incoming, IRegistry<ItemDefinition> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (incoming.IsEmpty)
        {
            return ItemStack.Empty;
        }

        var maxStack = items.TryGetById(incoming.ItemId, out var def)
            ? Math.Max(1, def.MaxStackSize)
            : 64;

        var remaining = incoming.Count;

        // Pass 1: Merge into existing compatible partial stacks in main slots [0..35].
        if (maxStack > 1)
        {
            for (var i = 0; i < MainSlotCount && remaining > 0; i++)
            {
                var existing = _slots[i];
                if (existing.CanMergeWith(incoming) && existing.Count < maxStack)
                {
                    var space = maxStack - existing.Count;
                    var transfer = Math.Min(space, remaining);
                    _slots[i] = existing.WithCount(existing.Count + transfer);
                    remaining -= transfer;
                }
            }
        }

        // Pass 2: Place into empty main slots [0..35].
        for (var i = 0; i < MainSlotCount && remaining > 0; i++)
        {
            if (_slots[i].IsEmpty)
            {
                var transfer = Math.Min(maxStack, remaining);
                _slots[i] = new ItemStack(incoming.ItemId, transfer);
                remaining -= transfer;
            }
        }

        return remaining > 0 ? new ItemStack(incoming.ItemId, remaining) : ItemStack.Empty;
    }

    /// <summary>
    /// Removes up to <paramref name="requestedCount"/> items of <paramref name="itemId"/> across all 41 slots.
    /// </summary>
    /// <param name="itemId">Target item ID.</param>
    /// <param name="requestedCount">Quantity to remove.</param>
    /// <returns>Total items actually removed.</returns>
    public int RemoveItem(ushort itemId, int requestedCount)
    {
        if (itemId == 0 || requestedCount <= 0)
        {
            return 0;
        }

        var removed = 0;
        for (var i = 0; i < TotalSlotCount && removed < requestedCount; i++)
        {
            var current = _slots[i];
            if (!current.IsEmpty && current.ItemId == itemId)
            {
                var take = Math.Min(current.Count, requestedCount - removed);
                _slots[i] = current.WithCount(current.Count - take);
                removed += take;
            }
        }

        return removed;
    }

    /// <summary>
    /// Computes a deterministic 64-bit FNV-1a hash of all 41 slots and selected hotbar index.
    /// </summary>
    /// <returns>64-bit state hash.</returns>
    public ulong ComputeDeterministicHash()
    {
        var hasher = Fnv1a64.Create();
        hasher.AppendInt32(_selectedHotbarSlot);
        for (var i = 0; i < TotalSlotCount; i++)
        {
            hasher.AppendInt32(_slots[i].ItemId);
            hasher.AppendInt32(_slots[i].Count);
        }

        return hasher.Value;
    }

    private static void ValidateSlotIndex(int slotIndex)
    {
        if ((uint)slotIndex >= TotalSlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), $"Slot index {slotIndex} must be in [0, {TotalSlotCount - 1}].");
        }
    }
}
