using System.Text.Json.Serialization;

namespace Roadnik.MAUI.JsonCtx;

[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(Guid))]
internal partial class PrefsStorageJsonCtx : JsonSerializerContext { }
