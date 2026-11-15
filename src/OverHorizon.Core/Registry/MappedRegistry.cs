using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace OverHorizon.Core.Registry;

/// <summary>
/// Thread-safe (once frozen), AOT-compatible registry mapping <see cref="ResourceKey"/> to dense integer IDs.
/// </summary>
/// <typeparam name="T">Registered entry type.</typeparam>
public sealed class MappedRegistry<T> : IRegistry<T>
    where T : notnull
{
    private readonly List<T> _mutableEntries = [];
    private readonly List<ResourceKey> _mutableKeys = [];
    private readonly Dictionary<ResourceKey, int> _mutableLookup = [];

    private T[] _frozenEntries = [];
    private ResourceKey[] _frozenKeys = [];
    private FrozenDictionary<ResourceKey, int>? _frozenLookup;

    /// <inheritdoc />
    public int Count => IsFrozen ? _frozenEntries.Length : _mutableEntries.Count;

    /// <inheritdoc />
    public bool IsFrozen => _frozenLookup is not null;

    /// <inheritdoc />
    public IReadOnlyList<T> Entries => IsFrozen ? _frozenEntries : _mutableEntries;

    /// <summary>
    /// Registers a new entry prior to freezing and returns its assigned zero-based raw ID.
    /// </summary>
    /// <param name="key">Unique resource identifier.</param>
    /// <param name="value">Entry value.</param>
    /// <returns>Assigned zero-based raw ID.</returns>
    public int Register(ResourceKey key, T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (IsFrozen)
        {
            throw new InvalidOperationException($"Cannot register '{key}' because the registry is already frozen.");
        }

        if (_mutableLookup.ContainsKey(key))
        {
            throw new InvalidOperationException($"Duplicate registration for resource key '{key}'.");
        }

        var id = _mutableEntries.Count;
        _mutableEntries.Add(value);
        _mutableKeys.Add(key);
        _mutableLookup.Add(key, id);
        return id;
    }

    /// <summary>
    /// Seals the registry into dense arrays and a <see cref="FrozenDictionary{TKey, TValue}"/> for lock-free concurrent reads.
    /// </summary>
    public void Freeze()
    {
        if (IsFrozen)
        {
            return;
        }

        _frozenEntries = _mutableEntries.ToArray();
        _frozenKeys = _mutableKeys.ToArray();
        _frozenLookup = _mutableLookup.ToFrozenDictionary();
    }

    /// <summary>
    /// Returns a zero-allocation <see cref="ReadOnlySpan{T}"/> over the frozen entries.
    /// </summary>
    /// <returns>Contiguous span of entries.</returns>
    public ReadOnlySpan<T> AsSpan()
    {
        if (!IsFrozen)
        {
            throw new InvalidOperationException("Registry must be frozen before accessing AsSpan().");
        }

        return _frozenEntries;
    }

    /// <inheritdoc />
    public T GetById(int rawId)
    {
        if (!TryGetById(rawId, out var value))
        {
            throw new ArgumentOutOfRangeException(nameof(rawId), $"Registry ID {rawId} is out of range [0, {Count}).");
        }

        return value;
    }

    /// <inheritdoc />
    public bool TryGetById(int rawId, [MaybeNullWhen(false)] out T value)
    {
        if (IsFrozen)
        {
            var entries = _frozenEntries;
            if ((uint)rawId < (uint)entries.Length)
            {
                value = entries[rawId];
                return true;
            }
        }
        else if ((uint)rawId < (uint)_mutableEntries.Count)
        {
            value = _mutableEntries[rawId];
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc />
    public T GetRequired(ResourceKey key)
    {
        if (!TryGet(key, out var value))
        {
            throw new KeyNotFoundException($"Resource key '{key}' is not registered.");
        }

        return value;
    }

    /// <inheritdoc />
    public bool TryGet(ResourceKey key, [MaybeNullWhen(false)] out T value)
    {
        var id = GetId(key);
        if (id >= 0)
        {
            value = IsFrozen ? _frozenEntries[id] : _mutableEntries[id];
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc />
    public int GetId(ResourceKey key)
    {
        if (_frozenLookup is { } frozen)
        {
            return frozen.TryGetValue(key, out var id) ? id : -1;
        }

        return _mutableLookup.TryGetValue(key, out var mutableId) ? mutableId : -1;
    }

    /// <inheritdoc />
    public ResourceKey GetKey(int rawId)
    {
        if (IsFrozen)
        {
            if ((uint)rawId < (uint)_frozenKeys.Length)
            {
                return _frozenKeys[rawId];
            }
        }
        else if ((uint)rawId < (uint)_mutableKeys.Count)
        {
            return _mutableKeys[rawId];
        }

        throw new ArgumentOutOfRangeException(nameof(rawId), $"Registry ID {rawId} is out of range [0, {Count}).");
    }
}
