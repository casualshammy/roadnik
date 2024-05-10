namespace Roadnik.Server.Data.WebSockets;

internal record WsMsgHello(long UnixTimeMs, uint MaxPathPointsPerRoom);
