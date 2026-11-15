namespace OverHorizon.Gameplay.Ecs.Components;

/// <summary>
/// Physical bounding-box width and height component for an entity.
/// </summary>
/// <param name="Width">Horizontal width in blocks.</param>
/// <param name="Height">Vertical height in blocks.</param>
public readonly record struct BodyDimensionsComponent(double Width, double Height);
