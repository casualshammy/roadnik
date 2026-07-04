using Ax.Fw.Web.Data.SseServer;

namespace Roadnik.Server.Data.SseServer;

internal sealed record SseMsgUpdateAvailable(
  long UnixTimeMs)
  : SseAbstractMsg("ws-msg-data-updated");
