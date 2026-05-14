using Ax.Fw.Storage.Data;
using Roadnik.Common.ReqRes;

namespace Roadnik.Common.Data;

public record StorageEntry(
  Guid AppId,
  string Username,
  float Latitude,
  float Longitude,
  float Altitude,
  float? Speed = null,
  float? Accuracy = null,
  float? Battery = null,
  float? GsmSignal = null,
  float? Bearing = null,
  int? HR = null)
{
  public static StorageEntry From(StorePathPointReq _req)
  {
    return new StorageEntry(
      _req.AppId,
      _req.Username,
      _req.Lat,
      _req.Lng,
      _req.Alt,
      _req.Speed,
      _req.Acc,
      _req.Battery,
      _req.GsmSignal,
      _req.Bearing,
      _req.HR);
  }

  public static Guid GetAppIdFromDocumentKey(BlobEntryMeta _meta)
  {
    var keyParts = _meta.Key.Split('.', 2);
    if (keyParts.Length != 2 || !Guid.TryParse(keyParts[0], out var appId))
      throw new FormatException($"Invalid document key format: {_meta.Key}");

    return appId;
  }
}
