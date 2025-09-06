using Roadnik.Common.ReqRes.PushMessages;
using System.Text.Json.Serialization;

namespace Roadnik.Common.JsonCtx;

[JsonSourceGenerationOptions(
  PropertyNameCaseInsensitive = true, 
  PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, 
  WriteIndented = true)]
[JsonSerializable(typeof(PushMsg))]
[JsonSerializable(typeof(PushMsgNewTrackStarted))]
[JsonSerializable(typeof(PushMsgRoomPointAdded))]
public partial class AndroidPushJsonCtx : JsonSerializerContext { }