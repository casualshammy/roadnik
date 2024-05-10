using Ax.Fw.SharedTypes.Attributes;

namespace Roadnik.Data;

[SimpleDocument("room-info")]
public record RoomInfo(
  string RoomId, 
  string Email, 
  uint? MaxPathPoints,
  double? MaxPathPointAgeHours,
  uint? MinPathPointIntervalMs);
