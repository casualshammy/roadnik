﻿#if ANDROID
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
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using static Roadnik.MAUI.Data.Consts;
using Roadnik.Common.ReqRes;
using System.Net.Http.Json;
using System.Text.Json;
using Roadnik.Common.Toolkit;
using Ax.Fw.Pools;

namespace Roadnik.MAUI.Modules.LocationReporter;

[ExportClass(typeof(ILocationReporter), Singleton: true)]
internal class LocationReporterImpl : ILocationReporter
{
  record ForceReqData(DateTimeOffset DateTime, bool Ok);
  record ReportingCtx(Location? Location, DateTimeOffset? LastTimeReported);

  private readonly ReplaySubject<LocationReporterSessionStats> p_statsFlow = new(1);
  private readonly ReplaySubject<bool> p_enableFlow = new(1);
  private readonly ILogger p_log;
  private readonly IPreferencesStorage p_storage;
  private readonly IHttpClientProvider p_httpClientProvider;

  public LocationReporterImpl(
    IReadOnlyLifetime _lifetime,
    ILogger _log,
    IPreferencesStorage _storage,
    IHttpClientProvider _httpClientProvider,
    ILocationProvider _locationProvider,
    ITelephonyMgrProvider _telephonyMgrProvider)
  {
    p_log = _log["location-reporter"];
    p_storage = _storage;
    p_httpClientProvider = _httpClientProvider;

    _lifetime.ToDisposeOnEnded(SharedPool<EventLoopScheduler>.Get(out var reportScheduler));

    var prefsFlow = _storage.PreferencesChanged
      .Select(_ => new
      {
        ServerAddress = _storage.GetValueOrDefault<string>(PREF_SERVER_ADDRESS),
        RoomId = _storage.GetValueOrDefault<string>(PREF_ROOM),
        TimeInterval = TimeSpan.FromSeconds(_storage.GetValueOrDefault<int>(PREF_TIME_INTERVAL)),
        DistanceInterval = _storage.GetValueOrDefault<int>(PREF_DISTANCE_INTERVAL),
        ReportingCondition = _storage.GetValueOrDefault<TrackpointReportingConditionType>(PREF_TRACKPOINT_REPORTING_CONDITION),
        MinAccuracy = _storage.GetValueOrDefault<int>(PREF_MIN_ACCURACY),
        Username = _storage.GetValueOrDefault<string>(PREF_USERNAME)
      })
      .Replay(1)
      .RefCount();

    var timerFlow = prefsFlow
      .DistinctUntilChanged(_ => HashCode.Combine(_.ReportingCondition, _.TimeInterval))
      .Select(_prefs =>
      {
        if (_prefs.ReportingCondition == TrackpointReportingConditionType.TimeOrDistance)
          return Observable
            .Interval(_prefs.TimeInterval)
            .StartWithDefault();

        return Observable
          .Return(0L);
      })
      .Switch()
      .ToUnit();

    var locationFlow = _locationProvider.Location
      .WithLatestFrom(prefsFlow)
      .Where(_ =>
      {
        var (location, prefs) = _;
        return location.Accuracy != null && location.Accuracy.Value < prefs.MinAccuracy;
      })
      .Select(_ => _.First);

    var counter = 0L;
    var stats = LocationReporterSessionStats.Empty;

    var reportFlow = timerFlow
      .CombineLatest(_telephonyMgrProvider.SignalLevel, prefsFlow, locationFlow)
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
        var (_, signalStrength, prefs, location) = _entry;
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

          var batteryCharge = Battery.Default.ChargeLevel;

          stats = stats with { Total = stats.Total + 1 };
          p_statsFlow.OnNext(stats);

          p_log.Info($"Sending location data, lat: *{location.Latitude % 10}, lng: *{location.Longitude}, alt: {location.Altitude}, acc: {location.Accuracy}");

          var reqData = new StorePathPointReq()
          {
            RoomId = prefs.RoomId,
            Username = prefs.Username ?? prefs.RoomId,
            Lat = (float)location.Latitude,
            Lng = (float)location.Longitude,
            Alt = (float)(location.Altitude ?? 0d),
            Speed = (float)(location.Speed ?? 0d),
            Acc = (float)(location.Accuracy ?? 100d),
            Battery = (float)batteryCharge,
            GsmSignal = (float?)signalStrength,
            Bearing = (float)(location.Course ?? 0d),
          };

          using var res = await _httpClientProvider.Value.PostAsJsonAsync($"{prefs.ServerAddress.TrimEnd('/')}{ReqPaths.STORE_PATH_POINT}", reqData, _lifetime.Token);
          res.EnsureSuccessStatusCode();

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

    _lifetime.ToDisposeOnEnded(SharedPool<EventLoopScheduler>.Get(out var locationProviderStateScheduler));

    p_enableFlow
      .ObserveOn(locationProviderStateScheduler)
      .HotAlive(_lifetime, (_enable, _life) =>
      {
        if (!_enable)
          return;

        _life.DoOnEnding(() => stats = LocationReporterSessionStats.Empty);
        _life.DoOnEnding(_locationProvider.Disable);
        _locationProvider.Enable();
        reportFlow.Subscribe(_life);
      });

    p_enableFlow.OnNext(false);

    prefsFlow
      .ObserveOn(locationProviderStateScheduler)
      .DistinctUntilChanged(_ => HashCode.Combine(_.ReportingCondition, _.TimeInterval, _.DistanceInterval))
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

  public async Task ReportStartNewPathAsync(CancellationToken _ct = default)
  {
    var serverAddress = p_storage.GetValueOrDefault<string>(PREF_SERVER_ADDRESS);
    if (serverAddress.IsNullOrWhiteSpace())
      return;

    var roomId = p_storage.GetValueOrDefault<string>(PREF_ROOM);
    if (roomId.IsNullOrWhiteSpace())
      return;

    var username = p_storage.GetValueOrDefault<string>(PREF_USERNAME);
    if (username.IsNullOrWhiteSpace())
      return;

    var wipeOldTrack = p_storage.GetValueOrDefault<bool>(PREF_WIPE_OLD_TRACK_ON_NEW_ENABLED);

    var counter = 10;
    while (counter-- > 0 && !_ct.IsCancellationRequested)
    {
      try
      {
        p_log.Info($"Sending request to start new track '{roomId}/{username}'; retry: '{counter}'...");
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{serverAddress.TrimEnd('/')}{ReqPaths.START_NEW_PATH}");
        using var content = JsonContent.Create(new StartNewPathReq(roomId, username, wipeOldTrack));
        req.Content = content;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var res = await p_httpClientProvider.Value.SendAsync(req, cts.Token);
        res.EnsureSuccessStatusCode();
        p_log.Info($"Sent request to start new track '{roomId}/{username}'");
        break;
      }
      catch (Exception ex)
      {
        p_log.Error($"Request to start new path '{roomId}/{username}' was completed with error (retry: '{counter}')", ex);
        await Task.Delay(TimeSpan.FromSeconds(6), _ct);
      }
    }
  }

}
