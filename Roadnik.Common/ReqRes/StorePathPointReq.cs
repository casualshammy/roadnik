namespace Roadnik.Common.ReqRes;

public record StorePathPointReq
{
  public required int SessionId { get; init; }
  public required string RoomId { get; init; }
  public required string Username { get; init; }
  public required float Lat { get; init; }
  public required float Lng { get; init; }
  public required float Alt { get; init; }
  public float? Speed { get; init; }
  public float? Acc { get; init; }
  public float? Battery { get; init; }
  public float? GsmSignal { get; init; }
  public float? Bearing { get; init; }
  public bool? WipeOldPath { get; init; }
}