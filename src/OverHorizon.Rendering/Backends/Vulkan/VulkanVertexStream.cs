using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace OverHorizon.Rendering.Backends.Vulkan;

/// <summary>
/// A host-visible bump-allocated vertex buffer used for the per-frame dynamic geometry (clouds, mobs, drops,
/// particles, the held viewmodel and the HUD). Every draw writes its vertices at a fresh offset, so a single
/// buffer serves the whole frame and no allocation happens while the player is playing. One instance exists
/// per frame in flight (and one per UI frame), so rewinding never disturbs a frame that is still in flight;
/// when a frame outgrows its stream mid-recording the data written so far is copied into a bigger buffer and
/// the old one is retired through <see cref="VulkanDeferredDisposer"/> — draws already recorded this frame
/// still bind it.
/// </summary>
internal sealed unsafe class VulkanVertexStream : IDisposable
{
    private readonly Vk _vk;
    private readonly Device _device;
    private readonly PhysicalDevice _physicalDevice;
    private readonly VulkanDeferredDisposer? _disposer;
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
    /// <param name="capacity">Initial capacity in bytes; the stream grows on demand.</param>
    /// <param name="disposer">Optional retirement list that defers destruction of replaced buffers until in-flight frames stop reading them.</param>
    public VulkanVertexStream(Vk vk, Device device, PhysicalDevice physicalDevice, ulong capacity, VulkanDeferredDisposer? disposer = null)
    {
        _vk = vk;
        _device = device;
        _physicalDevice = physicalDevice;
        _disposer = disposer;
        _buffer = CreateBuffer(capacity);
        _mapped = (byte*)_buffer.Map();
    }

    /// <summary>Gets the underlying buffer handle.</summary>
    public Buffer Handle => _buffer.Handle;

    /// <summary>Gets the bump pointer: the offset just past the last byte written (before alignment rounding).</summary>
    public ulong Offset => _offset;

    /// <summary>Rewinds the bump allocator. Called once at the start of every frame.</summary>
    public void Reset() => _offset = 0;

    /// <summary>
    /// Ensures the stream can hold at least <paramref name="requiredBytes"/>, growing it if needed. Bytes
    /// already written are preserved, so this is safe both at frame start (nothing recorded yet) and when
    /// a frame overflows mid-recording.
    /// </summary>
    /// <param name="requiredBytes">Total bytes the frame is about to write.</param>
    public void EnsureCapacity(ulong requiredBytes)
    {
        if (requiredBytes <= _buffer.Size)
        {
            return;
        }

        Grow(requiredBytes);
    }

    /// <summary>
    /// Copies <paramref name="vertices"/> into the stream and reports where they landed. The stream grows
    /// on demand instead of dropping the batch, so a frame that exceeds its previous high-water mark keeps
    /// every byte of geometry.
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
            Grow(_offset + byteCount);
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

    /// <summary>
    /// Replaces the backing buffer with a bigger one and copies the bytes written so far into it at the
    /// same offsets. The previous buffer keeps the data of draws already recorded against it: those
    /// command buffers have not been submitted yet, so it goes through the deferred disposer (or, with no
    /// disposer, is destroyed immediately — valid only for callers that own no in-flight frame).
    /// </summary>
    /// <param name="requiredBytes">Capacity the stream must reach.</param>
    private void Grow(ulong requiredBytes)
    {
        var grown = Math.Max(requiredBytes, _buffer.Size * 2);
        var previous = _buffer;
        var previousMapped = _mapped;

        _buffer = CreateBuffer(grown);
        _mapped = (byte*)_buffer.Map();
        if (_offset > 0)
        {
            System.Buffer.MemoryCopy(previousMapped, _mapped, grown, _offset);
        }

        previous.Unmap();
        if (_disposer is not null)
        {
            _disposer.Retire(null, [previous]);
        }
        else
        {
            previous.Dispose();
        }
    }

    private VulkanBuffer CreateBuffer(ulong capacity) => VulkanBuffer.Create(
        _vk,
        _device,
        _physicalDevice,
        capacity,
        BufferUsageFlags.VertexBufferBit,
        MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
}
