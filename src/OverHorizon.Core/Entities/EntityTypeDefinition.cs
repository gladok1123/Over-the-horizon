using OverHorizon.Core.Registry;

namespace OverHorizon.Core.Entities;

/// <summary>
/// Immutable descriptor of an entity archetype's physical bounds and base attributes.
/// </summary>
/// <param name="Key">Unique entity type identifier.</param>
/// <param name="Width">Horizontal collision box width in blocks.</param>
/// <param name="Height">Vertical collision box height in blocks.</param>
/// <param name="MaxHealth">Default maximum health points.</param>
/// <param name="Summonable">Whether commands/spawners may summon this entity.</param>
public sealed record EntityTypeDefinition(
    ResourceKey Key,
    float Width,
    float Height,
    float MaxHealth,
    bool Summonable = true);
