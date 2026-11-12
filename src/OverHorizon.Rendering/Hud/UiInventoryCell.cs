using System.Numerics;

namespace OverHorizon.Rendering.Hud;

/// <summary>
/// Describes a single clickable block cell in the Creative Block Palette (<c>E</c> inventory screen).
/// </summary>
/// <param name="BlockIndex">Zero-based index in the Creative Palette.</param>
/// <param name="DisplayName">Human-readable block display name.</param>
/// <param name="Color">Representative RGB block swatch color.</param>
/// <param name="X">Left screen coordinate in pixels.</param>
/// <param name="Y">Top screen coordinate in pixels.</param>
/// <param name="Size">Square cell width and height in pixels.</param>
/// <param name="IsHovered">Whether the mouse cursor currently hovers over this cell.</param>
public readonly record struct UiInventoryCell(
    int BlockIndex,
    string DisplayName,
    Vector3 Color,
    float X,
    float Y,
    float Size,
    bool IsHovered);
