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
    private bool _disposed;

    private VulkanSectionSlot(SectionPos sectionPosition, BoundingBox worldBounds)
    {
        SectionPosition = sectionPosition;
        WorldBounds = worldBounds;
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
    /// <param name="vk">Vulkan API.</param>
    /// <param name="device">Logical device.</param>
    /// <param name="physicalDevice">Physical device used for memory-type selection.</param>
    /// <param name="commandPool">Command pool used for the staging copies.</param>
    /// <param name="queue">Queue used for the staging copies.</param>
    /// <param name="mesh">CPU mesh produced by the greedy mesher.</param>
    /// <returns>The resident GPU section.</returns>
    public static VulkanSectionSlot Create(
        Vk vk,
        Device device,
        PhysicalDevice physicalDevice,
        CommandPool commandPool,
        Queue queue,
        SectionMeshData mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        var slot = new VulkanSectionSlot(mesh.SectionPosition, mesh.WorldBounds);

        if (!mesh.OpaqueIndices.IsEmpty)
        {
            slot.OpaqueVertices = VulkanBuffer.CreateDeviceLocal(
                vk,
                device,
                physicalDevice,
                commandPool,
                queue,
                mesh.OpaqueVertices.Span,
                BufferUsageFlags.VertexBufferBit);

            slot.OpaqueIndices = VulkanBuffer.CreateDeviceLocal(
                vk,
                device,
                physicalDevice,
                commandPool,
                queue,
                mesh.OpaqueIndices.Span,
                BufferUsageFlags.IndexBufferBit);

            slot.OpaqueIndexCount = mesh.OpaqueIndices.Length;
            slot.Track(slot.OpaqueVertices);
            slot.Track(slot.OpaqueIndices);
        }

        if (!mesh.TranslucentIndices.IsEmpty)
        {
            slot.TranslucentVertices = VulkanBuffer.CreateDeviceLocal(
                vk,
                device,
                physicalDevice,
                commandPool,
                queue,
                mesh.TranslucentVertices.Span,
                BufferUsageFlags.VertexBufferBit);

            slot.TranslucentIndices = VulkanBuffer.CreateDeviceLocal(
                vk,
                device,
                physicalDevice,
                commandPool,
                queue,
                mesh.TranslucentIndices.Span,
                BufferUsageFlags.IndexBufferBit);

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
        foreach (var buffer in _buffers)
        {
            buffer.Dispose();
        }

        _buffers.Clear();
        OpaqueVertices = null;
        OpaqueIndices = null;
        TranslucentVertices = null;
        TranslucentIndices = null;
        OpaqueIndexCount = 0;
        TranslucentIndexCount = 0;
    }

    private void Track(VulkanBuffer buffer) => _buffers.Add(buffer);
}
