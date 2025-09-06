namespace Roadnik.Common.ReqRes.PushMessages;

public record PushMsgRoomPointAdded(
  string? AppId,
  string UserName, 
  string Description, 
  double Lat, 
  double Lng);
