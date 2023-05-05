﻿using Ax.Fw.Storage.Data;

namespace Roadnik.Data;

internal record TimedStorageEntry(
  long UnixTimeMs,
  string Key,
  string Nickname,
  float Latitude,
  float Longitude,
  float Altitude,
  float? Speed = null,
  float? Accuracy = null,
  float? Battery = null,
  float? GsmSignal = null,
  float? Bearing = null,
  string? Message = null)
{
  public static TimedStorageEntry FromStorageEntry(DocumentTypedEntry<StorageEntry> _document)
  {
    var data = _document.Data;
    return new TimedStorageEntry(
      _document.Created.ToUnixTimeMilliseconds(),
      data.Key,
      data.Nickname,
      data.Latitude,
      data.Longitude,
      data.Altitude,
      data.Speed,
      data.Accuracy,
      data.Battery,
      data.GsmSignal,
      data.Bearing,
      data.Message);
  }
}