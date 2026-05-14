using Roadnik.MAUI.Data.JsonBridge;
using System.Text.Json.Serialization;

namespace Roadnik.MAUI.JsonCtx;

[JsonSourceGenerationOptions(
  PropertyNameCaseInsensitive = true,
  PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(HostMsgTracksSynchronizedData))]
[JsonSerializable(typeof(HostMsgMapStateData))]
internal partial class JsBridgeJsonCtx : JsonSerializerContext { }
