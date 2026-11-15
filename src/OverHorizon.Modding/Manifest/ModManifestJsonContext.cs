using System.Text.Json;
using System.Text.Json.Serialization;

namespace OverHorizon.Modding.Manifest;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(ModManifest))]
internal sealed partial class ModManifestJsonContext : JsonSerializerContext
{
}
