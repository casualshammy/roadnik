using Roadnik.MAUI.Data;

namespace Roadnik.MAUI.Toolkit;

internal class KalmanFilter
{
  private const int MIN_ACCURACY = 1;
  private readonly float p_qMetresPerSecond;
  private double p_variance = -1;
  private long p_timestampMs;
  private double p_lat;
  private double p_lng;

  public KalmanFilter(float _qMetresPerSecond)
  {
    p_qMetresPerSecond = _qMetresPerSecond;
  }

  public LatLng CalculateNext(
    double _lat,
    double _lng,
    double _accuracy,
    long _timestampMs)
  {
    if (_accuracy < MIN_ACCURACY)
      _accuracy = MIN_ACCURACY;

    if (p_variance < 0)
    {
      p_timestampMs = _timestampMs;
      p_lat = _lat;
      p_lng = _lng;
      p_variance = _accuracy * _accuracy;
    }
    else
    {
      var deltaMs = _timestampMs - p_timestampMs;
      if (deltaMs > 0)
      {
        p_variance += _timestampMs / 1000f * p_qMetresPerSecond * p_qMetresPerSecond;
        p_timestampMs = _timestampMs;
      }

      var k = p_variance / (p_variance + _accuracy * _accuracy);
      p_lat += k * (_lat - p_lat);
      p_lng += k * (_lng - p_lng);
      p_variance = (1 - k) * p_variance;
    }
    return new LatLng(p_lat, p_lng);
  }

}
