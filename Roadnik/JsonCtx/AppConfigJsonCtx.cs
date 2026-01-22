using System.Text.Json.Serialization;

namespace Roadnik.Server.JsonCtx;

[JsonSerializable(typeof(IReadOnlyDictionary<string, string>))]
internal partial class AppConfigJsonCtx : JsonSerializerContext { }