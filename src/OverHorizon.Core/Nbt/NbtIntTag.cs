namespace OverHorizon.Core.Nbt;

/// <summary>Immutable 32-bit signed integer NBT tag.</summary>
public sealed class NbtIntTag : NbtTag
{
    /// <summary>Initializes a new instance of the <see cref="NbtIntTag"/> class.</summary>
    /// <param name="value">Signed 32-bit value.</param>
    public NbtIntTag(int value) => Value = value;

    /// <summary>Gets the 32-bit signed value.</summary>
    public int Value { get; }

    /// <inheritdoc />
    public override NbtTagType TagType => NbtTagType.IntTag;
}
