using Ax.Fw.Web.Data.SseServer;

namespace Roadnik.Server.Data.SseServer;

internal sealed record SseMsgPathWiped(
  string AppId,
  string UserName)
  : SseAbstractMsg("ws-msg-path-wiped");