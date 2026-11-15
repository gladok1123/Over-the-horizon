using System.Diagnostics.CodeAnalysis;

namespace OverHorizon.Core.Nbt;

/// <summary>
/// Ordered map of named <see cref="NbtTag"/> entries preserving deterministic insertion order.
/// </summary>
public sealed class NbtCompoundTag : NbtTag
{
    private readonly List<KeyValuePair<string, NbtTag>> _entries = [];
    private readonly Dictionary<string, int> _indexByKey = new(StringComparer.Ordinal);

    /// <summary>Gets the number of named entries in this compound tag.</summary>
    public int Count => _entries.Count;

    /// <summary>Gets the ordered sequence of named entries.</summary>
    public IReadOnlyList<KeyValuePair<string, NbtTag>> Entries => _entries;

    /// <inheritdoc />
    public override NbtTagType TagType => NbtTagType.CompoundTag;

    /// <summary>
    /// Adds or replaces a named child tag while preserving existing key position on replacement.
    /// </summary>
    /// <param name="name">Entry name (ordinal).</param>
    /// <param name="tag">Child tag value.</param>
    /// <returns>This <see cref="NbtCompoundTag"/> for fluent construction.</returns>
    public NbtCompoundTag Set(string name, NbtTag tag)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(tag);

        if (_indexByKey.TryGetValue(name, out var existingIndex))
        {
            _entries[existingIndex] = new KeyValuePair<string, NbtTag>(name, tag);
        }
        else
        {
            _indexByKey[name] = _entries.Count;
            _entries.Add(new KeyValuePair<string, NbtTag>(name, tag));
        }

        return this;
    }

    /// <summary>Convenience setter for a 32-bit integer tag.</summary>
    /// <param name="name">Entry name.</param>
    /// <param name="value">Integer value.</param>
    /// <returns>This compound tag.</returns>
    public NbtCompoundTag SetInt32(string name, int value) => Set(name, new NbtIntTag(value));

    /// <summary>Convenience setter for a 64-bit integer tag.</summary>
    /// <param name="name">Entry name.</param>
    /// <param name="value">64-bit value.</param>
    /// <returns>This compound tag.</returns>
    public NbtCompoundTag SetInt64(string name, long value) => Set(name, new NbtLongTag(value));

    /// <summary>Convenience setter for a double-precision tag.</summary>
    /// <param name="name">Entry name.</param>
    /// <param name="value">Double value.</param>
    /// <returns>This compound tag.</returns>
    public NbtCompoundTag SetDouble(string name, double value) => Set(name, new NbtDoubleTag(value));

    /// <summary>Convenience setter for a UTF-8 string tag.</summary>
    /// <param name="name">Entry name.</param>
    /// <param name="value">String value.</param>
    /// <returns>This compound tag.</returns>
    public NbtCompoundTag SetString(string name, string value) => Set(name, new NbtStringTag(value));

    /// <summary>
    /// Attempts to retrieve a strongly-typed child tag by name.
    /// </summary>
    /// <typeparam name="TTag">Expected <see cref="NbtTag"/> subtype.</typeparam>
    /// <param name="name">Entry name.</param>
    /// <param name="tag">Resolved tag when present and matching <typeparamref name="TTag"/>.</param>
    /// <returns><see langword="true"/> if found with matching type.</returns>
    public bool TryGet<TTag>(string name, [MaybeNullWhen(false)] out TTag tag)
        where TTag : NbtTag
    {
        ArgumentNullException.ThrowIfNull(name);

        if (_indexByKey.TryGetValue(name, out var idx) && _entries[idx].Value is TTag typed)
        {
            tag = typed;
            return true;
        }

        tag = null;
        return false;
    }
}
