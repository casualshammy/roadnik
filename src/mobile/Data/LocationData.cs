using System.Runtime.CompilerServices;

namespace Roadnik.MAUI.Data;

internal record LocationData(
  double Latitude,
  double Longitude,
  double Altitude,
  float Accuracy,
  float? VerticalAccuracy,
  float? Course,
  float? Speed,
  DateTimeOffset Timestamp)
{
  private const double DEGREES_TO_RAD = Math.PI / 180.0;
  private const double MEAN_EARTH_RADIUS_METRES = 6371d * 1000;

  public double GetDistanceTo(LocationData _otherLocation)
  {
    return GetDistanceTo(_otherLocation.Latitude, _otherLocation.Longitude);
  }

  public double GetDistanceTo(double _lat, double _lng)
  {
    if (Latitude == _lat && Longitude == _lng)
      return 0d;

    var latStart = Latitude;
    var lngStart = Longitude;
    var latEnd = _lat;
    var lngEnd = _lng;

    var dLat = DegreesToRadians(latEnd - latStart);
    var dLon = DegreesToRadians(lngEnd - lngStart);

    latStart = DegreesToRadians(latStart);
    latEnd = DegreesToRadians(latEnd);

    var dLat2 = Math.Sin(dLat / 2) * Math.Sin(dLat / 2);
    var dLon2 = Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

    var a = dLat2 + dLon2 * Math.Cos(latStart) * Math.Cos(latEnd);
    var c = 2 * Math.Asin(Math.Sqrt(a));

    return MEAN_EARTH_RADIUS_METRES * c;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private static double DegreesToRadians(double _degrees) => _degrees * DEGREES_TO_RAD;

}
