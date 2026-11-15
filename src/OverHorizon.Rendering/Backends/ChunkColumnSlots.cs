using System.Numerics;
using OverHorizon.Core.Math;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Groups all vertical section slots of a single <see cref="ChunkPos"/> column for any render backend,
/// enabling <c>O(1)</c> chunk eviction and single-test column-level distance and frustum culling.
/// </summary>
/// <typeparam name="TSection">Backend-specific uploaded section type.</typeparam>
internal sealed class ChunkColumnSlots<TSection> : IDisposable
    where TSection : class, IResidentSection, IDisposable
{
    /// <summary>Lowest section Y coordinate that can be stored.</summary>
    public const int MinSectionY = -4;

    /// <summary>Number of vertical slots covering the whole world height.</summary>
    public const int SectionSlotCount = 24;

    private readonly TSection?[] _slots = new TSection?[SectionSlotCount];

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkColumnSlots{TSection}"/> class.
    /// </summary>
    /// <param name="chunkPosition">Chunk coordinate this column covers.</param>
    public ChunkColumnSlots(ChunkPos chunkPosition)
    {
        ChunkPosition = chunkPosition;
        RecomputeBounds();
    }

    /// <summary>Gets the chunk coordinate this column covers.</summary>
    public ChunkPos ChunkPosition { get; }

    /// <summary>Gets the bit mask of occupied slots (one bit per section Y, starting at <see cref="MinSectionY"/>).</summary>
    public uint PresentMask { get; private set; }

    /// <summary>Gets the world-space bounds spanning every occupied slot of this column.</summary>
    public BoundingBox ActiveWorldBounds { get; private set; }

    /// <summary>Gets the number of resident sections in this column.</summary>
    public int Count => BitOperations.PopCount(PresentMask);

    /// <summary>Gets a value indicating whether this column holds no sections at all.</summary>
    public bool IsEmpty => PresentMask == 0;

    /// <summary>
    /// Gets the section stored in one vertical slot.
    /// </summary>
    /// <param name="slotIndex">Zero-based slot index (<c>0</c> corresponds to section Y <see cref="MinSectionY"/>).</param>
    /// <returns>The resident section, or <see langword="null"/> when the slot is empty.</returns>
    public TSection? GetSlot(int slotIndex) => _slots[slotIndex];

    /// <summary>Replaces or inserts <paramref name="section"/> and returns the delta in resident section count.</summary>
    /// <param name="section">Section to store.</param>
    /// <returns><c>1</c> when a new slot was occupied, otherwise <c>0</c>.</returns>
    public int UpsertSection(TSection section)
    {
        ArgumentNullException.ThrowIfNull(section);

        var slot = Math.Clamp(section.SectionPosition.Y - MinSectionY, 0, SectionSlotCount - 1);
        var added = 0;
        if (_slots[slot] is { } existing)
        {
            existing.Dispose();
        }
        else
        {
            PresentMask |= 1U << slot;
            added = 1;
        }

        _slots[slot] = section;
        RecomputeBounds();
        return added;
    }

    /// <summary>Removes one section slot if present.</summary>
    /// <param name="sectionY">Section Y coordinate to remove.</param>
    /// <returns><c>1</c> when a section was removed, otherwise <c>0</c>.</returns>
    public int RemoveSection(int sectionY)
    {
        var slot = sectionY - MinSectionY;
        if ((uint)slot >= SectionSlotCount || _slots[slot] is not { } existing)
        {
            return 0;
        }

        existing.Dispose();
        _slots[slot] = null;
        PresentMask &= ~(1U << slot);
        if (PresentMask != 0)
        {
            RecomputeBounds();
        }

        return 1;
    }

    /// <summary>Releases every resident section in this column.</summary>
    public void Dispose()
    {
        var mask = PresentMask;
        while (mask != 0)
        {
            var slot = BitOperations.TrailingZeroCount(mask);
            mask &= mask - 1;
            _slots[slot]?.Dispose();
            _slots[slot] = null;
        }

        PresentMask = 0;
    }

    private void RecomputeBounds()
    {
        var minX = ChunkPosition.X * 16.0;
        var minZ = ChunkPosition.Z * 16.0;

        if (PresentMask == 0)
        {
            ActiveWorldBounds = new BoundingBox(minX, -64.0, minZ, minX + 16.0, 320.0, minZ + 16.0);
            return;
        }

        var minSlot = BitOperations.TrailingZeroCount(PresentMask);
        var maxSlot = 31 - BitOperations.LeadingZeroCount(PresentMask);
        var minY = (minSlot + MinSectionY) * 16.0;
        var maxY = (maxSlot + MinSectionY + 1) * 16.0;
        ActiveWorldBounds = new BoundingBox(minX, minY, minZ, minX + 16.0, maxY, minZ + 16.0);
    }
}
