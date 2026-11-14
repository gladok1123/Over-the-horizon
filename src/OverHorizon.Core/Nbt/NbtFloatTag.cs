namespace OverHorizon.Core.Nbt;

/// <summary>Immutable 32-bit IEEE-754 floating-point NBT tag.</summary>
public sealed class NbtFloatTag : NbtTag
{
    /// <summary>Initializes a new instance of the <see cref="NbtFloatTag"/> class.</summary>
    /// <param name="value">32-bit float value.</param>
    public NbtFloatTag(float value) => Value = value;

    /// <summary>Gets the 32-bit float value.</summary>
    public float Value { get; }

    /// <inheritdoc />
    public override NbtTagType TagType => NbtTagType.FloatTag;
}
