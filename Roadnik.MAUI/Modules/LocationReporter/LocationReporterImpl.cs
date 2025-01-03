﻿using Android.App;
using Android.Content;
using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.Pools;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Common.ReqRes;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.Modules.LocationProvider;
using Roadnik.MAUI.Platforms.Android.Services;
using Roadnik.MAUI.Toolkit;
using System.Net.Http.Json;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.Json;
using static Roadnik.MAUI.Data.Consts;

namespace Roadnik.MAUI.Modules.LocationReporter;

internal class LocationReporterImpl : ILocationReporter, IAppModule<ILocationReporter>
{
  public static ILocationReporter ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance(
      (IReadOnlyLifetime _lifetime,
      ILog _log,
      IPreferencesStorage _storage,
      IHttpClientProvider _httpClientProvider,
      ITelephonyMgrProvider _telephonyMgrProvider)
      => new LocationReporterImpl(_lifetime, _log["location-reporter"], _storage, _httpClientProvider, _telephonyMgrProvider));
  }

  record ReportingCtx(
    int? SessionId,
    LocationData? Location,
    DateTimeOffset? LastReportAttemptTime)
  {
    public static ReportingCtx Empty { get; } = new ReportingCtx(null, null, null);
  }

  private readonly ReplaySubject<LocationReporterSessionStats> p_statsFlow = new(1);
  private readonly ReplaySubject<bool> p_enableFlow = new(1);
  private readonly ILog p_log;
  private readonly IPreferencesStorage p_storage;
  private readonly IHttpClientProvider p_httpClientProvider;

  private LocationReporterImpl(
    IReadOnlyLifetime _lifetime,
    ILog _log,
    IPreferencesStorage _storage,
    IHttpClientProvider _httpClientProvider,
    ITelephonyMgrProvider _telephonyMgrProvider)
  {
    p_log = _log;
    p_storage = _storage;
    p_httpClientProvider = _httpClientProvider;

    var locationProvider = new AndroidLocationProvider(p_log, _lifetime);
    _lifetime.ToDisposeOnEnded(SharedPool<EventLoopScheduler>.Get(out var reportScheduler));
    var reportQueueCounter = 0L;
    var stats = LocationReporterSessionStats.Empty;

    var prefsFlow = _storage.PreferencesChanged
      .Select(_ => new
      {
        ServerAddress = _storage.GetValueOrDefault<string>(PREF_SERVER_ADDRESS),
        RoomId = _storage.GetValueOrDefault<string>(PREF_ROOM),
        TimeInterval = TimeSpan.FromSeconds(_storage.GetValueOrDefault<int>(PREF_TIME_INTERVAL)),
        DistanceInterval = _storage.GetValueOrDefault<int>(PREF_DISTANCE_INTERVAL),
        ReportingCondition = _storage.GetValueOrDefault<TrackpointReportingConditionType>(PREF_TRACKPOINT_REPORTING_CONDITION),
        MinAccuracy = _storage.GetValueOrDefault<int>(PREF_MIN_ACCURACY),
        Username = _storage.GetValueOrDefault<string>(PREF_USERNAME),
        LowPowerMode = _storage.GetValueOrDefault<bool>(PREF_LOW_POWER_MODE),
        WipeOldPath = _storage.GetValueOrDefault<bool>(PREF_WIPE_OLD_TRACK_ON_NEW_ENABLED)
      })
      .Replay(1)
      .RefCount();

    var kalmanFilter = new KalmanFilter(100f * 1000 / 3600);
    var locationFlow = locationProvider.Location
      .DistinctUntilChanged(_ => _.Timestamp)
      .Buffer(TimeSpan.FromSeconds(1))
      .Select(_locations =>
      {
        if (_locations.Count == 0)
          return null;

        var location = _locations
          .OrderBy(_ => _.Accuracy)
          .First();

        stats = stats with
        {
          LastLocationFixTime = location.Timestamp,
          LastLocationFixAccuracy = (int)(location.Accuracy)
        };
        p_statsFlow.OnNext(stats);

        return location;
      })
      .WhereNotNull()
      .WithLatestFrom(prefsFlow)
      .Where(_tuple =>
      {
        var (location, prefs) = _tuple;
        return location.Accuracy <= prefs.MinAccuracy;
      })
      .Select(_tuple =>
      {
        var (location, prefs) = _tuple;
        if (!prefs.LowPowerMode)
          return location;

        var filteredLatLng = kalmanFilter.CalculateNext(
          location.Latitude,
          location.Longitude,
          location.Accuracy,
          location.Timestamp.ToUnixTimeMilliseconds());

        p_log.Info($"Kalman filter delta: {location.GetDistanceTo(filteredLatLng.Lat, filteredLatLng.Lng)}");

        return location with { Latitude = filteredLatLng.Lat, Longitude = filteredLatLng.Lng };
      });

    var batteryFlow = Observable
      .FromEventPattern<BatteryInfoChangedEventArgs>(_ => Battery.BatteryInfoChanged += _, _ => Battery.BatteryInfoChanged -= _)
      .Select(_ => _.EventArgs.ChargeLevel)
      .DistinctUntilChanged()
      .Replay(1)
      .RefCount();

    var signalLevelFlow = _telephonyMgrProvider.SignalLevel
      .DistinctUntilChanged()
      .Replay(1)
      .RefCount();

    var reportFlow = locationFlow
      .CombineLatest(prefsFlow)
      .Sample(TimeSpan.FromSeconds(1), reportScheduler)
      .Where(_ =>
      {
        var queue = Interlocked.Increment(ref reportQueueCounter);
        if (queue <= 10)
          return true;

        Interlocked.Decrement(ref reportQueueCounter);
        return false;
      })
      .ObserveOn(reportScheduler)
      .WithLatestFrom(batteryFlow, (_tuple, _battery) => (Location: _tuple.First, Prefs: _tuple.Second, Battery: _battery))
      .WithLatestFrom(signalLevelFlow, (_tuple, _signal) => (_tuple.Location, _tuple.Prefs, _tuple.Battery, Signal: _signal))
      .ScanAsync(ReportingCtx.Empty, async (_acc, _entry) =>
      {
        var (location, prefs, battery, signalStrength) = _entry;
        var now = DateTimeOffset.UtcNow;
        var acc = _acc;

        try
        {
          if (string.IsNullOrWhiteSpace(prefs.ServerAddress) || string.IsNullOrWhiteSpace(prefs.RoomId))
            return acc;

          var distance = acc.Location?.GetDistanceTo(location);

          if (prefs.ReportingCondition == TrackpointReportingConditionType.TimeAndDistance)
          {
            if (distance != null && distance < prefs.DistanceInterval)
              return acc;
            if (acc.LastReportAttemptTime != null && now - acc.LastReportAttemptTime < prefs.TimeInterval)
              return acc;
          }
          else if (prefs.ReportingCondition == TrackpointReportingConditionType.TimeOrDistance)
          {
            if (distance != null && acc.LastReportAttemptTime != null && distance < prefs.DistanceInterval && now - acc.LastReportAttemptTime < prefs.TimeInterval)
              return acc;
          }

          stats = stats with { Total = stats.Total + 1 };
          p_statsFlow.OnNext(stats);

          if (acc.SessionId == null)
          {
            acc = acc with { SessionId = Random.Shared.Next(int.MinValue, int.MaxValue) };
            p_log.Info($"New session id: {acc.SessionId}");
          }

          p_log.Info($"Sending location data, lat: {location.Latitude}, lng: {location.Longitude}, alt: {location.Altitude}, acc: {location.Accuracy}");

          var reqData = new StorePathPointReq
          {
            SessionId = acc.SessionId.Value,
            RoomId = prefs.RoomId,
            Username = prefs.Username ?? prefs.RoomId,
            Lat = (float)location.Latitude,
            Lng = (float)location.Longitude,
            Alt = (float)location.Altitude,
            Speed = (float)(location.Speed ?? 0d),
            Acc = (float)location.Accuracy,
            Battery = (float)battery,
            GsmSignal = (float?)signalStrength,
            Bearing = (float)(location.Course ?? 0d),
            WipeOldPath = prefs.WipeOldPath,
          };

          var reqDataJson = JsonSerializer.Serialize(reqData);
          using var content = new StringContent(reqDataJson, Encoding.UTF8, "application/json");
          using var res = await _httpClientProvider.Value.PostAsync($"{prefs.ServerAddress.TrimEnd('/')}{ReqPaths.STORE_PATH_POINT}", content, _lifetime.Token);
          res.EnsureSuccessStatusCode();

          stats = stats with { Successful = stats.Successful + 1, LastSuccessfulReportTime = now };
          p_statsFlow.OnNext(stats);

          return acc with { Location = location, LastReportAttemptTime = now };
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
        return acc with { LastReportAttemptTime = now };
      })
      .Do(_ => Interlocked.Decrement(ref reportQueueCounter));

    _lifetime.ToDisposeOnEnded(SharedPool<EventLoopScheduler>.Get(out var locationProviderStateScheduler));

    p_enableFlow
      .WithLatestFrom(prefsFlow)
      .ObserveOn(locationProviderStateScheduler)
      .HotAlive(_lifetime, locationProviderStateScheduler, (_tuple, _life) =>
      {
        var (enabled, conf) = _tuple;
        if (!enabled)
          return;

        _life.DoOnEnding(() =>
        {
          stats = LocationReporterSessionStats.Empty;
          p_statsFlow.OnNext(stats);
        });

        _life.DoOnEnding(() => locationProvider.StopLocationWatcher());

        _life.DoOnEnding(async () =>
        {
          await MainThreadExt.InvokeAsync(_c =>
          {
            var context = Android.App.Application.Context;
            var intent = new Intent(context, typeof(BackgroundService));
            intent.SetAction("STOP_SERVICE");
            context.StartService(intent);
          });
        });

        MainThread.BeginInvokeOnMainThread(() =>
        {
          var context = Android.App.Application.Context;
          var intent = new Intent(context, typeof(BackgroundService));
          intent.SetAction("START_SERVICE");
          context.StartForegroundService(intent);
        });

        var providers = (string[])(conf.LowPowerMode ?
          [Android.Locations.LocationManager.NetworkProvider, Android.Locations.LocationManager.PassiveProvider] :
          [Android.Locations.LocationManager.GpsProvider]);

        locationProvider.StartLocationWatcher(providers);
        reportQueueCounter = 0;
        reportFlow.Subscribe(_life);
      });

    p_enableFlow.OnNext(false);

    //locationProvider.ProviderDisabled
    //  .WithLatestFrom(p_enableFlow, (_providerDisabled, _enabled) => (ProviderDisabled: _providerDisabled, Enabled: _enabled))
    //  .Where(_ => _.Enabled && _.ProviderDisabled == Android.Locations.LocationManager.GpsProvider)
    //  .ToUnit()
    //  .Subscribe(_unit =>
    //  {
    //    var context = Android.App.Application.Context;
    //    var notificationMgr = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
    //    var activity = PendingIntent.GetActivity(
    //      context,
    //      0,
    //      new Intent(Android.Provider.Settings.ActionLocationSourceSettings),
    //      PendingIntentFlags.Immutable);

    //    var channelId = "LocationProviderIsDisabled";
    //    var channel = new NotificationChannel(channelId, "Notify when location provider is disabled", NotificationImportance.Max);
    //    notificationMgr.CreateNotificationChannel(channel);
    //    var notification = new Notification.Builder(context, channelId)
    //      .SetContentTitle("Location provider is disabled")
    //      .SetContentText("Please enable location provider in your phone's settings")
    //      .SetContentIntent(activity)
    //      .SetSmallIcon(Resource.Drawable.letter_r)
    //      .SetAutoCancel(true)
    //      .Build();

    //    notificationMgr.Notify(NOTIFICATION_ID_LOCATION_PROVIDER_DISABLED, notification);

    //    var page = Shell.Current.CurrentPage;
    //    if (page != null && page.IsVisible)
    //    {
    //      _ = page.DisplayAlert(
    //        "Location provider is disabled",
    //        "Please enable location provider in your phone's settings\n\nYou can click notification in the notification drawer to open location settings",
    //        "Okay");
    //    }
    //  }, _lifetime);

    //locationProvider.ProviderEnabled
    //  .Where(_ => _ == Android.Locations.LocationManager.GpsProvider)
    //  .ToUnit()
    //  .Subscribe(_unit =>
    //  {
    //    var context = Android.App.Application.Context;
    //    var notificationMgr = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
    //    notificationMgr.Cancel(NOTIFICATION_ID_LOCATION_PROVIDER_DISABLED);
    //  }, _lifetime);
  }

  public IObservable<LocationReporterSessionStats> Stats => p_statsFlow;

  public IObservable<bool> Enabled => p_enableFlow;

  public async Task<bool> IsEnabledAsync() => await p_enableFlow.FirstOrDefaultAsync();

  public void SetState(bool _enabled)
  {
    p_enableFlow.OnNext(_enabled);
    p_log.Info($"Stats reporter {(_enabled ? "enabled" : "disable")}");
  }

}
