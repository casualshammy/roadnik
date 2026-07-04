using Ax.Fw.Web.Data.SseServer;

namespace Roadnik.Server.Data.SseServer;

internal sealed record SseMsgHello(
  long UnixTimeMs,
  uint MaxPathPointsPerRoom,
  IReadOnlyDictionary<string, long> Timestamps)
  : SseAbstractMsg("ws-msg-hello");
