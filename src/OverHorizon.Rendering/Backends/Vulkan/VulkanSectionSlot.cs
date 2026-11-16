using OverHorizon.Core.Math;
using OverHorizon.Rendering.Meshing;
using Silk.NET.Vulkan;

namespace OverHorizon.Rendering.Backends.Vulkan;

/// <summary>
/// One uploaded 16x16x16 chunk section: up to four device-local buffers (opaque and translucent vertices and
/// indices) plus the metadata the shared culling stage needs.
/// </summary>
internal sealed unsafe class VulkanSectionSlot : IResidentSection, IDisposable
{
    private readonly List<VulkanBuffer> _buffers = [];
    private VulkanBufferArena? _arena;
    private VulkanDeferredDisposer? _disposer;
    private bool _disposed;

    private VulkanSectionSlot(SectionPos sectionPosition, BoundingBox worldBounds, VulkanDeferredDisposer? disposer)
    {
        SectionPosition = sectionPosition;
        WorldBounds = worldBounds;
        _disposer = disposer;
    }

    /// <inheritdoc />
    public SectionPos SectionPosition { get; }

    /// <inheritdoc />
    public BoundingBox WorldBounds { get; }

    /// <inheritdoc />
    public int OpaqueIndexCount { get; private set; }

    /// <inheritdoc />
    public int TranslucentIndexCount { get; private set; }

    /// <summary>Gets the opaque vertex buffer, or <see langword="null"/> when the section has no opaque geometry.</summary>
    public VulkanBuffer? OpaqueVertices { get; private set; }

    /// <summary>Gets the opaque index buffer, or <see langword="null"/> when the section has no opaque geometry.</summary>
    public VulkanBuffer? OpaqueIndices { get; private set; }

    /// <summary>Gets the translucent vertex buffer, or <see langword="null"/> when the section is fully opaque.</summary>
    public VulkanBuffer? TranslucentVertices { get; private set; }

    /// <summary>Gets the translucent index buffer, or <see langword="null"/> when the section is fully opaque.</summary>
    public VulkanBuffer? TranslucentIndices { get; private set; }

    /// <summary>
    /// Uploads one meshed section to the GPU.
    /// </summary>
    /// <param name="arena">Device-local arena the section's buffers are sub-allocated from.</param>
    /// <param name="uploads">Batched staging uploader.</param>
    /// <param name="mesh">CPU mesh produced by the greedy mesher.</param>
    /// <param name="disposer">Optional retirement list; when provided, <see cref="Dispose"/> defers the actual buffer destruction until in-flight frames stop reading it.</param>
    /// <returns>The resident GPU section.</returns>
    public static VulkanSectionSlot Create(VulkanBufferArena arena, VulkanUploadQueue uploads, SectionMeshData mesh, VulkanDeferredDisposer? disposer = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        var slot = new VulkanSectionSlot(mesh.SectionPosition, mesh.WorldBounds, disposer);
        slot._arena = arena;

        if (!mesh.OpaqueIndices.IsEmpty)
        {
            slot.OpaqueVertices = Upload(arena, uploads, mesh.OpaqueVertices.Span, BufferUsageFlags.VertexBufferBit);
            slot.OpaqueIndices = Upload(arena, uploads, mesh.OpaqueIndices.Span, BufferUsageFlags.IndexBufferBit);

            slot.OpaqueIndexCount = mesh.OpaqueIndices.Length;
            slot.Track(slot.OpaqueVertices);
            slot.Track(slot.OpaqueIndices);
        }

        if (!mesh.TranslucentIndices.IsEmpty)
        {
            slot.TranslucentVertices = Upload(arena, uploads, mesh.TranslucentVertices.Span, BufferUsageFlags.VertexBufferBit);
            slot.TranslucentIndices = Upload(arena, uploads, mesh.TranslucentIndices.Span, BufferUsageFlags.IndexBufferBit);

            slot.TranslucentIndexCount = mesh.TranslucentIndices.Length;
            slot.Track(slot.TranslucentVertices);
            slot.Track(slot.TranslucentIndices);
        }

        return slot;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Detach the buffers first: frames recorded from now on must never see them again. The GPU may
        // still be reading the memory, so destruction is either handed to the deferred disposer (the
        // renderer's normal path) or, when no disposer exists, performed right away by the caller that
        // guarantees the device is idle.
        var arena = _arena;
        var disposer = _disposer;
        var buffers = _buffers.ToArray();
        _buffers.Clear();
        _arena = null;
        _disposer = null;

        OpaqueVertices = null;
        OpaqueIndices = null;
        TranslucentVertices = null;
        TranslucentIndices = null;
        OpaqueIndexCount = 0;
        TranslucentIndexCount = 0;

        if (disposer is not null)
        {
            disposer.Retire(arena, buffers);
            return;
        }

        DestroyBuffers(arena, buffers);
    }

    private void Track(VulkanBuffer buffer) => _buffers.Add(buffer);

    private static void DestroyBuffers(VulkanBufferArena? arena, VulkanBuffer[] buffers)
    {
        foreach (var buffer in buffers)
        {
            arena?.Free(buffer.Memory, 0, buffer.AllocatedSize);
            buffer.Dispose();
        }
    }

    private static VulkanBuffer Upload<T>(
        VulkanBufferArena arena,
        VulkanUploadQueue uploads,
        ReadOnlySpan<T> data,
        BufferUsageFlags usage)
        where T : unmanaged
    {
        var buffer = VulkanBuffer.Create(
            uploads.Api,
            uploads.Device,
            arena,
            Math.Max((ulong)(data.Length * sizeof(T)), 1UL),
            BufferUsageFlags.TransferDstBit | usage);

        uploads.CopyTo(data, buffer);
        return buffer;
    }
}
