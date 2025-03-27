using Android.Content;
using Android.Locations;
using Android.OS;
using Android.Runtime;
using Ax.Fw.Extensions;
using Ax.Fw.Pools;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Data.LocationProvider;
using Roadnik.MAUI.Interfaces;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Roadnik.MAUI.Modules.LocationProvider;

internal class AndroidLocationProvider : Java.Lang.Object, ILocationListener, ILocationProvider
{
  private static readonly LocationManager p_locationService;
  private static readonly Pool<ReplaySubject<LocationData>> p_locationSubjPool;
  private readonly ReplaySubject<LocationData> p_locationFlow;
  private readonly Subject<string> p_providerDisabledSubj;
  private readonly Subject<string> p_providerEnabledSubj;
  private readonly ILog p_logger;
  private readonly object p_startStopLock = new();

  static AndroidLocationProvider()
  {
    p_locationService = (LocationManager)Platform.AppContext.GetSystemService(Context.LocationService)!;
    p_locationSubjPool = new Pool<ReplaySubject<LocationData>>(() => new ReplaySubject<LocationData>(1), null);
  }

  public AndroidLocationProvider(ILog _logger, IReadOnlyLifetime _lifetime)
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

  public IObservable<LocationData> Location { get; }
  public IObservable<string> ProviderDisabled { get; }
  public IObservable<string> ProviderEnabled { get; }

  public void StartLocationWatcher(LocationProviders _providers, TimeSpan _frequency)
  {
    var providers = new List<string>();
    if ((_providers & LocationProviders.Gps) != 0)
      providers.Add(LocationManager.GpsProvider);
    if ((_providers & LocationProviders.Network) != 0)
      providers.Add(LocationManager.NetworkProvider);
    if ((_providers & LocationProviders.Passive) != 0)
      providers.Add(LocationManager.PassiveProvider);

    StartLocationWatcher(providers, _frequency);
  }

  public void StartLocationWatcher(IReadOnlyList<string> _providers, TimeSpan _frequency)
  {
    if (!p_locationService.IsLocationEnabled)
      return;

    MainThread.BeginInvokeOnMainThread(() =>
    {
      lock (p_startStopLock)
      {
        p_logger.Info($"Subscribing to location updates, desired providers: '{string.Join(", ", _providers)}'; interval: {_frequency}...");
        var result = new HashSet<string>();

        foreach (var provider in _providers.Distinct())
        {
          if (!p_locationService.IsProviderEnabled(provider))
            continue;

          try
          {
            p_locationService.RequestLocationUpdates(provider, (long)_frequency.TotalMilliseconds, 0f, this);
            result.Add(provider);
            p_logger.Info($"Subscribed to '{provider}' provider");
          }
          catch (Exception ex)
          {
            p_logger.Error($"Can't subscribe to provider '{provider}'", ex);
          }
        }

        p_logger.Info($"Subscribed to location updates, providers: '{string.Join(", ", result)}'; interval: {_frequency}");
      }
    });
  }

  public void StopLocationWatcher()
  {
    p_logger.Info($"Unsubscribing from location updates...");

    MainThread.BeginInvokeOnMainThread(() =>
    {
      lock (p_startStopLock)
      {
        try
        {
          p_locationService.RemoveUpdates(this);
          p_logger.Info($"Unsubscribed from location updates");
        }
        catch (Exception ex)
        {
          p_logger.Error($"Can't unsubscribe from location updates!", ex);
        }
      }
    });
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

      var location = new LocationData(
        _location.Latitude,
        _location.Longitude,
        _location.Altitude,
        _location.Accuracy,
        _location.HasVerticalAccuracy ? _location.VerticalAccuracyMeters : null,
        _location.HasBearing ? _location.Bearing : null,
        _location.HasSpeed ? _location.Speed : null,
        timeStamp);

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

  public static async Task<LocationData?> GetCurrentBestLocationAsync(TimeSpan _timeout, CancellationToken _ct)
  {
    try
    {
      var request = new GeolocationRequest(GeolocationAccuracy.Best, _timeout);
      var location = await Geolocation.GetLocationAsync(request, _ct);
      if (location == null)
        return null;

      return new LocationData(
        Latitude: location.Latitude,
        Longitude: location.Longitude,
        Altitude: location.Altitude ?? 0d,
        Accuracy: (float?)location.Accuracy ?? 1000f,
        VerticalAccuracy: (float?)location.VerticalAccuracy,
        Course: (float?)location.Course,
        Speed: (float?)location.Speed,
        Timestamp: location.Timestamp);
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
