namespace Roadnik.Common.ReqRes;

public record ListRoomPointsResData(
  long PointId,
  string Username,
  double Lat,
  double Lng,
  string Description);
