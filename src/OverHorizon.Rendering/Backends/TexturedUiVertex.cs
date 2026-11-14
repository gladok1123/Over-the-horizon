using System.Numerics;
using System.Runtime.InteropServices;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Interleaved 2D pixel-space vertex with font-atlas UV coordinates and RGBA tint color (32 bytes).
/// </summary>
/// <param name="Position">Pixel-space screen position.</param>
/// <param name="Uv">Normalized font-atlas UV coordinates.</param>
/// <param name="Color">Linear RGBA vertex color multiplier.</param>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct TexturedUiVertex(Vector2 Position, Vector2 Uv, Vector4 Color);
