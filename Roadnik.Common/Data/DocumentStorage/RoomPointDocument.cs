using Ax.Fw.SharedTypes.Attributes;

namespace Roadnik.Common.Data.DocumentStorage;

[SimpleDocument("geo-point")]
public record RoomPointDocument(
  string RoomId,
  string Username,
  double Lat,
  double Lng,
  string Description);
