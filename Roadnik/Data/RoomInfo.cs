using Ax.Fw.SharedTypes.Attributes;

namespace Roadnik.Data;

[SimpleDocument("room-info")]
public record RoomInfo(
  string RoomId, 
  string Email, 
  int? MaxPoints,
  double? MinPointIntervalMs,
  DateTimeOffset? ValidUntil);
