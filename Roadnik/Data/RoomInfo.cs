using Ax.Fw.SharedTypes.Attributes;

namespace Roadnik.Data;

[SimpleDocument("room-info")]
public record RoomInfo(
  string RoomId, 
  string Email, 
  uint? MaxPathPoints,
  uint? MaxPointsPerPath,
  double? MaxPathPointAgeHours,
  uint? MinPathPointIntervalMs);
