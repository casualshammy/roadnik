using Ax.Fw.Storage.Data;
using Roadnik.Common.Data.DocumentStorage;

namespace Roadnik.Common.ReqResTypes;

public record RoomPoint(
  long PointId,
  string Username,
  float Lat,
  float Lng,
  string Description)
{
  public static RoomPoint From(BlobEntry<RoomPointDocument> _doc)
  {
    return new(
      _doc.Created.ToUnixTimeMilliseconds(),
      _doc.Data.Username,
      (float)_doc.Data.Lat,
      (float)_doc.Data.Lng,
      _doc.Data.Description);
  }
}
