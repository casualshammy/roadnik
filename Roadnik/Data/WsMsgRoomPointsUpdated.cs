using Roadnik.Attributes;

namespace Roadnik.Data;

[WebSocketMsg("ws-msg-room-points-updated")]
internal record WsMsgRoomPointsUpdated(long UnixTimeMs);