using OverHorizon.Core.Registry;

namespace OverHorizon.Core.Entities;

/// <summary>
/// Clean-room built-in entity type definitions for the OverHorizon MVP.
/// </summary>
public static class BuiltInEntities
{
    /// <summary>Key for <c>overhorizon:player</c>.</summary>
    public static readonly ResourceKey Player = ResourceKey.OverHorizon("player");

    /// <summary>Key for <c>overhorizon:dropped_item</c>.</summary>
    public static readonly ResourceKey DroppedItem = ResourceKey.OverHorizon("dropped_item");

    /// <summary>Key for <c>overhorizon:passive_grazer</c>.</summary>
    public static readonly ResourceKey PassiveGrazer = ResourceKey.OverHorizon("passive_grazer");

    /// <summary>Key for <c>overhorizon:night_stalker</c>.</summary>
    public static readonly ResourceKey NightStalker = ResourceKey.OverHorizon("night_stalker");

    /// <summary>
    /// Registers built-in entity types into an unfrozen <paramref name="registry"/>.
    /// </summary>
    /// <param name="registry">Target unfrozen entity registry.</param>
    public static void PopulateBuiltIns(MappedRegistry<EntityTypeDefinition> registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        _ = registry.Register(Player, new EntityTypeDefinition(Player, Width: 0.6f, Height: 1.8f, MaxHealth: 20.0f, Summonable: false));
        _ = registry.Register(DroppedItem, new EntityTypeDefinition(DroppedItem, Width: 0.25f, Height: 0.25f, MaxHealth: 5.0f));
        _ = registry.Register(PassiveGrazer, new EntityTypeDefinition(PassiveGrazer, Width: 0.9f, Height: 1.3f, MaxHealth: 10.0f));
        _ = registry.Register(NightStalker, new EntityTypeDefinition(NightStalker, Width: 0.6f, Height: 1.95f, MaxHealth: 20.0f));
    }

    /// <summary>
    /// Creates, populates, and freezes the default MVP entity type registry.
    /// </summary>
    /// <returns>Frozen <see cref="MappedRegistry{T}"/> of <see cref="EntityTypeDefinition"/>.</returns>
    public static MappedRegistry<EntityTypeDefinition> CreateFrozen()
    {
        var registry = new MappedRegistry<EntityTypeDefinition>();
        PopulateBuiltIns(registry);
        registry.Freeze();
        return registry;
    }
}
