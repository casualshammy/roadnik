namespace Roadnik.Server.Data.WebSockets;

internal sealed record WsMsgHello(
  long UnixTimeMs,
  uint MaxPathPointsPerRoom,
  IReadOnlyDictionary<string, long> Timestamps);
