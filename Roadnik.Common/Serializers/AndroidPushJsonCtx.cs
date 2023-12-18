using Roadnik.Common.ReqRes.PushMessages;
using System.Text.Json.Serialization;

namespace Roadnik.Common.Serializers;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(PushMsg))]
[JsonSerializable(typeof(PushMsgNewTrackStarted))]
[JsonSerializable(typeof(PushMsgNotification))]
[JsonSerializable(typeof(PushMsgRoomPointAdded))]
public partial class AndroidPushJsonCtx : JsonSerializerContext { }