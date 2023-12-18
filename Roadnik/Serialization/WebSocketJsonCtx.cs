using Roadnik.Server.Data.WebSockets;
using Roadnik.Server.Toolkit;
using System.Text.Json.Serialization;

namespace AxToolsServerNet.Data.Serializers;

[JsonSerializable(typeof(WsBaseMsg))]
[JsonSerializable(typeof(WsMsgHello))]
[JsonSerializable(typeof(WsMsgPathWiped))]
[JsonSerializable(typeof(WsMsgUpdateAvailable))]
[JsonSerializable(typeof(WsMsgRoomPointsUpdated))]
internal partial class WebSocketJsonCtx : JsonSerializerContext
{

}