using Roadnik.Server.Data.Settings;
using System.Text.Json.Serialization;

namespace Roadnik.Server.JsonCtx;

[JsonSerializable(typeof(RawAppSettings))]
internal partial class SettingsJsonCtx : JsonSerializerContext
{

}
