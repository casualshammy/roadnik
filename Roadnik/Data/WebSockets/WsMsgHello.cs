namespace Roadnik.Server.Data.WebSockets;

internal readonly record struct WsMsgHello(
  long UnixTimeMs, 
  uint MaxPathPointsPerRoom,
  long RoomOldestTimestampUnixMs);
