using System.Collections.Immutable;

namespace OverHorizon.Core.Nbt;

/// <summary>Immutable 32-bit integer array NBT tag.</summary>
public sealed class NbtIntArrayTag : NbtTag
{
    /// <summary>Initializes a new instance of the <see cref="NbtIntArrayTag"/> class.</summary>
    /// <param name="values">Source integers copied into immutable storage.</param>
    public NbtIntArrayTag(ReadOnlySpan<int> values) => Values = [.. values];

    /// <summary>Initializes a new instance of the <see cref="NbtIntArrayTag"/> class from an immutable array.</summary>
    /// <param name="values">Immutable integer sequence.</param>
    public NbtIntArrayTag(ImmutableArray<int> values) => Values = values.IsDefault ? [] : values;

    /// <summary>Gets the immutable integer payload.</summary>
    public ImmutableArray<int> Values { get; }

    /// <inheritdoc />
    public override NbtTagType TagType => NbtTagType.IntArrayTag;
}
