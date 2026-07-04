using Ax.Fw.Web.Data.SseServer;

namespace Roadnik.Server.Data.SseServer;

internal sealed record SseMsgRoomPointsUpdated(
  long UnixTimeMs)
  : SseAbstractMsg("ws-msg-room-points-updated");