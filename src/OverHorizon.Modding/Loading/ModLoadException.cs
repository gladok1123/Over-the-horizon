namespace OverHorizon.Modding.Loading;

/// <summary>
/// Thrown when mod discovery, manifest validation, or dependency resolution fails.
/// </summary>
public sealed class ModLoadException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ModLoadException"/> class.</summary>
    public ModLoadException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ModLoadException"/> class with a message.</summary>
    /// <param name="message">Diagnostic error message.</param>
    public ModLoadException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ModLoadException"/> class with a message and inner exception.</summary>
    /// <param name="message">Diagnostic error message.</param>
    /// <param name="innerException">Underlying cause.</param>
    public ModLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
