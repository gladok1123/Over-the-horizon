using System.Numerics;

namespace OverHorizon.Rendering.Hud;

/// <summary>
/// Describes a single clickable item/block slot cell in the Creative Catalog or Survival Inventory screen.
/// </summary>
/// <param name="BlockIndex">Catalog or inventory slot index.</param>
/// <param name="DisplayName">Human-readable item or block display name.</param>
/// <param name="Color">Fallback RGB color tint.</param>
/// <param name="X">Left screen coordinate in pixels.</param>
/// <param name="Y">Top screen coordinate in pixels.</param>
/// <param name="Size">Square cell width and height in pixels.</param>
/// <param name="IsHovered">Whether the mouse cursor currently hovers over this cell.</param>
/// <param name="AtlasTileIndex">Optional <c>0..255</c> sprite index in the <c>256x256</c> block/item atlas (<c>-1</c> uses color swatch).</param>
/// <param name="StackCount">Stack quantity badge (<c>&gt; 1</c> renders count in bottom-right).</param>
/// <param name="TooltipDetail">Secondary stats line (e.g. <c>"+7 Attack Damage"</c> or <c>"Restores 8 Hunger"</c>).</param>
/// <param name="IsSelected">Whether this slot is currently selected for swapping in Survival inventory.</param>
public readonly record struct UiInventoryCell(
    int BlockIndex,
    string DisplayName,
    Vector3 Color,
    float X,
    float Y,
    float Size,
    bool IsHovered,
    int AtlasTileIndex = -1,
    int StackCount = 1,
    string TooltipDetail = "",
    bool IsSelected = false);
