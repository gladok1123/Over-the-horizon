using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace OverHorizon.Rendering.Backends.Vulkan;

/// <summary>
/// Batched staging-ring uploader. Chunk streaming hands the renderer dozens of meshes per second, and every
/// <c>vkQueueWaitIdle</c> stalls the whole GPU pipeline, so copies are accumulated into one persistently mapped
/// staging buffer and one command buffer and are submitted exactly once per frame by <see cref="Flush"/>.
/// </summary>
internal sealed unsafe class VulkanUploadQueue : IDisposable
{
    private const ulong MinStagingCapacity = 8UL * 1024 * 1024;

    private readonly Vk _vk;
    private readonly Device _device;
    private readonly PhysicalDevice _physicalDevice;
    private readonly CommandPool _commandPool;
    private readonly Queue _queue;

    private VulkanBuffer _staging;
    private byte* _mapped;
    private ulong _offset;
    private CommandBuffer _commandBuffer;
    private bool _recording;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="VulkanUploadQueue"/> class.</summary>
    /// <param name="device">Active Vulkan device.</param>
    public VulkanUploadQueue(VulkanDevice device)
    {
        _vk = device.Api;
        _device = device.LogicalDevice;
        _physicalDevice = device.PhysicalDevice;
        _commandPool = device.Pool;
        _queue = device.GraphicsQueue;
        _staging = CreateStaging(MinStagingCapacity);
    }

    /// <summary>Gets a value indicating whether copies are pending and <see cref="Flush"/> must submit them.</summary>
    public bool HasPendingCopies => _recording;

    /// <summary>Gets the Vulkan API bindings.</summary>
    public Vk Api => _vk;

    /// <summary>Gets the logical device.</summary>
    public Device Device => _device;

    /// <summary>
    /// Copies <paramref name="data"/> into <paramref name="destination"/>. The copy is only recorded; call
    /// <see cref="Flush"/> before the buffer is read by the GPU.
    /// </summary>
    /// <typeparam name="T">Unmanaged element type.</typeparam>
    /// <param name="data">Source data.</param>
    /// <param name="destination">Device-local destination buffer.</param>
    public void CopyTo<T>(ReadOnlySpan<T> data, VulkanBuffer destination)
        where T : unmanaged
    {
        var byteCount = (ulong)(data.Length * sizeof(T));
        if (byteCount == 0)
        {
            return;
        }

        if (_offset + byteCount > _staging.Size)
        {
            Flush();
        }

        if (byteCount > _staging.Size)
        {
            GrowStaging(byteCount);
        }

        fixed (T* source = data)
        {
            System.Buffer.MemoryCopy(source, _mapped + _offset, byteCount, byteCount);
        }

        EnsureRecording();
        var region = new BufferCopy(_offset, 0, byteCount);
        _vk.CmdCopyBuffer(_commandBuffer, _staging.Handle, destination.Handle, 1, &region);
        _offset += byteCount;
    }

    /// <summary>Submits every recorded copy in a single queue submission and waits for it once.</summary>
    public void Flush()
    {
        if (!_recording)
        {
            _offset = 0;
            return;
        }

        _recording = false;
        _offset = 0;

        var commandBuffer = _commandBuffer;
        _commandBuffer = default;

        VulkanResult.Ensure(_vk.EndCommandBuffer(commandBuffer), "vkEndCommandBuffer");

        var submitInfo = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer,
        };

        VulkanResult.Ensure(_vk.QueueSubmit(_queue, 1, in submitInfo, default), "vkQueueSubmit");
        VulkanResult.Ensure(_vk.QueueWaitIdle(_queue), "vkQueueWaitIdle");
        _vk.FreeCommandBuffers(_device, _commandPool, 1, &commandBuffer);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Flush();
        _staging.Dispose();
    }

    private void EnsureRecording()
    {
        if (_recording)
        {
            return;
        }

        var allocateInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1,
        };

        CommandBuffer commandBuffer;
        VulkanResult.Ensure(
            _vk.AllocateCommandBuffers(_device, in allocateInfo, &commandBuffer),
            "vkAllocateCommandBuffers");

        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };

        VulkanResult.Ensure(_vk.BeginCommandBuffer(commandBuffer, in beginInfo), "vkBeginCommandBuffer");
        _commandBuffer = commandBuffer;
        _recording = true;
    }

    private void GrowStaging(ulong requiredBytes)
    {
        _staging.Dispose();
        _staging = CreateStaging(Math.Max(requiredBytes, _staging.Size * 2));
    }

    private VulkanBuffer CreateStaging(ulong capacity)
    {
        var buffer = VulkanBuffer.Create(
            _vk,
            _device,
            _physicalDevice,
            capacity,
            BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

        _mapped = (byte*)buffer.Map();
        _offset = 0;
        return buffer;
    }
}
