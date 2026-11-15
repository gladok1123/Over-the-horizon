namespace OverHorizon.Rendering.Hud;

/// <summary>
/// Describes a clickable stone button, draggable settings slider, or editable text input box rendered by the 2D menu overlay.
/// </summary>
public readonly record struct UiButtonItem(
    string ActionId,
    string Label,
    float X,
    float Y,
    float Width,
    float Height,
    bool IsHovered = false,
    bool IsInputField = false,
    bool IsFocused = false,
    bool IsSlider = false,
    float SliderNormalizedValue = 0.0f);
