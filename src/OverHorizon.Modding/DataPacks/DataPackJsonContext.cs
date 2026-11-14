using System.Text.Json;
using System.Text.Json.Serialization;

namespace OverHorizon.Modding.DataPacks;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(DataPackBundle))]
internal sealed partial class DataPackJsonContext : JsonSerializerContext
{
}
