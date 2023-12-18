﻿using Android.Content;
using Android.Locations;
using Android.OS;
using Android.Runtime;
using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using JustLogger.Interfaces;
using Roadnik.MAUI.Interfaces;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Roadnik.MAUI.Modules.LocationProvider;

public class AndroidLocationProviderImpl : Java.Lang.Object, ILocationListener, ILocationProvider, IAppModule<AndroidLocationProviderImpl>
{
  public static AndroidLocationProviderImpl ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance((ILogger _logger) => new AndroidLocationProviderImpl(_logger));
  }

  private readonly LocationManager p_locationManager;
  private readonly ReplaySubject<Microsoft.Maui.Devices.Sensors.Location> p_locationFlow = new(1);
  private readonly ILogger p_logger;
  private TimeSpan p_minTimePeriod = TimeSpan.FromSeconds(1);
  private float p_minDistanceMeters = 0;
  private long p_enabled = 0;

  private AndroidLocationProviderImpl(ILogger _logger)
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

    var knownProviders = new[] { "gps", "passive" };
    var providers = p_locationManager
      .GetProviders(false)
      .ToArray();

    var usableProviders = providers
      .Intersect(knownProviders)
      .ToArray();

    if (usableProviders.Length == 0)
    {
      p_logger.Error($"There is not known providers: <{string.Join(">, <", providers)}>");
      return;
    }

    p_logger.Info($"Starting updates, providers: <{string.Join(">, <", providers)}>");

    MainThread.BeginInvokeOnMainThread(() =>
    {
      foreach (var provider in usableProviders)
      {
        p_locationManager.RequestLocationUpdates(provider, (long)p_minTimePeriod.TotalMilliseconds, p_minDistanceMeters, this);
        p_logger.Info($"Subscribed to '{provider}' provider");
      }
    });
  }

  public void Disable()
  {
    var oldEnabled = Interlocked.Exchange(ref p_enabled, 0);

    if (oldEnabled == 0)
      return;

    p_logger.Info($"Stopping updates...");

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
  }

  public void OnProviderEnabled(string _provider)
  {
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
