using Ax.Fw.Storage.Data;
using Roadnik.Common.Toolkit;

namespace Roadnik.Common.Data;

public record TimedStorageEntry(
  string AppId,
  long UnixTimeMs,
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
  public static TimedStorageEntry FromStorageEntry(DocumentEntry<StorageEntry> _document)
  {
    var data = _document.Data;
    return new TimedStorageEntry(
      GenericToolkit.ConcealAppInstanceId(data.AppId),
      _document.Created.ToUnixTimeMilliseconds(),
      data.Username,
      data.Latitude,
      data.Longitude,
      data.Altitude,
      data.Speed,
      data.Accuracy,
      data.Battery,
      data.GsmSignal,
      data.Bearing,
      data.HR);
  }
}