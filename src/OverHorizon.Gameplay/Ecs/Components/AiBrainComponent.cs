namespace OverHorizon.Gameplay.Ecs.Components;

/// <summary>
/// Parameters governing deterministic autonomous AI wandering behavior.
/// </summary>
/// <param name="WanderSpeed">Horizontal movement speed in blocks per second.</param>
/// <param name="DecisionIntervalTicks">Tick interval between steering decisions (must be &gt;= 1).</param>
public readonly record struct AiBrainComponent(double WanderSpeed, int DecisionIntervalTicks);
