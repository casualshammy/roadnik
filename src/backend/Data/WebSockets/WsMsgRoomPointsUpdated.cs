using Ax.Fw.Web.Data.SseServer;

namespace Roadnik.Server.Data.WebSockets;

internal record WsMsgRoomPointsUpdated(long UnixTimeMs)
  : SseAbstractMsg("ws-msg-room-points-updated");