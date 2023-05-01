namespace Roadnik.MAUI.Toolkit;

internal record LatLng(double Lat, double Lng);

internal class KalmanLocationFilter
{
  private readonly int p_minAccuracy;
  private readonly bool p_autoCalculateQ;
  private float p_qMetresPerSecond;
  private DateTimeOffset p_lastLocationTime;
  private double p_lat;
  private double p_lng;
  private float? p_variance;

  public KalmanLocationFilter(float _qKmPerHour = 10, int _minAccuracy = 1, bool _autoCalculateQ = true)
  {
    p_qMetresPerSecond = _qKmPerHour / 3.6f;
    p_minAccuracy = _minAccuracy;
    p_autoCalculateQ = _autoCalculateQ;
  }

  public void SetQ(float _qKmPerHour) => p_qMetresPerSecond = _qKmPerHour / 3.6f;

  public Location Filter(Location _location, DateTimeOffset _measurementTime)
  {
    if (_location.Accuracy == null || _location.Accuracy < p_minAccuracy)
      _location.Accuracy = p_minAccuracy;

    if (p_variance == null)
    {
      p_lastLocationTime = _measurementTime;
      p_lat = _location.Latitude;
      p_lng = _location.Longitude;
      p_variance = (float)(_location.Accuracy.Value * _location.Accuracy.Value);
    }
    else
    {
      var elapsed = _measurementTime - p_lastLocationTime;
      if (elapsed.TotalMilliseconds > 0)
      {
        p_variance += (float)(elapsed.TotalMilliseconds * p_qMetresPerSecond * p_qMetresPerSecond / 1000);
        p_lastLocationTime = _measurementTime;
      }

      var oldLat = p_lat;
      var oldLng = p_lng;

      var kalman = p_variance.Value / (p_variance.Value + _location.Accuracy.Value * _location.Accuracy.Value);
      p_lat += kalman * (_location.Latitude - p_lat);
      p_lng += kalman * (_location.Longitude - p_lng);
      p_variance = (float)((1 - kalman) * p_variance.Value);

      if (p_autoCalculateQ)
      {
        var oldLocation = new Location(oldLat, oldLng);
        var newLocation = new Location(p_lat, p_lng);
        var distance = oldLocation.CalculateDistance(newLocation, DistanceUnits.Kilometers);
        if (distance > 0 && elapsed.TotalSeconds > 0)
        {
          var speed = distance * 1000 / elapsed.TotalSeconds; // m/s
          p_qMetresPerSecond = (float)speed;
        }
      }
    }

    var result = new Location(p_lat, p_lng)
    {
      Accuracy = _location.Accuracy,
      Altitude = _location.Altitude,
      AltitudeReferenceSystem = _location.AltitudeReferenceSystem,
      Course = _location.Course,
      IsFromMockProvider = _location.IsFromMockProvider,
      ReducedAccuracy = _location.ReducedAccuracy,
      Speed = _location.Speed,
      Timestamp = _location.Timestamp,
      VerticalAccuracy = _location.VerticalAccuracy
    };

    return result;
  }

}
