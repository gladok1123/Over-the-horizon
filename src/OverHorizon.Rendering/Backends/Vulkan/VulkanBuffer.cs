using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace OverHorizon.Rendering.Backends.Vulkan;

/// <summary>
/// A device buffer with its backing memory. Chunk geometry lives in device-local memory filled from a
/// host-visible staging buffer; per-frame overlay vertex streams stay host-visible so they can be
/// re-uploaded every frame without an extra copy.
/// </summary>
internal sealed unsafe class VulkanBuffer : IDisposable
{
    private readonly Vk _vk;
    private readonly Device _device;
    private readonly bool _ownsMemory;
    private Buffer _buffer;
    private DeviceMemory _memory;
    private bool _disposed;

    private VulkanBuffer(Vk vk, Device device, Buffer buffer, DeviceMemory memory, ulong size, bool ownsMemory, MemoryRequirements requirements)
    {
        _vk = vk;
        _device = device;
        _buffer = buffer;
        _memory = memory;
        _ownsMemory = ownsMemory;
        Size = size;
        MemoryRequirements = requirements;
    }

    /// <summary>Gets the capacity of the buffer in bytes.</summary>
    public ulong Size { get; }

    /// <summary>Gets the raw buffer handle.</summary>
    public Buffer Handle => _buffer;

    /// <summary>Gets the memory requirements reported for this buffer.</summary>
    public MemoryRequirements MemoryRequirements { get; }

    /// <summary>Gets the device memory backing this buffer (possibly shared with other buffers).</summary>
    public DeviceMemory Memory => _memory;

    /// <summary>Gets the number of bytes this buffer occupies inside its backing memory.</summary>
    public ulong AllocatedSize => MemoryRequirements.Size;

    /// <summary>
    /// Allocates a buffer bound to freshly allocated memory.
    /// </summary>
    /// <param name="vk">Vulkan API.</param>
    /// <param name="device">Logical device.</param>
    /// <param name="physicalDevice">Physical device used for memory-type selection.</param>
    /// <param name="size">Capacity in bytes.</param>
    /// <param name="usage">Buffer usage flags.</param>
    /// <param name="properties">Required memory properties.</param>
    /// <returns>The allocated buffer.</returns>
    public static VulkanBuffer Create(
        Vk vk,
        Device device,
        PhysicalDevice physicalDevice,
        ulong size,
        BufferUsageFlags usage,
        MemoryPropertyFlags properties)
    {
        var bufferInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
            SharingMode = SharingMode.Exclusive,
        };

        Buffer buffer;
        VulkanResult.Ensure(vk.CreateBuffer(device, in bufferInfo, null, out buffer), "vkCreateBuffer");

        vk.GetBufferMemoryRequirements(device, buffer, out var requirements);
        var allocateInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = VulkanMemory.FindMemoryType(vk, physicalDevice, requirements.MemoryTypeBits, properties),
        };

        DeviceMemory memory;
        VulkanResult.Ensure(vk.AllocateMemory(device, in allocateInfo, null, out memory), "vkAllocateMemory");
        VulkanResult.Ensure(vk.BindBufferMemory(device, buffer, memory, 0), "vkBindBufferMemory");
        return new VulkanBuffer(vk, device, buffer, memory, size, ownsMemory: true, requirements);
    }

    /// <summary>
    /// Allocates a buffer inside a range reserved from <paramref name="arena"/> instead of taking out a fresh
    /// <c>vkAllocateMemory</c> of its own.
    /// </summary>
    /// <param name="vk">Vulkan API.</param>
    /// <param name="device">Logical device.</param>
    /// <param name="arena">Arena the backing memory comes from.</param>
    /// <param name="size">Capacity in bytes.</param>
    /// <param name="usage">Buffer usage flags.</param>
    /// <returns>The allocated buffer.</returns>
    public static VulkanBuffer Create(
        Vk vk,
        Device device,
        VulkanBufferArena arena,
        ulong size,
        BufferUsageFlags usage)
    {
        var bufferInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
            SharingMode = SharingMode.Exclusive,
        };

        Buffer buffer;
        VulkanResult.Ensure(vk.CreateBuffer(device, in bufferInfo, null, out buffer), "vkCreateBuffer");

        vk.GetBufferMemoryRequirements(device, buffer, out var requirements);
        arena.Allocate(requirements.Size, out var memory, out var offset);
        VulkanResult.Ensure(vk.BindBufferMemory(device, buffer, memory, offset), "vkBindBufferMemory");
        return new VulkanBuffer(vk, device, buffer, memory, size, ownsMemory: false, requirements);
    }

    /// <summary>
    /// Copies <paramref name="data"/> into a host-visible buffer.
    /// </summary>
    /// <typeparam name="T">Unmanaged element type.</typeparam>
    /// <param name="data">Source data.</param>
    public void Upload<T>(ReadOnlySpan<T> data)
        where T : unmanaged
    {
        var byteCount = (nuint)(data.Length * sizeof(T));
        if (byteCount == 0 || byteCount > (nuint)Size)
        {
            return;
        }

        void* mapped;
        VulkanResult.Ensure(_vk.MapMemory(_device, _memory, 0, byteCount, MemoryMapFlags.None, &mapped), "vkMapMemory");

        fixed (T* source = data)
        {
            System.Buffer.MemoryCopy(source, mapped, byteCount, byteCount);
        }

        _vk.UnmapMemory(_device, _memory);
    }

    /// <summary>
    /// Maps the whole allocation for CPU writes and returns the mapped pointer.
    /// </summary>
    /// <returns>The host-visible pointer to the buffer contents.</returns>
    public void* Map()
    {
        void* mapped;
        VulkanResult.Ensure(_vk.MapMemory(_device, _memory, 0, (nuint)Size, MemoryMapFlags.None, &mapped), "vkMapMemory");
        return mapped;
    }

    /// <summary>Unmaps a buffer previously mapped with <see cref="Map"/>.</summary>
    public void Unmap() => _vk.UnmapMemory(_device, _memory);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _vk.DestroyBuffer(_device, _buffer, null);
        if (_ownsMemory)
        {
            _vk.FreeMemory(_device, _memory, null);
        }
        _buffer = default;
        _memory = default;
    }
}
