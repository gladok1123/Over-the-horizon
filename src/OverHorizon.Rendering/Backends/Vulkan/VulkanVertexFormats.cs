using Silk.NET.Vulkan;

namespace OverHorizon.Rendering.Backends.Vulkan;

/// <summary>
/// The three vertex layouts the renderer feeds to Vulkan, kept byte-for-byte identical to the managed vertex
/// structs (<c>ChunkVertex</c>, <c>OverlayVertex</c> and <c>TexturedUiVertex</c>) the meshers emit.
/// </summary>
internal static class VulkanVertexFormats
{
    /// <summary>Builds the 32-byte <c>ChunkVertex</c> layout.</summary>
    /// <returns>The terrain vertex format.</returns>
    public static VulkanVertexFormat Chunk() => new(
        [new VertexInputBindingDescription(0, 32, VertexInputRate.Vertex)],
        [
            new VertexInputAttributeDescription(0, 0, Format.R32G32B32Sfloat, 0),
            new VertexInputAttributeDescription(1, 0, Format.R32G32Sfloat, 12),
            new VertexInputAttributeDescription(2, 0, Format.R32Sfloat, 20),
            new VertexInputAttributeDescription(3, 0, Format.R32Sfloat, 24),
            new VertexInputAttributeDescription(4, 0, Format.R32Sfloat, 28),
        ]);

    /// <summary>Builds the 36-byte <c>OverlayVertex</c> layout.</summary>
    /// <returns>The dynamic overlay vertex format.</returns>
    public static VulkanVertexFormat Overlay() => new(
        [new VertexInputBindingDescription(0, 36, VertexInputRate.Vertex)],
        [
            new VertexInputAttributeDescription(0, 0, Format.R32G32B32Sfloat, 0),
            new VertexInputAttributeDescription(1, 0, Format.R32G32B32A32Sfloat, 12),
            new VertexInputAttributeDescription(2, 0, Format.R32G32Sfloat, 28),
        ]);

    /// <summary>Builds the 32-byte <c>TexturedUiVertex</c> layout.</summary>
    /// <returns>The screen-space HUD vertex format.</returns>
    public static VulkanVertexFormat Ui() => new(
        [new VertexInputBindingDescription(0, 32, VertexInputRate.Vertex)],
        [
            new VertexInputAttributeDescription(0, 0, Format.R32G32Sfloat, 0),
            new VertexInputAttributeDescription(1, 0, Format.R32G32Sfloat, 8),
            new VertexInputAttributeDescription(2, 0, Format.R32G32B32A32Sfloat, 16),
        ]);
}
