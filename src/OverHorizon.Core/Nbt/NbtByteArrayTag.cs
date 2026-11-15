using System.Collections.Immutable;

namespace OverHorizon.Core.Nbt;

/// <summary>Immutable byte-array NBT tag.</summary>
public sealed class NbtByteArrayTag : NbtTag
{
    /// <summary>Initializes a new instance of the <see cref="NbtByteArrayTag"/> class.</summary>
    /// <param name="values">Source bytes copied into immutable storage.</param>
    public NbtByteArrayTag(ReadOnlySpan<byte> values) => Values = [.. values];

    /// <summary>Initializes a new instance of the <see cref="NbtByteArrayTag"/> class from an immutable array.</summary>
    /// <param name="values">Immutable byte sequence.</param>
    public NbtByteArrayTag(ImmutableArray<byte> values) => Values = values.IsDefault ? [] : values;

    /// <summary>Gets the immutable byte payload.</summary>
    public ImmutableArray<byte> Values { get; }

    /// <inheritdoc />
    public override NbtTagType TagType => NbtTagType.ByteArrayTag;
}
