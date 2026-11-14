namespace OverHorizon.Gameplay.Ecs.Components;

/// <summary>
/// Marks an entity as driven by validated network movement packets from a connected player session
/// rather than autonomous server-side Phase 1 kinematic integration.
/// </summary>
/// <param name="SessionId">Owning network session identifier.</param>
public readonly record struct PlayerControlledComponent(long SessionId);
