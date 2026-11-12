using OverHorizon.Networking.Codec;

namespace OverHorizon.Networking;

/// <summary>
/// Configuration options for <see cref="NetworkServer"/>.
/// </summary>
/// <param name="ProtocolVersion">Supported wire protocol version.</param>
/// <param name="VersionName">Displayed server version string.</param>
/// <param name="Motd">Server list Message of the Day.</param>
/// <param name="MaxPlayers">Maximum concurrent logged-in players.</param>
/// <param name="CompressionThresholdBytes">ZLib compression threshold in bytes (<c>-1</c> disables compression).</param>
/// <param name="KeepAliveIntervalTicks">Ticks between outgoing <c>KeepAliveRequestPacket</c>s (<c>200</c> = 10s).</param>
/// <param name="KeepAliveTimeoutTicks">Maximum ticks without keep-alive response before disconnect (<c>600</c> = 30s).</param>
public sealed record NetworkServerOptions(
    int ProtocolVersion = JavaProtocolCodec.CurrentProtocolVersion,
    string VersionName = "OverHorizon 0.1-MVP",
    string Motd = "OverHorizon Clean-Room C# Voxel Server",
    int MaxPlayers = 100,
    int CompressionThresholdBytes = 256,
    int KeepAliveIntervalTicks = 200,
    int KeepAliveTimeoutTicks = 600);
