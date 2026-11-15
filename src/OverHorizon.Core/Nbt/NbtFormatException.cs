namespace OverHorizon.Core.Nbt;

/// <summary>
/// Exception thrown when binary NBT data is truncated, corrupted, or exceeds safety limits.
/// </summary>
public sealed class NbtFormatException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="NbtFormatException"/> class.</summary>
    public NbtFormatException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NbtFormatException"/> class with a message.</summary>
    /// <param name="message">Error description.</param>
    public NbtFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NbtFormatException"/> class with an inner exception.</summary>
    /// <param name="message">Error description.</param>
    /// <param name="innerException">Underlying cause.</param>
    public NbtFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
