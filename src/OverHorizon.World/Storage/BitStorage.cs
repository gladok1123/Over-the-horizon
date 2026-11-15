namespace OverHorizon.World.Storage;

/// <summary>
/// Dense fixed-width integer array packed into 64-bit words without straddling word boundaries.
/// </summary>
public sealed class BitStorage
{
    private readonly ulong[] _data;
    private readonly int _bitsPerEntry;
    private readonly ulong _maxEntryValue;
    private readonly int _valuesPerLong;

    /// <summary>
    /// Initializes a new zero-filled instance of the <see cref="BitStorage"/> class.
    /// </summary>
    /// <param name="bitsPerEntry">Number of bits per element in <c>[1, 32]</c>.</param>
    /// <param name="size">Logical element count.</param>
    public BitStorage(int bitsPerEntry, int size)
    {
        if (bitsPerEntry is < 1 or > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerEntry), "Bits per entry must be in [1, 32].");
        }

        if (size < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Size must be non-negative.");
        }

        _bitsPerEntry = bitsPerEntry;
        Size = size;
        _maxEntryValue = bitsPerEntry == 32 ? uint.MaxValue : (1UL << bitsPerEntry) - 1UL;
        _valuesPerLong = 64 / bitsPerEntry;

        var wordCount = size == 0 ? 0 : ((size + _valuesPerLong) - 1) / _valuesPerLong;
        _data = wordCount == 0 ? [] : new ulong[wordCount];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BitStorage"/> class from existing packed 64-bit words.
    /// </summary>
    /// <param name="bitsPerEntry">Number of bits per element in <c>[1, 32]</c>.</param>
    /// <param name="size">Logical element count.</param>
    /// <param name="rawWords">Packed 64-bit backing words (copied).</param>
    public BitStorage(int bitsPerEntry, int size, ReadOnlySpan<ulong> rawWords)
        : this(bitsPerEntry, size)
    {
        if (rawWords.Length != _data.Length)
        {
            throw new ArgumentException(
                $"Expected {_data.Length} backing ulong word(s) for size={size}, bits={bitsPerEntry}, got {rawWords.Length}.",
                nameof(rawWords));
        }

        rawWords.CopyTo(_data);
    }

    /// <summary>Gets the logical number of elements stored.</summary>
    public int Size { get; }

    /// <summary>Gets the bit width of each packed element.</summary>
    public int BitsPerEntry => _bitsPerEntry;

    /// <summary>Gets a read-only span over the packed 64-bit backing words.</summary>
    public ReadOnlySpan<ulong> RawWords => _data;

    /// <summary>
    /// Reads the integer value at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">Element index in <c>[0, Size)</c>.</param>
    /// <returns>Unpacked non-negative integer value.</returns>
    public int Get(int index)
    {
        if ((uint)index >= (uint)Size)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var wordIndex = index / _valuesPerLong;
        var bitOffset = (index - (wordIndex * _valuesPerLong)) * _bitsPerEntry;
        return (int)((_data[wordIndex] >> bitOffset) & _maxEntryValue);
    }

    /// <summary>
    /// Writes <paramref name="value"/> at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">Element index in <c>[0, Size)</c>.</param>
    /// <param name="value">Non-negative value fitting within <see cref="BitsPerEntry"/> bits.</param>
    public void Set(int index, int value)
    {
        _ = GetAndSet(index, value);
    }

    /// <summary>
    /// Atomically replaces the value at <paramref name="index"/> in local storage and returns the previous value.
    /// </summary>
    /// <param name="index">Element index in <c>[0, Size)</c>.</param>
    /// <param name="value">New value fitting within <see cref="BitsPerEntry"/> bits.</param>
    /// <returns>Previous unpacked value.</returns>
    public int GetAndSet(int index, int value)
    {
        if ((uint)index >= (uint)Size)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var uval = (ulong)(uint)value;
        if (value < 0 || uval > _maxEntryValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"Value {value} exceeds {_bitsPerEntry}-bit range.");
        }

        var wordIndex = index / _valuesPerLong;
        var bitOffset = (index - (wordIndex * _valuesPerLong)) * _bitsPerEntry;
        var word = _data[wordIndex];
        var previous = (int)((word >> bitOffset) & _maxEntryValue);

        _data[wordIndex] = (word & ~(_maxEntryValue << bitOffset)) | (uval << bitOffset);
        return previous;
    }

    /// <summary>
    /// Creates an independent deep copy of this <see cref="BitStorage"/>.
    /// </summary>
    /// <returns>Cloned <see cref="BitStorage"/>.</returns>
    public BitStorage Clone() => new(_bitsPerEntry, Size, _data);
}
