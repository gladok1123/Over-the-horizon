using System.Numerics;
using System.Runtime.InteropServices;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// 28-byte position + RGBA vertex used by <see cref="SilkOpenGlOverlayRenderer"/> for 3D player avatars,
/// targeted block highlights, and 2D screen-space HUD elements.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal readonly record struct OverlayVertex(Vector3 Position, Vector4 Color);
