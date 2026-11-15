using System.Numerics;
using System.Runtime.InteropServices;

namespace OverHorizon.Rendering.Backends.Vulkan;

/// <summary>
/// The 80-byte push constant block of the overlay programs: a camera-relative transform matrix followed by
/// the fog start/end pair, matching <c>Shaders/overlay.vert</c> and <c>Shaders/overlay.frag</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct VulkanOverlayPushConstants
{
    /// <summary>Size of the push constant block in bytes (a <c>mat4</c> plus a <c>vec4</c>).</summary>
    public const uint SizeInBytes = 80;

    /// <summary>Transform applied to every overlay vertex.</summary>
    public Matrix4x4 Transform;

    /// <summary>Fog start distance, fog end distance, unused, unused.</summary>
    public Vector4 Fog;

    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanOverlayPushConstants"/> struct.
    /// </summary>
    /// <param name="transform">Transform applied to every overlay vertex.</param>
    /// <param name="fogStart">Fog start distance in blocks.</param>
    /// <param name="fogEnd">Fog end distance in blocks.</param>
    public VulkanOverlayPushConstants(Matrix4x4 transform, float fogStart, float fogEnd)
    {
        Transform = transform;
        Fog = new Vector4(fogStart, fogEnd, 0.0f, 0.0f);
    }

    /// <summary>
    /// Serializes the block into <paramref name="destination"/> exactly as the shaders expect it.
    /// </summary>
    /// <param name="destination">Destination span of at least <see cref="SizeInBytes"/> bytes.</param>
    public void WriteTo(Span<byte> destination) => MemoryMarshal.Write(destination, in this);
}
