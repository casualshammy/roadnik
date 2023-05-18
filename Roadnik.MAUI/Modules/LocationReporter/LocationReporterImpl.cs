#if ANDROID
using global::Android.Telephony;
#endif
using Ax.Fw;
using Ax.Fw.Attributes;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using JustLogger.Interfaces;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using System.Globalization;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using static Roadnik.MAUI.Data.Consts;
using Microsoft.Maui.Controls.Shapes;

namespace Roadnik.MAUI.Modules.LocationReporter;

[ExportClass(typeof(ILocationReporter), Singleton: true, ActivateOnStart: true)]
internal class LocationReporterImpl : ILocationReporter
{
  record ForceReqData(DateTimeOffset DateTime, bool Ok);
  record ReportingCtx(Location? Location, DateTimeOffset? LastTimeReported);

  private readonly ReplaySubject<LocationReporterSessionStats> p_statsFlow = new(1);
  private readonly ReplaySubject<bool> p_enableFlow = new(1);
  private readonly ILogger p_log;

  public LocationReporterImpl(
    IReadOnlyLifetime _lifetime,
    ILogger _log,
    IPreferencesStorage _storage,
    IHttpClientProvider _httpClientProvider,
    ILocationProvider _locationProvider,
    ITelephonyMgrProvider _telephonyMgrProvider)
  {
    p_log = _log["location-reporter"];

    _lifetime.DisposeOnCompleted(Pool<EventLoopScheduler>.Get(out var reportScheduler));

    var prefsFlow = _storage.PreferencesChanged
      .Select(_ => new
      {
        ServerAddress = _storage.GetValueOrDefault<string>(PREF_SERVER_ADDRESS),
        RoomId = _storage.GetValueOrDefault<string>(PREF_ROOM),
        TimeInterval = TimeSpan.FromSeconds(_storage.GetValueOrDefault<int>(PREF_TIME_INTERVAL)),
        DistanceInterval = _storage.GetValueOrDefault<int>(PREF_DISTANCE_INTERVAL),
        ReportingCondition = _storage.GetValueOrDefault<TrackpointReportingConditionType>(PREF_TRACKPOINT_REPORTING_CONDITION),
        UserMsg = _storage.GetValueOrDefault<string>(PREF_USER_MSG),
        MinAccuracy = _storage.GetValueOrDefault<int>(PREF_MIN_ACCURACY),
        Username = _storage.GetValueOrDefault<string>(PREF_USERNAME)
      })
      .Replay(1)
      .RefCount();

    var timerFlow = prefsFlow
      .Select(_prefs =>
      {
        if (_prefs.ReportingCondition == TrackpointReportingConditionType.TimeOrDistance)
          return Observable
            .Interval(_prefs.TimeInterval, reportScheduler)
            .StartWithDefault();

        return Observable
          .Return(Unit.Default)
          .Select(_ => 0L);
      })
      .Switch()
      .ToUnit();

    var batteryStatsFlow = Observable
      .FromEventPattern<BatteryInfoChangedEventArgs>(_ => Battery.Default.BatteryInfoChanged += _, _ => Battery.Default.BatteryInfoChanged -= _)
      .Select(_ => _.EventArgs)
      .Do(_ => p_log.Info($"Battery level: {_.ChargeLevel*100}"))
      .StartWith(new BatteryInfoChangedEventArgs(Battery.Default.ChargeLevel, Battery.Default.State, Battery.Default.PowerSource));

    var locationFlow = _locationProvider.Location
      .CombineLatest(prefsFlow)
      .Where(_ =>
      {
        var (location, prefs) = _;
        return location.Accuracy != null && location.Accuracy.Value < prefs.MinAccuracy;
      })
      .Select(_ => _.First);

    var filteredLocationFlow = _locationProvider.FilteredLocation
      .CombineLatest(prefsFlow)
      .Where(_ =>
      {
        var (location, prefs) = _;
        return location.Accuracy != null && location.Accuracy.Value < prefs.MinAccuracy;
      })
      .Select(_ => _.First);

    var counter = 0L;
    var stats = LocationReporterSessionStats.Empty;
    
    var reportFlow = timerFlow
      .CombineLatest(batteryStatsFlow, _telephonyMgrProvider.SignalLevel, prefsFlow, locationFlow, filteredLocationFlow)
      .Sample(TimeSpan.FromSeconds(1), reportScheduler)
      .Do(_ => Interlocked.Increment(ref counter))
      .Where(_ =>
      {
        var queue = Interlocked.Read(ref counter);
        if (queue <= 1)
          return true;

        Interlocked.Decrement(ref counter);
        return false;
      })
      .ObserveOn(reportScheduler)
      .ScanAsync(new ReportingCtx(null, null), async (_acc, _entry) =>
      {
        var (_, batteryStat, signalStrength, prefs, location, filteredLocation) = _entry;
        var now = DateTimeOffset.UtcNow;
        try
        {
          if (string.IsNullOrWhiteSpace(prefs.ServerAddress) || string.IsNullOrWhiteSpace(prefs.RoomId))
            return _acc;

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

          stats = stats with { Total = stats.Total + 1 };
          p_statsFlow.OnNext(stats);

          var url = GetUrl(prefs.ServerAddress, prefs.RoomId, prefs.Username, prefs.UserMsg, location, batteryStat, signalStrength);
          var res = await _httpClientProvider.Value.GetAsync(url, _lifetime.Token);
          res.EnsureSuccessStatusCode();

          var filteredUrl = GetUrl(prefs.ServerAddress, $"{prefs.RoomId}-f", prefs.Username, prefs.UserMsg, filteredLocation, batteryStat, signalStrength);
          var resFiltered = await _httpClientProvider.Value.GetAsync(filteredUrl, _lifetime.Token);
          resFiltered.EnsureSuccessStatusCode();

          stats = stats with { Successful = stats.Successful + 1 };
          p_statsFlow.OnNext(stats);

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
        catch (HttpRequestException httpEx) when (httpEx.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
          p_log.Warn($"Too many requests (HTTP 429)");
        }
        catch (Exception ex)
        {
          p_log.Error($"Geo location generic error", ex);
        }
        return _acc with { LastTimeReported = now };
      })
      .Do(_ => Interlocked.Decrement(ref counter));

    _lifetime.DisposeOnCompleted(Pool<EventLoopScheduler>.Get(out var locationProviderStateScheduler));

    p_enableFlow
      .ObserveOn(locationProviderStateScheduler)
      .HotAlive(_lifetime, (_enable, _life) =>
      {
        if (!_enable)
          return;

        _life.DoOnCompleted(() => stats = LocationReporterSessionStats.Empty);
        _life.DoOnCompleted(_locationProvider.Disable);
        _locationProvider.Enable();
        reportFlow.Subscribe(_life);
      });

    p_enableFlow.OnNext(false);

    prefsFlow
      .ObserveOn(locationProviderStateScheduler)
      .Subscribe(_ =>
      {
        if (_.ReportingCondition == TrackpointReportingConditionType.TimeAndDistance)
          _locationProvider.ChangeConstrains(_.TimeInterval, _.DistanceInterval);
        else
          _locationProvider.ChangeConstrains(_.TimeInterval, 0f);
      });
  }

  public IObservable<LocationReporterSessionStats> Stats => p_statsFlow;

  public async Task<bool> IsEnabledAsync() => await p_enableFlow.FirstOrDefaultAsync();

  public void SetState(bool _enabled)
  {
    p_enableFlow.OnNext(_enabled);
    p_log.Info($"Stats reporter {(_enabled ? "enabled" : "disable")}");
  }

  private static string GetUrl(
    string _serverAddress,
    string _roomId,
    string? _username,
    string? _userMsg,
    Location _location,
    BatteryInfoChangedEventArgs _batteryInfo,
    double? _signalStrength)
  {
    var culture = CultureInfo.InvariantCulture;
    var sb = new StringBuilder();
    sb.Append(_serverAddress.TrimEnd('/'));
    sb.Append("/store?roomId=");
    sb.Append(_roomId);
    sb.Append("&username=");
    sb.Append(_username ?? _roomId);
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
    sb.Append(((float)(_batteryInfo.ChargeLevel * 100)).ToString(culture));
    if (_signalStrength != null)
    {
      sb.Append("&gsm_signal=");
      sb.Append(((float)(_signalStrength.Value * 100)).ToString(culture));
    }
    if (_userMsg?.Length > 0)
    {
      sb.Append("&var=");
      sb.Append(_userMsg);
    }

    return sb.ToString();
  }

}
