namespace Roadnik.Server.Data.WebSockets;

internal record WsMsgPathTruncated(
  string AppId, 
  string UserName,
  uint PathPoints);