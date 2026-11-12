namespace OverHorizon.World.Storage;

/// <summary>
/// Adaptive bit-packed voxel container supporting single-value, indirect-palette, and direct-ID modes.
/// </summary>
/// <typeparam name="T">Value type stored in each cell.</typeparam>
public sealed class PalettedContainer<T>
    where T : struct, IEquatable<T>
{
    private readonly int _minIndirectBits;
    private readonly int _maxIndirectBits;
    private readonly int _directBits;
    private readonly Func<T, int> _toGlobalId;
    private readonly Func<int, T> _fromGlobalId;

    private int _bitsPerEntry;
    private T _singleValue;
    private List<T>? _palette;
    private Dictionary<T, int>? _paletteIndex;
    private BitStorage? _storage;

    /// <summary>
    /// Initializes a new instance of the <see cref="PalettedContainer{T}"/> class filled with <paramref name="initialValue"/>.
    /// </summary>
    /// <param name="capacity">Total number of cells (e.g. 4096 for blocks, 64 for biomes).</param>
    /// <param name="initialValue">Initial uniform value occupying zero bit-array memory.</param>
    /// <param name="minIndirectBits">Minimum bit width when transitioning to an indirect palette.</param>
    /// <param name="maxIndirectBits">Maximum bit width for indirect palettes before switching to direct IDs.</param>
    /// <param name="directBits">Bit width required to store global registry IDs directly.</param>
    /// <param name="toGlobalId">Maps a value to its dense non-negative global ID.</param>
    /// <param name="fromGlobalId">Resolves a global ID back to <typeparamref name="T"/>.</param>
    public PalettedContainer(
        int capacity,
        T initialValue,
        int minIndirectBits,
        int maxIndirectBits,
        int directBits,
        Func<T, int> toGlobalId,
        Func<int, T> fromGlobalId)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
        }

        if (minIndirectBits < 1 || maxIndirectBits < minIndirectBits || directBits <= maxIndirectBits)
        {
            throw new ArgumentException("Bit thresholds must satisfy 1 <= minIndirectBits <= maxIndirectBits < directBits.");
        }

        ArgumentNullException.ThrowIfNull(toGlobalId);
        ArgumentNullException.ThrowIfNull(fromGlobalId);

        Capacity = capacity;
        _minIndirectBits = minIndirectBits;
        _maxIndirectBits = maxIndirectBits;
        _directBits = directBits;
        _toGlobalId = toGlobalId;
        _fromGlobalId = fromGlobalId;

        _bitsPerEntry = 0;
        _singleValue = initialValue;
    }

    /// <summary>Gets the total cell capacity of this container.</summary>
    public int Capacity { get; }

    /// <summary>Gets the current bit width per entry (<c>0</c> = uniform single-value mode).</summary>
    public int BitsPerEntry => _bitsPerEntry;

    /// <summary>Gets a value indicating whether all cells hold the same single value.</summary>
    public bool IsSingleValue => _bitsPerEntry == 0;

    /// <summary>
    /// Reads the value at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">Cell index in <c>[0, Capacity)</c>.</param>
    /// <returns>Value stored at <paramref name="index"/>.</returns>
    public T Get(int index)
    {
        if ((uint)index >= (uint)Capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (_bitsPerEntry == 0)
        {
            return _singleValue;
        }

        var raw = _storage!.Get(index);
        return _bitsPerEntry == _directBits ? _fromGlobalId(raw) : _palette![raw];
    }

    /// <summary>
    /// Replaces the value at <paramref name="index"/> and returns the previous value.
    /// </summary>
    /// <param name="index">Cell index in <c>[0, Capacity)</c>.</param>
    /// <param name="value">New cell value.</param>
    /// <returns>Previous value at <paramref name="index"/>.</returns>
    public T GetAndSet(int index, T value)
    {
        if ((uint)index >= (uint)Capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (_bitsPerEntry == 0)
        {
            if (_singleValue.Equals(value))
            {
                return _singleValue;
            }

            var previousSingle = _singleValue;
            PromoteFromSingleValue(value, index);
            return previousSingle;
        }

        if (_bitsPerEntry == _directBits)
        {
            var prevGlobal = _storage!.GetAndSet(index, _toGlobalId(value));
            return _fromGlobalId(prevGlobal);
        }

        if (!_paletteIndex!.TryGetValue(value, out var paletteSlot))
        {
            paletteSlot = AddPaletteEntryOrResize(value);
            if (_bitsPerEntry == _directBits)
            {
                var prevGlobal = _storage!.GetAndSet(index, _toGlobalId(value));
                return _fromGlobalId(prevGlobal);
            }
        }

        var previousSlot = _storage!.GetAndSet(index, paletteSlot);
        return _palette![previousSlot];
    }

    /// <summary>
    /// Sets the value at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">Cell index in <c>[0, Capacity)</c>.</param>
    /// <param name="value">New value.</param>
    public void Set(int index, T value)
    {
        _ = GetAndSet(index, value);
    }

    /// <summary>
    /// Creates an independent deep copy suitable for lock-free Copy-on-Write snapshots.
    /// </summary>
    /// <returns>Cloned <see cref="PalettedContainer{T}"/>.</returns>
    public PalettedContainer<T> Clone()
    {
        var copy = new PalettedContainer<T>(
            Capacity,
            _singleValue,
            _minIndirectBits,
            _maxIndirectBits,
            _directBits,
            _toGlobalId,
            _fromGlobalId)
        {
            _bitsPerEntry = _bitsPerEntry,
        };

        if (_storage is not null)
        {
            copy._storage = _storage.Clone();
        }

        if (_palette is not null && _paletteIndex is not null)
        {
            copy._palette = [.. _palette];
            copy._paletteIndex = new Dictionary<T, int>(_paletteIndex);
        }

        return copy;
    }

    private void PromoteFromSingleValue(T secondValue, int secondValueIndex)
    {
        _bitsPerEntry = _minIndirectBits;
        _palette = [_singleValue, secondValue];
        _paletteIndex = new Dictionary<T, int>(1 << _minIndirectBits)
        {
            [_singleValue] = 0,
            [secondValue] = 1,
        };
        _storage = new BitStorage(_minIndirectBits, Capacity);
        _storage.Set(secondValueIndex, 1);
    }

    private int AddPaletteEntryOrResize(T newValue)
    {
        var maxPaletteCapacity = 1 << _bitsPerEntry;
        if (_palette!.Count < maxPaletteCapacity)
        {
            var newSlot = _palette.Count;
            _palette.Add(newValue);
            _paletteIndex![newValue] = newSlot;
            return newSlot;
        }

        var oldStorage = _storage!;
        var oldPalette = _palette;

        if (_bitsPerEntry < _maxIndirectBits)
        {
            _bitsPerEntry++;
            var resizedStorage = new BitStorage(_bitsPerEntry, Capacity);
            for (var i = 0; i < Capacity; i++)
            {
                resizedStorage.Set(i, oldStorage.Get(i));
            }

            _storage = resizedStorage;
            var newSlot = _palette.Count;
            _palette.Add(newValue);
            _paletteIndex![newValue] = newSlot;
            return newSlot;
        }

        // Promote to direct global ID mode.
        _bitsPerEntry = _directBits;
        var directStorage = new BitStorage(_directBits, Capacity);
        for (var i = 0; i < Capacity; i++)
        {
            var entry = oldPalette[oldStorage.Get(i)];
            directStorage.Set(i, _toGlobalId(entry));
        }

        _storage = directStorage;
        _palette = null;
        _paletteIndex = null;
        return -1;
    }
}
