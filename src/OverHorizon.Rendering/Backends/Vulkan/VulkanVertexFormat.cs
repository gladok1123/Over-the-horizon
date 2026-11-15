using Silk.NET.Vulkan;

namespace OverHorizon.Rendering.Backends.Vulkan;

/// <summary>
/// One vertex input layout description: the buffer binding plus every attribute the vertex shader consumes.
/// </summary>
/// <param name="Bindings">Vertex buffer binding descriptions (always a single binding at index 0).</param>
/// <param name="Attributes">Attribute descriptions matching the shader <c>location</c> qualifiers.</param>
internal readonly record struct VulkanVertexFormat(
    VertexInputBindingDescription[] Bindings,
    VertexInputAttributeDescription[] Attributes);
