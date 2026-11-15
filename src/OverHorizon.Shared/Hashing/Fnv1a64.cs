using System.Buffers.Binary;

namespace OverHorizon.Shared.Hashing;

/// <summary>
/// Cross-platform, process-independent deterministic 64-bit FNV-1a hash accumulator.
/// Used for world-state and ECS snapshot determinism verification.
/// </summary>
public struct Fnv1a64
{
    /// <summary>
    /// Standard 64-bit FNV offset basis.
    /// </summary>
    public const ulong OffsetBasis = 14695981039346656037UL;

    /// <summary>
    /// Standard 64-bit FNV prime.
    /// </summary>
    public const ulong Prime = 1099511628211UL;

    private ulong _hash;
    private bool _initialized;

    /// <summary>
    /// Gets the current 64-bit hash value.
    /// </summary>
    public readonly ulong Value => _initialized ? _hash : OffsetBasis;

    /// <summary>
    /// Creates a new initialized <see cref="Fnv1a64"/> accumulator.
    /// </summary>
    /// <returns>Initialized <see cref="Fnv1a64"/> instance.</returns>
    public static Fnv1a64 Create() => new()
    {
        _hash = OffsetBasis,
        _initialized = true,
    };

    /// <summary>
    /// Appends a single byte to the hash.
    /// </summary>
    /// <param name="data">Byte to append.</param>
    public void AppendByte(byte data)
    {
        EnsureInitialized();
        _hash ^= data;
        _hash = unchecked(_hash * Prime);
    }

    /// <summary>
    /// Appends a span of bytes to the hash.
    /// </summary>
    /// <param name="bytes">Bytes to append.</param>
    public void AppendBytes(ReadOnlySpan<byte> bytes)
    {
        EnsureInitialized();
        var h = _hash;
        for (var i = 0; i < bytes.Length; i++)
        {
            h ^= bytes[i];
            h = unchecked(h * Prime);
        }

        _hash = h;
    }

    /// <summary>
    /// Appends a 32-bit signed integer in little-endian byte order.
    /// </summary>
    /// <param name="value">Integer value.</param>
    public void AppendInt32(int value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        AppendBytes(buffer);
    }

    /// <summary>
    /// Appends a 64-bit signed integer in little-endian byte order.
    /// </summary>
    /// <param name="value">64-bit integer value.</param>
    public void AppendInt64(long value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
        AppendBytes(buffer);
    }

    /// <summary>
    /// Appends a canonicalized IEEE-754 double-precision float (normalizing -0.0 and NaN).
    /// </summary>
    /// <param name="value">Floating-point value.</param>
    public void AppendDouble(double value)
    {
        long bits;
        if (double.IsNaN(value))
        {
            bits = 0x7FF8000000000000L;
        }
        else
        {
            var rawBits = BitConverter.DoubleToInt64Bits(value);
            bits = (rawBits & 0x7FFFFFFFFFFFFFFFL) == 0L ? 0L : rawBits;
        }

        AppendInt64(bits);
    }

    /// <summary>
    /// Appends UTF-16 code units of an ordinal string deterministically.
    /// </summary>
    /// <param name="text">Text to append.</param>
    public void AppendString(ReadOnlySpan<char> text)
    {
        AppendInt32(text.Length);
        Span<byte> pair = stackalloc byte[sizeof(ushort)];
        for (var i = 0; i < text.Length; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(pair, text[i]);
            AppendBytes(pair);
        }
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            _hash = OffsetBasis;
            _initialized = true;
        }
    }
}
