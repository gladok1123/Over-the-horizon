using System.Numerics;
using System.Runtime.InteropServices;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// 36-byte position + RGBA + optional atlas UV vertex used by <see cref="SilkOpenGlOverlayRenderer"/>
/// for 3D player/mob avatars, block highlights, and textured 3D held blocks &amp; pixel-extruded items.
/// </summary>
/// <param name="Position">Camera-relative or clip-space 3D vertex coordinate.</param>
/// <param name="Color">Linear/tinted RGBA vertex color.</param>
/// <param name="Uv">Optional <c>256x256</c> block/item atlas UV (<c>&lt; -0.5</c> disables texture sampling).</param>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct OverlayVertex(Vector3 Position, Vector4 Color, Vector2 Uv)
{
    /// <summary>Initializes an untextured solid-colored 3D overlay vertex.</summary>
    /// <param name="position">Vertex coordinate.</param>
    /// <param name="color">RGBA vertex color.</param>
    public OverlayVertex(Vector3 position, Vector4 color)
        : this(position, color, new Vector2(-1.0f, -1.0f))
    {
    }
}
