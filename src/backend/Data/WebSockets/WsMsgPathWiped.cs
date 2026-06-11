using Ax.Fw.Web.Data.SseServer;

namespace Roadnik.Server.Data.WebSockets;

internal record WsMsgPathWiped(
  string AppId,
  string UserName)
  : SseAbstractMsg("ws-msg-path-wiped");