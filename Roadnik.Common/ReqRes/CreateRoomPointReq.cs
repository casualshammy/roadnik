namespace Roadnik.Common.ReqRes;

public record CreateRoomPointReq
{
  public required string RoomId { get; init; }
  public required string Username { get; init; }
  public required double Lat { get; init; }
  public required double Lng { get; init; }
  public required string Description { get; init; }
}
