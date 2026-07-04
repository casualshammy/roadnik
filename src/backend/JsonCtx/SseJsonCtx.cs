using Roadnik.Server.Data.SseServer;
using System.Text.Json.Serialization;

namespace Roadnik.Server.JsonCtx;

[JsonSerializable(typeof(SseMsgHello))]
[JsonSerializable(typeof(SseMsgPathWiped))]
[JsonSerializable(typeof(SseMsgUpdateAvailable))]
[JsonSerializable(typeof(SseMsgRoomPointsUpdated))]
[JsonSerializable(typeof(SseMsgPathTruncated))]
internal partial class SseJsonCtx : JsonSerializerContext
{ }