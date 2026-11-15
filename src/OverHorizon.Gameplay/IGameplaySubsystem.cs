using OverHorizon.Core;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay.Crafting;
using OverHorizon.Gameplay.Ecs.Components;

namespace OverHorizon.Gameplay;

/// <summary>
/// Coordinates entity-component-system (ECS) gameplay simulation, parallel AI + physics ticks, crafting recipes, and health.
/// </summary>
public interface IGameplaySubsystem : ITickSubsystem, IDisposable
{
    /// <summary>Gets the current number of active entities in the ECS world.</summary>
    int EntityCount { get; }

    /// <summary>Gets the 64-bit deterministic simulation seed for this gameplay world.</summary>
    long WorldSeed { get; }

    /// <summary>Gets the frozen crafting recipe registry.</summary>
    IRegistry<ICraftingRecipe> Recipes { get; }

    /// <summary>
    /// Spawns a new entity into the ECS world on the main simulation thread and returns its deterministic 64-bit ID.
    /// </summary>
    /// <param name="typeKey">Entity type key.</param>
    /// <param name="position">Initial bottom-center world position.</param>
    /// <param name="velocity">Initial linear velocity in blocks per second.</param>
    /// <param name="maxHealth">Maximum and initial health points.</param>
    /// <param name="aiBrain">Optional autonomous AI wandering configuration.</param>
    /// <param name="width">Horizontal bounding-box width in blocks.</param>
    /// <param name="height">Vertical bounding-box height in blocks.</param>
    /// <param name="controllingSessionId">Optional owning network session ID for player-controlled entities.</param>
    /// <returns>Assigned monotonically increasing 64-bit entity identifier.</returns>
    long SpawnEntity(
        ResourceKey typeKey,
        Vec3d position,
        Vec3d velocity,
        float maxHealth = 20.0f,
        AiBrainComponent? aiBrain = null,
        double width = 0.6,
        double height = 1.8,
        long? controllingSessionId = null);

    /// <summary>
    /// Reads the current world-space feet position of <paramref name="entityId"/>, or <see langword="null"/> if not found.
    /// </summary>
    /// <param name="entityId">Target 64-bit entity ID.</param>
    /// <returns>Current <see cref="Vec3d"/> position, or <see langword="null"/>.</returns>
    Vec3d? TryGetEntityPosition(long entityId);

    /// <summary>
    /// Updates the authoritative kinematic state of <paramref name="entityId"/> on the main simulation thread.
    /// </summary>
    /// <param name="entityId">Target 64-bit entity ID.</param>
    /// <param name="position">New world-space feet position.</param>
    /// <param name="velocity">New linear velocity.</param>
    /// <param name="onGround">Whether the entity is grounded.</param>
    /// <returns><see langword="true"/> if the entity existed and was updated.</returns>
    bool TrySetEntityKinematics(long entityId, Vec3d position, Vec3d velocity, bool onGround);

    /// <summary>
    /// Removes <paramref name="entityId"/> from the ECS world on the main simulation thread.
    /// </summary>
    /// <param name="entityId">Target 64-bit entity ID.</param>
    /// <returns><see langword="true"/> if the entity existed and was destroyed.</returns>
    bool DespawnEntity(long entityId);

    /// <summary>
    /// Applies positive damage to <paramref name="entityId"/> on the main thread, destroying the entity if health reaches zero.
    /// </summary>
    /// <param name="entityId">Target 64-bit entity ID.</param>
    /// <param name="damageAmount">Positive damage points.</param>
    /// <returns><see langword="true"/> if the entity died and was removed; otherwise <see langword="false"/>.</returns>
    bool ApplyDamage(long entityId, float damageAmount);

    /// <summary>
    /// Computes a cross-platform deterministic 64-bit FNV-1a hash of all entities ordered by <c>EntityId</c>.
    /// </summary>
    /// <returns>Deterministic 64-bit state hash.</returns>
    ulong ComputeStateHash();
}
