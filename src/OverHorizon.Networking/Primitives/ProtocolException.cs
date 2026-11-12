namespace OverHorizon.Networking.Primitives;

/// <summary>
/// Thrown when an incoming or outgoing network packet violates wire framing, size bounds, or state-machine rules.
/// </summary>
public sealed class ProtocolException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProtocolException"/> class.
    /// </summary>
    public ProtocolException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtocolException"/> class with a message.
    /// </summary>
    /// <param name="message">Diagnostic error message.</param>
    public ProtocolException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtocolException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">Diagnostic error message.</param>
    /// <param name="innerException">Underlying cause.</param>
    public ProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
