using Android.Content;
using Android.Locations;
using Android.OS;
using Android.Runtime;
using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.MAUI.Interfaces;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Roadnik.MAUI.Modules.LocationProvider;

public class AndroidLocationProviderImpl : Java.Lang.Object, ILocationListener, ILocationProvider, IAppModule<ILocationProvider>
{
  public static ILocationProvider ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance((ILogger _logger) => new AndroidLocationProviderImpl(_logger));
  }

  private readonly LocationManager p_locationService;
  private readonly ReplaySubject<Microsoft.Maui.Devices.Sensors.Location> p_locationFlow = new(1);
  private readonly Subject<string> p_providerDisabledSubj = new();
  private readonly Subject<string> p_providerEnabledSubj = new();
  private readonly ILogger p_logger;
  private readonly HashSet<string> p_clients = [];
  private readonly object p_startStopLock = new();

  private AndroidLocationProviderImpl(ILogger _logger)
  {
    p_logger = _logger["location-provider"];
    p_locationService = (LocationManager)Platform.AppContext.GetSystemService(Context.LocationService)!;

    Location = p_locationFlow
      .DistinctUntilChanged(_ => HashCode.Combine(_.Latitude, _.Longitude, _.Timestamp));

    ProviderDisabled = p_providerDisabledSubj;
    ProviderEnabled = p_providerEnabledSubj;
  }

  public IObservable<Microsoft.Maui.Devices.Sensors.Location> Location { get; }
  public IObservable<string> ProviderDisabled { get; }
  public IObservable<string> ProviderEnabled { get; }

  public void StartLocationWatcher(string _clientId, out bool _providerEnabled)
  {
    _providerEnabled = 
      p_locationService.IsLocationEnabled && 
      p_locationService.IsProviderEnabled(LocationManager.GpsProvider);

    lock (p_startStopLock)
    {
      p_clients.Add(_clientId);
      p_logger.Info($"Added new client: '{_clientId}'");

      if (p_clients.Count > 1)
        return;

      p_logger.Info($"Starting updates, providers: '{LocationManager.GpsProvider}'");

      MainThread.BeginInvokeOnMainThread(() =>
      {
        p_locationService.RequestLocationUpdates(LocationManager.GpsProvider, 1000L, 0f, this);
        p_logger.Info($"Subscribed to '{LocationManager.GpsProvider}' provider");
      });
    }
  }

  public void StopLocationWatcher(string _clientId)
  {
    lock (p_startStopLock)
    {
      p_clients.Remove(_clientId);
      p_logger.Info($"Removed client: '{_clientId}'");

      if (p_clients.Count > 0)
        return;

      p_logger.Info($"Stopping updates...");

      try
      {
        MainThread.BeginInvokeOnMainThread(() => p_locationService.RemoveUpdates(this));
      }
      catch (Exception ex)
      {
        p_logger.Error($"Can't remove updates!", ex);
      }
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
