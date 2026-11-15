using System.Collections.Immutable;

namespace OverHorizon.Core.Nbt;

/// <summary>Immutable 64-bit integer array NBT tag.</summary>
public sealed class NbtLongArrayTag : NbtTag
{
    /// <summary>Initializes a new instance of the <see cref="NbtLongArrayTag"/> class.</summary>
    /// <param name="values">Source 64-bit integers copied into immutable storage.</param>
    public NbtLongArrayTag(ReadOnlySpan<long> values) => Values = [.. values];

    /// <summary>Initializes a new instance of the <see cref="NbtLongArrayTag"/> class from an immutable array.</summary>
    /// <param name="values">Immutable 64-bit integer sequence.</param>
    public NbtLongArrayTag(ImmutableArray<long> values) => Values = values.IsDefault ? [] : values;

    /// <summary>Gets the immutable 64-bit integer payload.</summary>
    public ImmutableArray<long> Values { get; }

    /// <inheritdoc />
    public override NbtTagType TagType => NbtTagType.LongArrayTag;
}
