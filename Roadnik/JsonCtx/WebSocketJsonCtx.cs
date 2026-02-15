using Ax.Fw.Web.Data.WsServer;
using Roadnik.Server.Data.WebSockets;
using System.Text.Json.Serialization;

namespace Roadnik.Server.JsonCtx;

[JsonSerializable(typeof(WsBaseMsg))]
[JsonSerializable(typeof(WsMsgHello))]
[JsonSerializable(typeof(WsMsgPathWiped))]
[JsonSerializable(typeof(WsMsgUpdateAvailable))]
[JsonSerializable(typeof(WsMsgRoomPointsUpdated))]
[JsonSerializable(typeof(WsMsgPathTruncated))]
internal partial class WebSocketJsonCtx : JsonSerializerContext
{ }