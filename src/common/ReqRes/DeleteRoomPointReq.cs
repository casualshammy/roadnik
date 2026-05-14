namespace Roadnik.Common.ReqRes;

public record DeleteRoomPointReq
{
  public required string RoomId { get; init; }
  public required long PointId { get; init; }
}
