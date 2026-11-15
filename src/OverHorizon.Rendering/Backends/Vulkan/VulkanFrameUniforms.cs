using System.Numerics;
using System.Runtime.InteropServices;

namespace OverHorizon.Rendering.Backends.Vulkan;

/// <summary>
/// The per-frame uniform block shared by the chunk vertex and fragment stages, laid out to match the
/// <c>FrameUniforms</c> std140 block declared in <c>Shaders/chunk.vert</c> and <c>Shaders/chunk.frag</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct VulkanFrameUniforms
{
    /// <summary>Size of the uniform block in bytes.</summary>
    public const int SizeInBytes = 112;

    /// <summary>Size of the per-section push constant block in bytes (a single <c>vec4</c> offset).</summary>
    public const uint SectionPushSize = 16;

    /// <summary>Camera-relative view-projection matrix.</summary>
    public Matrix4x4 ViewProjection;

    /// <summary>Fog start distance, fog end distance, unused, unused.</summary>
    public Vector4 Fog;

    /// <summary>Sky/fog colour RGB with the sky brightness multiplier in W.</summary>
    public Vector4 Sky;

    /// <summary>Held torch light radius, ambient light floor, underwater flag, unused.</summary>
    public Vector4 Lighting;

    /// <summary>
    /// Serializes the block into <paramref name="destination"/> exactly as the shaders expect it.
    /// </summary>
    /// <param name="destination">Destination span of at least <see cref="SizeInBytes"/> bytes.</param>
    public void WriteTo(Span<byte> destination) => MemoryMarshal.Write(destination, in this);
}
