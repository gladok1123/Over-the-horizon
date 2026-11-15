namespace OverHorizon.Networking.Packets.Clientbound;

/// <summary>
/// Enables ZLib packet framing for subsequent packets when <see cref="ThresholdBytes"/> is non-negative.
/// </summary>
/// <param name="ThresholdBytes">Minimum uncompressed payload size to compress (<c>-1</c> disables).</param>
public sealed record SetCompressionPacket(int ThresholdBytes) : IClientboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Login;
}
