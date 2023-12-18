using Ax.Fw.SharedTypes.Attributes;

namespace Roadnik.Data;

[SimpleDocument("geo-point")]
internal record GeoPointEntry(
  string RoomId,
  string Username,
  double Lat,
  double Lng,
  string Description);
