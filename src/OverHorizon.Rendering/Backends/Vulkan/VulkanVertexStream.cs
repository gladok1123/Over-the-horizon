using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace OverHorizon.Rendering.Backends.Vulkan;

/// <summary>
/// A host-visible bump-allocated vertex buffer used for the per-frame dynamic geometry (clouds, mobs, drops,
/// particles, the held viewmodel and the HUD). Every draw writes its vertices at a fresh offset, so a single
/// buffer serves the whole frame and no allocation happens while the player is playing.
/// </summary>
internal sealed unsafe class VulkanVertexStream : IDisposable
{
    private readonly Vk _vk;
    private readonly Device _device;
    private readonly PhysicalDevice _physicalDevice;
    private VulkanBuffer _buffer;
    private byte* _mapped;
    private ulong _offset;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanVertexStream"/> class.
    /// </summary>
    /// <param name="vk">Vulkan API.</param>
    /// <param name="device">Logical device.</param>
    /// <param name="physicalDevice">Physical device used for memory-type selection.</param>
    /// <param name="capacity">Initial capacity in bytes; the stream grows on demand between frames.</param>
    public VulkanVertexStream(Vk vk, Device device, PhysicalDevice physicalDevice, ulong capacity)
    {
        _vk = vk;
        _device = device;
        _physicalDevice = physicalDevice;
        _buffer = CreateBuffer(capacity);
        _mapped = (byte*)_buffer.Map();
    }

    /// <summary>Gets the underlying buffer handle.</summary>
    public Buffer Handle => _buffer.Handle;

    /// <summary>Rewinds the bump allocator. Called once at the start of every frame.</summary>
    public void Reset() => _offset = 0;

    /// <summary>
    /// Ensures the stream can hold at least <paramref name="requiredBytes"/> this frame, growing it if needed.
    /// </summary>
    /// <param name="requiredBytes">Total bytes the frame is about to write.</param>
    public void EnsureCapacity(ulong requiredBytes)
    {
        if (requiredBytes <= _buffer.Size)
        {
            return;
        }

        var grown = Math.Max(requiredBytes, _buffer.Size * 2);
        _buffer.Unmap();
        _buffer.Dispose();
        _buffer = CreateBuffer(grown);
        _mapped = (byte*)_buffer.Map();
        _offset = 0;
    }

    /// <summary>
    /// Copies <paramref name="vertices"/> into the stream and reports where they landed.
    /// </summary>
    /// <typeparam name="T">Unmanaged vertex type.</typeparam>
    /// <param name="vertices">Vertices to upload.</param>
    /// <param name="offset">Receives the byte offset of the first vertex inside the buffer.</param>
    /// <returns><see langword="false"/> when there is nothing to upload.</returns>
    public bool TryWrite<T>(ReadOnlySpan<T> vertices, out ulong offset)
        where T : unmanaged
    {
        var byteCount = (ulong)(vertices.Length * sizeof(T));
        if (byteCount == 0)
        {
            offset = 0;
            return false;
        }

        // Vertex attribute offsets must respect the alignment of the attribute formats (4 bytes for floats).
        _offset = (_offset + 15UL) & ~15UL;
        if (_offset + byteCount > _buffer.Size)
        {
            offset = 0;
            return false;
        }

        fixed (T* source = vertices)
        {
            System.Buffer.MemoryCopy(source, _mapped + _offset, byteCount, byteCount);
        }

        offset = _offset;
        _offset += byteCount;
        return true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _buffer.Unmap();
        _buffer.Dispose();
        _mapped = null;
    }

    private VulkanBuffer CreateBuffer(ulong capacity) => VulkanBuffer.Create(
        _vk,
        _device,
        _physicalDevice,
        capacity,
        BufferUsageFlags.VertexBufferBit,
        MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
}
