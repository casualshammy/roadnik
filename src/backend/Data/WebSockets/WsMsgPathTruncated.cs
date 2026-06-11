using Ax.Fw.Web.Data.SseServer;

namespace Roadnik.Server.Data.WebSockets;

internal record WsMsgPathTruncated(
  string AppId, 
  string UserName,
  uint PathPoints)
  : SseAbstractMsg("ws-msg-path-truncated");