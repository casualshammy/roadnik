using Roadnik.Server.Data.Settings;
using System.Text.Json.Serialization;

namespace AxToolsServerNet.Data.Serializers;

[JsonSerializable(typeof(RawAppSettings))]
internal partial class SettingsJsonCtx : JsonSerializerContext
{

}
