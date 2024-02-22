using Android.Content;
using Android.Locations;
using Android.OS;
using Android.Runtime;
using Ax.Fw.Extensions;
using Ax.Fw.Pools;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.Toolkit;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Roadnik.MAUI.Modules.LocationProvider;

internal class AndroidLocationProvider : Java.Lang.Object, ILocationListener, ILocationProvider
{
  private static readonly LocationManager p_locationService;
  private static readonly Pool<ReplaySubject<Microsoft.Maui.Devices.Sensors.Location>> p_locationSubjPool;
  private readonly ReplaySubject<Microsoft.Maui.Devices.Sensors.Location> p_locationFlow;
  private readonly Subject<string> p_providerDisabledSubj;
  private readonly Subject<string> p_providerEnabledSubj;
  private readonly ILogger p_logger;
  private readonly object p_startStopLock = new();

  static AndroidLocationProvider()
  {
    p_locationService = (LocationManager)Platform.AppContext.GetSystemService(Context.LocationService)!;
    p_locationSubjPool = new Pool<ReplaySubject<Microsoft.Maui.Devices.Sensors.Location>>(() => new ReplaySubject<Microsoft.Maui.Devices.Sensors.Location>(1), null);
  }

  public AndroidLocationProvider(ILogger _logger, IReadOnlyLifetime _lifetime)
  {
    p_logger = _logger["location-provider"];

    _lifetime.ToDisposeOnEnded(p_locationSubjPool.Get(out p_locationFlow));

    Location = p_locationFlow
      .DistinctUntilChanged(_ => HashCode.Combine(_.Latitude, _.Longitude, _.Timestamp));

    _lifetime.ToDisposeOnEnded(SharedPool<Subject<string>>.Get(out p_providerDisabledSubj));
    ProviderDisabled = p_providerDisabledSubj;

    _lifetime.ToDisposeOnEnded(SharedPool<Subject<string>>.Get(out p_providerEnabledSubj));
    ProviderEnabled = p_providerEnabledSubj;
  }

  public IObservable<Microsoft.Maui.Devices.Sensors.Location> Location { get; }
  public IObservable<string> ProviderDisabled { get; }
  public IObservable<string> ProviderEnabled { get; }

  public void StartLocationWatcher(IReadOnlyList<string> _providers, out IReadOnlySet<string> _providersEnabled)
  {
    var result = new HashSet<string>();
    _providersEnabled = result;

    if (!p_locationService.IsLocationEnabled)
      return;

    lock (p_startStopLock)
    {
      p_logger.Info($"Starting updates, desired providers: '{string.Join(", ", _providers)}'");
      foreach (var provider in _providers.Distinct())
      {
        if (!p_locationService.IsProviderEnabled(provider))
          continue;

        result.Add(provider);

        var success = MainThreadExt.Invoke(() =>
        {
          try
          {
            p_locationService.RequestLocationUpdates(provider, 1000L, 0f, this);
            p_logger.Info($"Subscribed to '{provider}' provider");
          }
          catch (Exception ex)
          {
            p_logger.Error($"Can't subscribe to provider '{provider}'", ex);
          }
        });

        if (!success)
          p_logger.Error($"Subscription routine is timed out");
      }
      p_logger.Info($"Updates are requested, providers: '{string.Join(", ", result)}'");
    }
  }

  public void StopLocationWatcher()
  {
    lock (p_startStopLock)
    {
      p_logger.Info($"Stopping updates...");

      var success = MainThreadExt.Invoke(() =>
      {
        try
        {
          p_locationService.RemoveUpdates(this);
          p_logger.Info($"Updates are stopped");
        }
        catch (Exception ex)
        {
          p_logger.Error($"Can't remove updates!", ex);
        }
      });

      if (!success)
        p_logger.Error($"Un-subscription routine is timed out");
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
        VerticalAccuracy = _location.HasVerticalAccuracy ? _location.VerticalAccuracyMeters : null,
      };

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
    p_providerDisabledSubj.OnNext(_provider);
    p_logger.Info($"Provider '{_provider}' was disabled");
  }

  public void OnProviderEnabled(string _provider)
  {
    p_providerEnabledSubj.OnNext(_provider);
    p_logger.Info($"Provider '{_provider}' was enabled");
  }

  public static async Task<Microsoft.Maui.Devices.Sensors.Location?> GetCurrentBestLocationAsync(TimeSpan _timeout, CancellationToken _ct)
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
