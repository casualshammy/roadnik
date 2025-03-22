using Roadnik.Common.ReqResTypes;

namespace Roadnik.Common.ReqRes;

public record ListRoomPointsRes(
  IReadOnlyList<RoomPoint> Result);
