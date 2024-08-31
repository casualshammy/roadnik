namespace Roadnik.Server.Data;

internal record StorageEntry(
  string Username,
  float Latitude,
  float Longitude,
  float Altitude,
  float? Speed = null,
  float? Accuracy = null,
  float? Battery = null,
  float? GsmSignal = null,
  float? Bearing = null);
