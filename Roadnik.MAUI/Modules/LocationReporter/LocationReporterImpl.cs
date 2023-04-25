#if ANDROID
using global::Android.Telephony;
#endif
using Ax.Fw;
using Ax.Fw.Attributes;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using JustLogger.Interfaces;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Devices;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using Microsoft.Extensions.Primitives;
using System.Globalization;

namespace Roadnik.MAUI.Modules.LocationReporter;

[ExportClass(typeof(ILocationReporter), Singleton: true, ActivateOnStart: true)]
internal class LocationReporterImpl : ILocationReporter
{
  record ForceReqData(DateTimeOffset DateTime, bool Ok);
  record ReportingCtx(Location? Location, DateTimeOffset? LastTimeReported);

  private readonly ReplaySubject<Location> p_locationFlow = new(1);
  private readonly Subject<Unit> p_forceReload = new();
  private readonly ReplaySubject<bool> p_enableFlow = new(1);
  private readonly ILogger p_log;

  public LocationReporterImpl(
    IReadOnlyLifetime _lifetime,
    ILogger _log,
    IPreferencesStorage _storage,
    IHttpClientProvider _httpClientProvider)
  {
    p_log = _log["location-reporter"];

    var prefsFlow = _storage.PreferencesChanged
      .Select(_ => new
      {
        ServerAddress = _storage.GetValueOrDefault<string>(_storage.SERVER_ADDRESS),
        ServerKey = _storage.GetValueOrDefault<string>(_storage.SERVER_KEY),
        TimeInterval = TimeSpan.FromSeconds(_storage.GetValueOrDefault<int>(_storage.TIME_INTERVAL)),
        DistanceInterval = _storage.GetValueOrDefault<int>(_storage.DISTANCE_INTERVAL),
        ReportingCondition = _storage.GetValueOrDefault<TrackpointReportingConditionType>(_storage.TRACKPOINT_REPORTING_CONDITION)
      });

    var batteryStatsFlow = Observable
      .FromEventPattern<BatteryInfoChangedEventArgs>(_ => Battery.BatteryInfoChanged += _, _ => Battery.BatteryInfoChanged -= _)
      .Select(_ => _.EventArgs)
      .StartWith(new BatteryInfoChangedEventArgs(Battery.Default.ChargeLevel, Battery.Default.State, Battery.Default.PowerSource));

    var signalStrengthFlow = Observable
      .Interval(TimeSpan.FromMinutes(1))
      .StartWithDefault()
      .Select(_ => GetSignalStrength());

    var reportInterval = TimeSpan.FromSeconds(10);

    _lifetime.DisposeOnCompleted(Pool<EventLoopScheduler>.Get(out var scheduler));

    var forceReqFlow = p_forceReload
      .Scan(new ForceReqData(DateTimeOffset.MinValue, true), (_acc, _entry) =>
      {
        var now = DateTimeOffset.UtcNow;
        if (now - _acc.DateTime < reportInterval)
          return _acc with { Ok = false };

        return new ForceReqData(now, true);
      })
      .Where(_ => _.Ok)
      .ToUnit();

    var counter = 0L;

    var reportFlow = Observable
      .Interval(TimeSpan.FromSeconds(1.01), scheduler)
      .ToUnit()
      .Merge(forceReqFlow)
      .CombineLatest(batteryStatsFlow, signalStrengthFlow, prefsFlow)
      .Sample(TimeSpan.FromSeconds(1), scheduler)
      .Do(_ => Interlocked.Increment(ref counter))
      .Where(_ =>
      {
        var queue = Interlocked.Read(ref counter);
        if (queue <= 1)
          return true;

        Interlocked.Decrement(ref counter);
        return false;
      })
      .ObserveOn(scheduler)
      .ScanAsync(new ReportingCtx(null, null), async (_acc, _entry) =>
      {
        var (_, batteryStat, signalStrength, prefs) = _entry;
        var now = DateTimeOffset.UtcNow;
        try
        {
          if (string.IsNullOrWhiteSpace(prefs.ServerAddress) || string.IsNullOrWhiteSpace(prefs.ServerKey))
            return _acc;

          var location = await GetCurrentBestLocationAsync(TimeSpan.FromSeconds(10), _lifetime.Token);
          if (location == null)
            return _acc;

          p_locationFlow.OnNext(location);

          var distance = _acc.Location?.CalculateDistance(location, DistanceUnits.Kilometers) * 1000;

          if (prefs.ReportingCondition == TrackpointReportingConditionType.TimeAndDistance)
          {
            if (distance != null && distance < prefs.DistanceInterval)
              return _acc;
            if (_acc.LastTimeReported != null && now - _acc.LastTimeReported < prefs.TimeInterval)
              return _acc;
          }
          else if (prefs.ReportingCondition == TrackpointReportingConditionType.TimeOrDistance)
          {
            if (distance != null && _acc.LastTimeReported != null && distance < prefs.DistanceInterval && now - _acc.LastTimeReported < prefs.TimeInterval)
              return _acc;
          }

          var url = GetUrl(prefs.ServerAddress, prefs.ServerKey, location, batteryStat, signalStrength);
          var res = await _httpClientProvider.Value.GetAsync(url, _lifetime.Token);
          res.EnsureSuccessStatusCode();
          return new ReportingCtx(location, now);
        }
        catch (FeatureNotSupportedException fnsEx)
        {
          p_log.Error($"Geo location is not supported on this device", fnsEx);
        }
        catch (FeatureNotEnabledException fneEx)
        {
          p_log.Error($"Geo location is not enabled on this device", fneEx);
        }
        catch (PermissionException pEx)
        {
          p_log.Error($"Geo location is not permitted", pEx);
        }
        catch (Exception ex)
        {
          p_log.Error($"Geo location generic error", ex);
        }
        return _acc with { LastTimeReported = now };
      })
      .Do(_ => Interlocked.Decrement(ref counter));

    p_enableFlow
      .Scan((ILifetime?)null, (_acc, _enable) =>
      {
        if (_enable)
        {
          if (_acc != null)
            return _acc;

          var life = _lifetime.GetChildLifetime();
          if (life == null)
            return _acc;

          reportFlow.Subscribe(life);
          return life;
        }
        else
        {
          if (_acc == null)
            return _acc;

          _acc.Complete();
          return null;
        }
      })
      .Subscribe(_lifetime);

    p_enableFlow.OnNext(false);
  }

