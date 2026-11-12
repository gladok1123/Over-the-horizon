namespace OverHorizon.Rendering.Hud;

/// <summary>
/// Describes a clickable button or editable text input box rendered by the 2D menu overlay.
/// </summary>
/// <param name="ActionId">Logical identifier dispatched when clicked.</param>
/// <param name="Label">Text rendered inside the control.</param>
/// <param name="X">Left screen coordinate in pixels.</param>
/// <param name="Y">Top screen coordinate in pixels.</param>
/// <param name="Width">Control width in pixels.</param>
/// <param name="Height">Control height in pixels.</param>
/// <param name="IsHovered">Whether the mouse cursor currently hovers over this control.</param>
/// <param name="IsInputField">Whether this control renders as an inset text-entry field.</param>
/// <param name="IsFocused">Whether this text-entry field currently owns keyboard focus.</param>
public readonly record struct UiButtonItem(
    string ActionId,
    string Label,
    float X,
    float Y,
    float Width,
    float Height,
    bool IsHovered = false,
    bool IsInputField = false,
    bool IsFocused = false);
