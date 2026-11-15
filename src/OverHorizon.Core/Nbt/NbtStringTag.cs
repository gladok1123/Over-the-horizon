namespace OverHorizon.Core.Nbt;

/// <summary>Immutable UTF-8 string NBT tag (up to 65,535 UTF-8 bytes).</summary>
public sealed class NbtStringTag : NbtTag
{
    /// <summary>Initializes a new instance of the <see cref="NbtStringTag"/> class.</summary>
    /// <param name="value">Non-null string payload.</param>
    public NbtStringTag(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    /// <summary>Gets the string payload.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override NbtTagType TagType => NbtTagType.StringTag;
}