  public IObservable<Location> Location => p_locationFlow;
  public async Task<bool> IsEnabled() => await p_enableFlow.FirstOrDefaultAsync();

  public void SetState(bool _enabled)
  {
    p_enableFlow.OnNext(_enabled);
    if (_enabled)
      p_forceReload.OnNext();

    p_log.Info($"Location reporter {(_enabled ? "enabled" : "disable")}");
  }

  public async Task<Location?> GetCurrentBestLocationAsync(TimeSpan _timeout, CancellationToken _ct)
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

  public async Task<Location?> GetCurrentAnyLocationAsync(TimeSpan _timeout, CancellationToken _ct)
  {
    try
    {
      var request = new GeolocationRequest(GeolocationAccuracy.Medium, _timeout);
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

  private static double? GetSignalStrength()
  {
#if ANDROID
    var service = Android.App.Application.Context.GetSystemService(Android.Content.Context.TelephonyService) as TelephonyManager;
    if (service == null || service.AllCellInfo == null)
      return null;

    static double normalize(int _level)
    {
      if (_level == 0)
        return 0.01;

      return (_level + 1) * 20 / 100d;
    }

    var signalStrength = 0d;
    foreach (var info in service.AllCellInfo)
    {
      // we cast to specific type because getting value of abstract field `CellSignalStrength` raises exception
      if (info is CellInfoWcdma wcdma && wcdma.CellSignalStrength != null)
        signalStrength = Math.Max(signalStrength, normalize(wcdma.CellSignalStrength.Level));
      else if (info is CellInfoGsm gsm && gsm.CellSignalStrength != null)
        signalStrength = Math.Max(signalStrength, normalize(gsm.CellSignalStrength.Level));
      else if (info is CellInfoLte lte && lte.CellSignalStrength != null)
        signalStrength = Math.Max(signalStrength, normalize(lte.CellSignalStrength.Level));
      else if (info is CellInfoCdma cdma && cdma.CellSignalStrength != null)
        signalStrength = Math.Max(signalStrength, normalize(cdma.CellSignalStrength.Level));
    }
    return signalStrength;
#else
    return null;
#endif
  }

  private static string GetUrl(
    string _serverAddress,
    string _serverKey,
    Location _location,
    BatteryInfoChangedEventArgs _batteryInfo,
    double? _signalStrength)
  {
    var culture = CultureInfo.InvariantCulture;
    var sb = new StringBuilder();
    sb.Append(_serverAddress.TrimEnd('/'));
    sb.Append("/store?key=");
    sb.Append(_serverKey);
    sb.Append("&lat=");
    sb.Append(_location.Latitude.ToString(culture));
    sb.Append("&lon=");
    sb.Append(_location.Longitude.ToString(culture));
    sb.Append("&alt=");
    sb.Append(_location.Altitude?.ToString(culture) ?? "0");
    sb.Append("&speed=");
    sb.Append(_location.Speed?.ToString(culture) ?? "0");
    sb.Append("&acc=");
    sb.Append(_location.Accuracy?.ToString(culture) ?? "100");
    sb.Append("&bearing=");
    sb.Append(_location.Course?.ToString(culture) ?? "0");
    sb.Append("&battery=");
    sb.Append((_batteryInfo.ChargeLevel * 100).ToString(culture));
    if (_signalStrength != null)
    {
      sb.Append("&gsm_signal=");
      sb.Append((_signalStrength.Value * 100).ToString(culture));
    }

    return sb.ToString();
  }

}
