using Ax.Fw.Web.Data.SseServer;

namespace Roadnik.Server.Data.SseServer;

internal sealed record SseMsgPathTruncated(
  string AppId, 
  string UserName,
  uint PathPoints)
  : SseAbstractMsg("ws-msg-path-truncated");