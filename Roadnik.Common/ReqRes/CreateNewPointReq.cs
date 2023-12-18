namespace Roadnik.Common.ReqRes;

public record CreateNewPointReq(
  string RoomId, 
  string Username, 
  double Lat, 
  double Lng,
  string Description);
