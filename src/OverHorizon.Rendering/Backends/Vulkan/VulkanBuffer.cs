using Silk.NET.Vulkan;

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
    private Buffer _buffer;
    private DeviceMemory _memory;
    private bool _disposed;

    private VulkanBuffer(Vk vk, Device device, Buffer buffer, DeviceMemory memory, ulong size)
    {
        _vk = vk;
        _device = device;
        _buffer = buffer;
        _memory = memory;
        Size = size;
    }

    /// <summary>Gets the capacity of the buffer in bytes.</summary>
    public ulong Size { get; }

    /// <summary>Gets the raw buffer handle.</summary>
    public Buffer Handle => _buffer;

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
        return new VulkanBuffer(vk, device, buffer, memory, size);
    }

    /// <summary>
    /// Allocates a device-local buffer and fills it with <paramref name="data"/> through a staging buffer.
    /// </summary>
    /// <typeparam name="T">Unmanaged element type.</typeparam>
    /// <param name="vk">Vulkan API.</param>
    /// <param name="device">Logical device.</param>
    /// <param name="physicalDevice">Physical device used for memory-type selection.</param>
    /// <param name="commandPool">Command pool used for the staging copy.</param>
    /// <param name="queue">Queue used for the staging copy.</param>
    /// <param name="data">Source data.</param>
    /// <param name="usage">Extra usage flags for the destination buffer.</param>
    /// <returns>The device-local buffer holding a copy of <paramref name="data"/>.</returns>
    public static VulkanBuffer CreateDeviceLocal<T>(
        Vk vk,
        Device device,
        PhysicalDevice physicalDevice,
        CommandPool commandPool,
        Queue queue,
        ReadOnlySpan<T> data,
        BufferUsageFlags usage)
        where T : unmanaged
    {
        var size = (ulong)(data.Length * sizeof(T));
        if (size == 0)
        {
            size = (ulong)sizeof(T);
        }

        using var staging = Create(
            vk,
            device,
            physicalDevice,
            size,
            BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

        staging.Upload(data);

        var destination = Create(
            vk,
            device,
            physicalDevice,
            size,
            BufferUsageFlags.TransferDstBit | usage,
            MemoryPropertyFlags.DeviceLocalBit);

        VulkanMemory.CopyBuffer(vk, device, commandPool, queue, staging.Handle, destination.Handle, size);
        return destination;
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
        VulkanResult.Ensure(_vk.MapMemory(_device, _memory, 0, byteCount, 0, &mapped), "vkMapMemory");

        fixed (T* source = data)
        {
            Buffer.MemoryCopy(source, mapped, byteCount, byteCount);
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
        VulkanResult.Ensure(_vk.MapMemory(_device, _memory, 0, (nuint)Size, 0, &mapped), "vkMapMemory");
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
        _vk.FreeMemory(_device, _memory, null);
        _buffer = default;
        _memory = default;
    }
}
