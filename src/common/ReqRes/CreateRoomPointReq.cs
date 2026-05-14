namespace Roadnik.Common.ReqRes;

public record CreateRoomPointReq
{
  public required string? AppId { get; init; }
  public required string RoomId { get; init; }
  public required string Username { get; init; }
  public required double Lat { get; init; }
  public required double Lng { get; init; }
  public required string Description { get; init; }

  public static CreateRoomPointReq From(
    string? _appId,
    string _roomId,
    string _username,
    double _lat,
    double _lng,
    string _description)
  {
    return new()
    {
      AppId = _appId,
      RoomId = _roomId,
      Username = _username,
      Lat = _lat,
      Lng = _lng,
      Description = _description
    };
  }

}
