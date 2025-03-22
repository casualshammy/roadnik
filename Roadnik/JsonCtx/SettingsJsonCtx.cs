using Roadnik.Server.Data.Settings;
using System.Text.Json.Serialization;

namespace Roadnik.Server.JsonCtx;

[JsonSourceGenerationOptions(
  PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(RawAppSettings))]
internal partial class SettingsJsonCtx : JsonSerializerContext
{

}
