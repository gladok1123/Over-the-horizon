using System.Text.Json.Serialization;

namespace OverHorizon.Networking.Status;

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(ServerStatusResponse))]
internal sealed partial class ServerStatusJsonContext : JsonSerializerContext
{
}
