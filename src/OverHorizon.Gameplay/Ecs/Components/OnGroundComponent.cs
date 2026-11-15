namespace OverHorizon.Gameplay.Ecs.Components;

/// <summary>
/// Tracks whether an entity rested on a solid downward surface during its most recent physics step.
/// </summary>
/// <param name="IsOnGround">Whether grounded.</param>
public record struct OnGroundComponent(bool IsOnGround);
