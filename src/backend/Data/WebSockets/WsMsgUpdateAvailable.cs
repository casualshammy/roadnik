using Ax.Fw.Web.Data.SseServer;

namespace Roadnik.Server.Data.WebSockets;

internal record WsMsgUpdateAvailable(long UnixTimeMs)
  : SseAbstractMsg("ws-msg-data-updated");
