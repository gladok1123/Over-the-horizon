using System.Diagnostics.CodeAnalysis;

namespace OverHorizon.Core.Registry;

/// <summary>
/// Read-only view over a deterministic, integer-indexed game content registry.
/// </summary>
/// <typeparam name="T">Registered entry type.</typeparam>
public interface IRegistry<T>
    where T : notnull
{
    /// <summary>
    /// Gets the number of registered entries.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets a value indicating whether registration has been sealed for thread-safe concurrent reads.
    /// </summary>
    bool IsFrozen { get; }

    /// <summary>
    /// Gets all entries in deterministic registration order.
    /// </summary>
    IReadOnlyList<T> Entries { get; }

    /// <summary>
    /// Retrieves an entry by its dense raw integer ID.
    /// </summary>
    /// <param name="rawId">Zero-based raw ID.</param>
    /// <returns>Registered entry.</returns>
    T GetById(int rawId);

    /// <summary>
    /// Attempts to retrieve an entry by its dense raw integer ID.
    /// </summary>
    /// <param name="rawId">Zero-based raw ID.</param>
    /// <param name="value">Resolved entry when found.</param>
    /// <returns><see langword="true"/> if <paramref name="rawId"/> is within bounds.</returns>
    bool TryGetById(int rawId, [MaybeNullWhen(false)] out T value);

    /// <summary>
    /// Retrieves an entry by its <see cref="ResourceKey"/>, throwing if not registered.
    /// </summary>
    /// <param name="key">Resource key.</param>
    /// <returns>Registered entry.</returns>
    T GetRequired(ResourceKey key);

    /// <summary>
    /// Attempts to retrieve an entry by its <see cref="ResourceKey"/>.
    /// </summary>
    /// <param name="key">Resource key.</param>
    /// <param name="value">Resolved entry when found.</param>
    /// <returns><see langword="true"/> if found.</returns>
    bool TryGet(ResourceKey key, [MaybeNullWhen(false)] out T value);

    /// <summary>
    /// Returns the dense raw integer ID assigned to <paramref name="key"/>, or <c>-1</c> if absent.
    /// </summary>
    /// <param name="key">Resource key.</param>
    /// <returns>Zero-based raw ID, or <c>-1</c>.</returns>
    int GetId(ResourceKey key);

    /// <summary>
    /// Returns the <see cref="ResourceKey"/> assigned to <paramref name="rawId"/>.
    /// </summary>
    /// <param name="rawId">Zero-based raw ID.</param>
    /// <returns>Associated <see cref="ResourceKey"/>.</returns>
    ResourceKey GetKey(int rawId);
}
