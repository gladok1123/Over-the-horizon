using Silk.NET.Vulkan;

namespace OverHorizon.Rendering.Backends.Vulkan;

/// <summary>
/// Sub-allocator for device-local chunk buffers.
/// <para>
/// Every chunk section used to own four separate <c>vkAllocateMemory</c> calls, so a normal view distance pushed
/// the renderer past the <c>maxMemoryAllocationCount</c> limit most drivers expose (4096) and turned chunk
/// streaming into allocation thrashing. The arena hands out aligned ranges inside a handful of large blocks
/// instead, and the buffers created on top of them are bound with <c>vkBindBufferMemory</c> at an offset.
/// </para>
/// <para>
/// Ranges are recycled with a simple "reset the bump pointer once every range in a block has been freed" policy:
/// all the sections of a chunk column are released together, so blocks empty out and start over instead of
/// fragmenting. Blocks themselves are kept for the lifetime of the arena.
/// </para>
/// </summary>
internal sealed unsafe class VulkanBufferArena : IDisposable
{
    private const ulong BlockSize = 8UL * 1024 * 1024;

    /// <summary>Fallback range alignment, used until a block reports the real binding alignment.</summary>
    private const ulong MinAlignment = 64UL;

    private readonly Vk _vk;
    private readonly Device _device;
    private readonly PhysicalDevice _physicalDevice;
    private readonly MemoryPropertyFlags _properties;
    private readonly List<Block> _blocks = [];
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="VulkanBufferArena"/> class.</summary>
    /// <param name="device">Active Vulkan device.</param>
    /// <param name="properties">Memory properties every allocation must satisfy.</param>
    public VulkanBufferArena(VulkanDevice device, MemoryPropertyFlags properties)
    {
        _vk = device.Api;
        _device = device.LogicalDevice;
        _physicalDevice = device.PhysicalDevice;
        _properties = properties;
    }

    /// <summary>
    /// Reserves <paramref name="size"/> bytes inside one of the arena blocks.
    /// </summary>
    /// <param name="size">Requested size in bytes.</param>
    /// <param name="memory">Receives the backing device memory.</param>
    /// <param name="offset">Receives the offset of the range inside that memory.</param>
    public void Allocate(ulong size, out DeviceMemory memory, out ulong offset)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        foreach (var block in _blocks)
        {
            var aligned = Align(size, block.Alignment);
            if (block.Used + aligned <= block.Size)
            {
                offset = block.Used;
                block.Used += aligned;
                block.Live++;
                memory = block.Memory;
                return;
            }
        }

        var fresh = CreateBlock(Math.Max(BlockSize, size));
        var firstSize = Align(size, fresh.Alignment);
        _blocks.Add(fresh);
        offset = 0;
        fresh.Used = firstSize;
        fresh.Live = 1;
        memory = fresh.Memory;
    }

    /// <summary>
    /// Releases a range previously handed out by <see cref="Allocate"/>.
    /// </summary>
    /// <param name="memory">Backing device memory the range lives in.</param>
    /// <param name="offset">Offset of the range inside that memory.</param>
    /// <param name="size">Size the range was allocated with.</param>
    public void Free(DeviceMemory memory, ulong offset, ulong size)
    {
        if (_disposed)
        {
            return;
        }

        foreach (var block in _blocks)
        {
            if (block.Memory.Handle != memory.Handle)
            {
                continue;
            }

            block.Live--;
            if (block.Live <= 0)
            {
                block.Live = 0;
                block.Used = 0;
            }

            return;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var block in _blocks)
        {
            _vk.FreeMemory(_device, block.Memory, null);
        }

        _blocks.Clear();
    }

    private Block CreateBlock(ulong size)
    {
        // A dummy buffer is used to discover a memory type that supports every buffer usage we need; the real
        // buffers are then bound into the block at an offset.
        using var probe = VulkanBuffer.Create(
            _vk,
            _device,
            _physicalDevice,
            size,
            BufferUsageFlags.TransferDstBit | BufferUsageFlags.VertexBufferBit | BufferUsageFlags.IndexBufferBit,
            _properties);

        var probeRequirements = probe.MemoryRequirements;
        var allocateInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = Math.Max(size, probeRequirements.Size),
            MemoryTypeIndex = VulkanMemory.FindMemoryType(_vk, _physicalDevice, probeRequirements.MemoryTypeBits, _properties),
        };

        DeviceMemory memory;
        VulkanResult.Ensure(_vk.AllocateMemory(_device, in allocateInfo, null, out memory), "vkAllocateMemory");
        return new Block(memory, allocateInfo.AllocationSize, Math.Max(probeRequirements.Alignment, MinAlignment));
    }

    private static ulong Align(ulong size, ulong alignment) => (size + (alignment - 1UL)) & ~(alignment - 1UL);

    private sealed class Block
    {
        public Block(DeviceMemory memory, ulong size, ulong alignment)
        {
            Memory = memory;
            Size = size;
            Alignment = alignment;
        }

        public DeviceMemory Memory { get; }

        public ulong Size { get; }

        public ulong Alignment { get; }

        public ulong Used { get; set; }

        public int Live { get; set; }
    }
}
