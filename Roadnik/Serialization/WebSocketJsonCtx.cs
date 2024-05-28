using Roadnik.Server.Data.WebSockets;
using Roadnik.Server.Toolkit;
using System.Text.Json.Serialization;

namespace Roadnik.Server.JsonCtx;

[JsonSerializable(typeof(WsBaseMsg))]
[JsonSerializable(typeof(WsMsgHello))]
[JsonSerializable(typeof(WsMsgPathWiped))]
[JsonSerializable(typeof(WsMsgUpdateAvailable))]
[JsonSerializable(typeof(WsMsgRoomPointsUpdated))]
[JsonSerializable(typeof(WsMsgPathTruncated))]
internal partial class WebSocketJsonCtx : JsonSerializerContext
{

}