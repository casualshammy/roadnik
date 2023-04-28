#if ANDROID
using Android.Content;
using Android.Locations;
using Android.OS;
using Android.Runtime;
using Org.Apache.Commons.Logging;
#endif
using Ax.Fw.Attributes;
using JustLogger.Interfaces;
using System.Collections.Immutable;
using System.Reactive.Subjects;

namespace Roadnik.MAUI.Modules.LocationProvider;

public interface ILocationProvider
{
  IObservable<Microsoft.Maui.Devices.Sensors.Location> Location { get; }

  void ChangeConstrains(TimeSpan _minTime, float _minDistanceMeters);
  void Disable();
  void Enable();
}

#if ANDROID
[ExportClass(typeof(ILocationProvider), Singleton: true)]
public class AndroidLocationProviderImpl : Java.Lang.Object, ILocationListener, ILocationProvider
{
  private readonly IReadOnlyList<string> p_allProviders;
  private readonly LocationManager p_locationManager;
  private readonly ReplaySubject<Microsoft.Maui.Devices.Sensors.Location> p_locationFlow = new(1);
  private readonly ILogger p_logger;
  private ImmutableHashSet<string> p_activeProviders;
  private string? p_activeProvider;
  private Android.Locations.Location? p_lastLocation;
  private TimeSpan p_minTimePeriod = TimeSpan.FromSeconds(1);
  private float p_minDistanceMeters = 0;
  private volatile bool p_enabled;

  public AndroidLocationProviderImpl(ILogger _logger)
  {
    p_logger = _logger["location-provider"];
    p_locationManager = (LocationManager)Platform.AppContext.GetSystemService(Context.LocationService)!;

    var providers = p_locationManager.GetProviders(false);
    p_allProviders = providers.ToArray();
    p_activeProviders = providers
      .Where(_ => p_locationManager.IsProviderEnabled(_))
      .ToImmutableHashSet();
  }

  public IObservable<Microsoft.Maui.Devices.Sensors.Location> Location => p_locationFlow;

  public void Enable()
  {
    if (p_enabled)
      return;

    p_enabled = true;
    MainThread.BeginInvokeOnMainThread(() =>
    {
      foreach (var provider in p_allProviders)
        p_locationManager.RequestLocationUpdates(provider, (long)p_minTimePeriod.TotalMilliseconds, p_minDistanceMeters, this);
    });
  }

  public void Disable()
  {
    if (!p_enabled)
      return;

    p_enabled = false;
    try
    {
      MainThread.BeginInvokeOnMainThread(() => p_locationManager.RemoveUpdates(this));
    }
    catch (Exception ex)
    {
      p_logger.Error($"Can't remove updates!", ex);
    }
  }

  public void ChangeConstrains(TimeSpan _minTime, float _minDistanceMeters)
  {
    p_minTimePeriod = _minTime;
    p_minDistanceMeters = _minDistanceMeters;

    if (p_enabled)
    {
      Disable();
      Enable();
    }
  }

  public void OnLocationChanged(Android.Locations.Location _location)
  {
    if (_location.Provider == null)
    {
      _location.Dispose();
      return;
    }

    if (_location.Provider != p_activeProvider)
    {
      if (p_activeProvider != null && p_locationManager.IsProviderEnabled(p_activeProvider))
      {
        var providerAccuracy = GetProviderAccuracy(_location.Provider);
        if (providerAccuracy == null)
        {
          _location.Dispose();
          return;
        }

        var elapsed = DateTimeOffset.FromUnixTimeMilliseconds(_location.Time) - DateTimeOffset.FromUnixTimeMilliseconds(p_lastLocation?.Time ?? 0);
        var oldAccuracy = GetProviderAccuracy(p_activeProvider);

        if (oldAccuracy != null && providerAccuracy > oldAccuracy && elapsed < p_minTimePeriod * 2)
        {
          _location.Dispose();
          return;
        }
      }

      p_activeProvider = _location.Provider;
      p_logger.Info($"Using provider: {p_activeProvider}");
    }

    var previous = Interlocked.Exchange(ref p_lastLocation, _location);
    previous?.Dispose();

    var location = new Microsoft.Maui.Devices.Sensors.Location(_location.Latitude, _location.Longitude, _location.Altitude)
    {
      Accuracy = _location.HasAccuracy ? _location.Accuracy : null,
      Course = _location.HasBearing ? _location.Bearing : null,
      Speed = _location.HasSpeed ? _location.Speed : null,
      Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(_location.Time),
      VerticalAccuracy = _location.HasVerticalAccuracy ? _location.VerticalAccuracyMeters : null
    };

    p_locationFlow.OnNext(location);
  }

  public void OnStatusChanged(string? _provider, [GeneratedEnum] Availability _status, Bundle? _extras)
  {

  }

  public void OnProviderDisabled(string _provider)
  {
    if (_provider == LocationManager.PassiveProvider)
      return;

    p_activeProviders = p_activeProviders.Remove(_provider);
  }

  public void OnProviderEnabled(string _provider)
  {
    if (_provider == LocationManager.PassiveProvider)
      return;

    p_activeProviders = p_activeProviders.Add(_provider);
  }

  private int? GetProviderAccuracy(string _provider)
  {
    if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
    {
#pragma warning disable CA1416 // Validate platform compatibility
      using var provider = p_locationManager.GetProviderProperties(_provider);
      if (provider == null)
        return null;

      return provider.Accuracy;
#pragma warning restore CA1416 // Validate platform compatibility
    }
    else
    {
#pragma warning disable CS0618 // Type or member is obsolete
      using var provider = p_locationManager.GetProvider(_provider);
      if (provider == null)
        return null;

      return 3 - (int)provider.Accuracy;
#pragma warning restore CS0618 // Type or member is obsolete
    }
  }

}
#endif