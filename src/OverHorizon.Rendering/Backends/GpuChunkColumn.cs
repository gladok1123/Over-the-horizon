using System.Numerics;
using OverHorizon.Core.Math;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Groups all vertical <see cref="GpuSectionBuffer"/> slots for a single <see cref="ChunkPos"/> column,
/// enabling <c>O(1)</c> chunk eviction and single-test column-level distance + frustum culling.
/// </summary>
internal sealed class GpuChunkColumn : IDisposable
{
    public const int MinSectionY = -4;
    public const int SectionSlotCount = 24;

    private readonly GpuSectionBuffer?[] _slots = new GpuSectionBuffer?[SectionSlotCount];

    public GpuChunkColumn(ChunkPos chunkPosition)
    {
        ChunkPosition = chunkPosition;
        RecomputeBounds();
    }

    public ChunkPos ChunkPosition { get; }

    public uint PresentMask { get; private set; }

    public BoundingBox ActiveWorldBounds { get; private set; }

    public int Count => BitOperations.PopCount(PresentMask);

    public bool IsEmpty => PresentMask == 0;

    public GpuSectionBuffer? GetSlot(int slotIndex) => _slots[slotIndex];

    /// <summary>Replaces or inserts <paramref name="buffer"/> and returns the delta in resident section count (<c>0</c> or <c>+1</c>).</summary>
    public int UpsertSection(GpuSectionBuffer buffer)
    {
        var slot = Math.Clamp(buffer.SectionPosition.Y - MinSectionY, 0, SectionSlotCount - 1);
        var bit = 1U << slot;
        var added = 0;
        if (_slots[slot] is { } existing)
        {
            existing.Dispose();
        }
        else
        {
            PresentMask |= bit;
            added = 1;
        }

        _slots[slot] = buffer;
        RecomputeBounds();
        return added;
    }

    /// <summary>Removes a section slot if present and returns <c>1</c> if removed, else <c>0</c>.</summary>
    public int RemoveSection(int sectionY)
    {
        var slot = sectionY - MinSectionY;
        if ((uint)slot >= SectionSlotCount)
        {
            return 0;
        }

        if (_slots[slot] is not { } existing)
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
        if (PresentMask == 0)
        {
            var minX0 = ChunkPosition.X * 16.0;
            var minZ0 = ChunkPosition.Z * 16.0;
            ActiveWorldBounds = new BoundingBox(minX0, -64.0, minZ0, minX0 + 16.0, 320.0, minZ0 + 16.0);
            return;
        }

        var minSlot = BitOperations.TrailingZeroCount(PresentMask);
        var maxSlot = 31 - BitOperations.LeadingZeroCount(PresentMask);
        var minX = ChunkPosition.X * 16.0;
        var minZ = ChunkPosition.Z * 16.0;
        var minY = (minSlot + MinSectionY) * 16.0;
        var maxY = (maxSlot + MinSectionY + 1) * 16.0;
        ActiveWorldBounds = new BoundingBox(minX, minY, minZ, minX + 16.0, maxY, minZ + 16.0);
    }
}
