namespace OverHorizon.Gameplay.Ecs.Components;

/// <summary>
/// Health points component for damageable entities.
/// </summary>
/// <param name="Current">Current health points.</param>
/// <param name="Max">Maximum health capacity.</param>
public record struct HealthComponent(float Current, float Max);
