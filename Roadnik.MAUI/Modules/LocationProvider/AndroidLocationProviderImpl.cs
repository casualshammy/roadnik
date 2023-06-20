#if ANDROID
using Android.Content;
using Android.Locations;
using Android.OS;
using Android.Runtime;
using Ax.Fw.Attributes;
using Ax.Fw.Extensions;
using JustLogger.Interfaces;
using Roadnik.MAUI.Interfaces;
using System.Collections.Immutable;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Roadnik.MAUI.Modules.LocationProvider;

[ExportClass(typeof(ILocationProvider), Singleton: true)]
public class AndroidLocationProviderImpl : Java.Lang.Object, ILocationListener, ILocationProvider
{
  private readonly LocationManager p_locationManager;
  private readonly ReplaySubject<Microsoft.Maui.Devices.Sensors.Location> p_locationFlow = new(1);
  private readonly ILogger p_logger;
  private ImmutableHashSet<string> p_activeProviders = ImmutableHashSet<string>.Empty;
  private TimeSpan p_minTimePeriod = TimeSpan.FromSeconds(1);
  private float p_minDistanceMeters = 0;
  private long p_enabled = 0;

  public AndroidLocationProviderImpl(ILogger _logger)
  {
    p_logger = _logger["location-provider"];
    p_locationManager = (LocationManager)Platform.AppContext.GetSystemService(Context.LocationService)!;

    Location = p_locationFlow
      .DistinctUntilChanged(_ => HashCode.Combine(_.Latitude, _.Longitude, _.Timestamp));
  }

  public IObservable<Microsoft.Maui.Devices.Sensors.Location> Location { get; }

  public void Enable()
  {
    var oldEnabled = Interlocked.Exchange(ref p_enabled, 1);

    if (oldEnabled == 1)
      return;

    var providers = p_locationManager.GetProviders(false);
    var allProviders = providers.ToArray();
    p_activeProviders = providers
      .Where(_ => p_locationManager.IsProviderEnabled(_))
      .ToImmutableHashSet();

    p_logger.Info($"Starting updates, all providers: <{string.Join(">, <", allProviders)}>");
    p_logger.Info($"Starting updates, active providers: <{string.Join(">, <", p_activeProviders)}>");

    MainThread.BeginInvokeOnMainThread(() =>
    {
      foreach (var provider in allProviders)
        p_locationManager.RequestLocationUpdates(provider, (long)p_minTimePeriod.TotalMilliseconds, p_minDistanceMeters, this);
    });
  }

  public void Disable()
  {
    var oldEnabled = Interlocked.Exchange(ref p_enabled, 0);

    if (oldEnabled == 0)
      return;

    p_logger.Info($"Stopping updates, active providers: <{string.Join(">, <", p_activeProviders)}>");

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

    p_logger.Info($"Constrains were changed; min time: '{_minTime}', min distance: '{_minDistanceMeters}'");

    if (Interlocked.Read(ref p_enabled) == 1)
    {
      Disable();
      Enable();
    }
  }

  public void OnLocationChanged(Android.Locations.Location _location)
  {
    try
    {
      if (_location.Provider == null)
        return;

      if (!_location.HasAccuracy)
        return;

      var timeStamp = DateTimeOffset.FromUnixTimeMilliseconds(_location.Time);

      var location = new Microsoft.Maui.Devices.Sensors.Location(_location.Latitude, _location.Longitude, _location.Altitude)
      {
        Accuracy = _location.Accuracy,
        Course = _location.HasBearing ? _location.Bearing : null,
        Speed = _location.HasSpeed ? _location.Speed : null,
        Timestamp = timeStamp,
      };
      if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
#pragma warning disable CA1416 // Validate platform compatibility
        location.VerticalAccuracy = _location.HasVerticalAccuracy ? _location.VerticalAccuracyMeters : null;
#pragma warning restore CA1416 // Validate platform compatibility

      p_locationFlow.OnNext(location);
    }
    finally
    {
      _location.Dispose();
    }
  }

  public void OnStatusChanged(string? _provider, [GeneratedEnum] Availability _status, Bundle? _extras)
  {
    if (_provider.IsNullOrWhiteSpace())
      return;

    p_logger.Info($"Provider '{_provider}' now in new state: '{_status}'");
  }

  public void OnProviderDisabled(string _provider)
  {
    p_logger.Info($"Provider '{_provider}' was disabled");

    if (_provider == LocationManager.PassiveProvider)
      return;

    p_activeProviders = p_activeProviders.Remove(_provider);
  }

  public void OnProviderEnabled(string _provider)
  {
    p_logger.Info($"Provider '{_provider}' was enabled");

    if (_provider == LocationManager.PassiveProvider)
      return;

    p_activeProviders = p_activeProviders.Add(_provider);
  }

  public async Task<Microsoft.Maui.Devices.Sensors.Location?> GetCurrentBestLocationAsync(TimeSpan _timeout, CancellationToken _ct)
  {
    try
    {
      var request = new GeolocationRequest(GeolocationAccuracy.Best, _timeout);
      var location = await Geolocation.GetLocationAsync(request, _ct);
      return location;
    }
    catch (FeatureNotSupportedException)
    {
      return null;
    }
    catch (FeatureNotEnabledException)
    {
      return null;
    }
    catch (PermissionException)
    {
      return null;
    }
  }

}
#endif