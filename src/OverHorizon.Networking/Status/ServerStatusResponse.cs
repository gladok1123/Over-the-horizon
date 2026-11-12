using System.Text.Json.Serialization;

namespace OverHorizon.Networking.Status;

/// <summary>
/// AOT-safe server list status payload serialized in <c>StatusResponsePacket</c>.
/// </summary>
/// <param name="VersionName">Human-readable engine/protocol label.</param>
/// <param name="ProtocolVersion">Numeric protocol version.</param>
/// <param name="MaxPlayers">Maximum concurrent player slots.</param>
/// <param name="OnlinePlayers">Currently active player sessions.</param>
/// <param name="Description">Server MOTD text.</param>
public sealed record ServerStatusResponse(
    [property: JsonPropertyName("versionName")] string VersionName,
    [property: JsonPropertyName("protocol")] int ProtocolVersion,
    [property: JsonPropertyName("maxPlayers")] int MaxPlayers,
    [property: JsonPropertyName("onlinePlayers")] int OnlinePlayers,
    [property: JsonPropertyName("description")] string Description);
