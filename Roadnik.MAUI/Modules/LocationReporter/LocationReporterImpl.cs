using Android.App;
using Android.Content;
using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.Pools;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Common.ReqRes;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.Platforms.Android.Services;
using System.Net.Http.Json;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using static Roadnik.MAUI.Data.Consts;

namespace Roadnik.MAUI.Modules.LocationReporter;

internal class LocationReporterImpl : ILocationReporter, IAppModule<ILocationReporter>
{
  public static ILocationReporter ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance(
      (IReadOnlyLifetime _lifetime,
      ILogger _log,
      IPreferencesStorage _storage,
      IHttpClientProvider _httpClientProvider,
      ILocationProvider _locationProvider,
      ITelephonyMgrProvider _telephonyMgrProvider)
      => new LocationReporterImpl(_lifetime, _log, _storage, _httpClientProvider, _locationProvider, _telephonyMgrProvider));
  }

  record ReportingCtx(Location? Location, DateTimeOffset? LastTimeReported);

  private readonly ReplaySubject<LocationReporterSessionStats> p_statsFlow = new(1);
  private readonly ReplaySubject<bool> p_enableFlow = new(1);
  private readonly ILogger p_log;
  private readonly IPreferencesStorage p_storage;
  private readonly IHttpClientProvider p_httpClientProvider;

  private LocationReporterImpl(
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

        _life.DoOnEnding(() =>
        {
          stats = LocationReporterSessionStats.Empty;
          p_statsFlow.OnNext(stats);
        });
        _life.DoOnEnding(_locationProvider.StopLocationWatcher);

        _life.DoOnEnding(async () =>
        {
          await MainThread.InvokeOnMainThreadAsync(() =>
          {
            var context = Android.App.Application.Context;
            var intent = new Intent(context, typeof(BackgroundService));
            intent.SetAction("STOP_SERVICE");
            context.StartService(intent);
          });
        });

        MainThread.InvokeOnMainThreadAsync(() =>
        {
          var context = Android.App.Application.Context;
          var intent = new Intent(context, typeof(BackgroundService));
          intent.SetAction("START_SERVICE");
          context.StartForegroundService(intent);
        });

        _ = Task.Run(async () => await ReportStartNewPathAsync(_life.Token));
        _locationProvider.StartLocationWatcher(out var locProviderEnabled);
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

    _locationProvider.ProviderDisabled
      .WithLatestFrom(p_enableFlow, (_providerDisabled, _enabled) => (ProviderDisabled: _providerDisabled, Enabled: _enabled))
      .Where(_ => _.Enabled && _.ProviderDisabled == Android.Locations.LocationManager.GpsProvider)
      .ToUnit()
      .Subscribe(_unit =>
      {
        var context = Android.App.Application.Context;
        var notificationMgr = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
        var activity = PendingIntent.GetActivity(
          context,
          0,
          new Intent(Android.Provider.Settings.ActionLocationSourceSettings),
          PendingIntentFlags.Immutable);

        var channelId = "LocationProviderIsDisabled";
        var channel = new NotificationChannel(channelId, "Notify when location provider is disabled", NotificationImportance.Max);
        notificationMgr.CreateNotificationChannel(channel);
        var notification = new Notification.Builder(context, channelId)
          .SetContentTitle("Location provider is disabled")
          .SetContentText("Please enable location provider in your phone's settings")
          .SetContentIntent(activity)
          .SetSmallIcon(Resource.Drawable.letter_r)
          .SetAutoCancel(true)
          .Build();

        notificationMgr.Notify(NOTIFICATION_ID_LOCATION_PROVIDER_DISABLED, notification);

        var page = Shell.Current.CurrentPage;
        if (page != null && page.IsVisible)
        {
          _ = page.DisplayAlert(
            "Location provider is disabled",
            "Please enable location provider in your phone's settings\n\nYou can click notification in the notification drawer to open location settings",
            "Okay");
        }
      }, _lifetime);

    _locationProvider.ProviderEnabled
      .Where(_ => _ == Android.Locations.LocationManager.GpsProvider)
      .ToUnit()
      .Subscribe(_unit =>
      {
        var context = Android.App.Application.Context;
        var notificationMgr = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
        notificationMgr.Cancel(NOTIFICATION_ID_LOCATION_PROVIDER_DISABLED);
      }, _lifetime);
  }

  public IObservable<LocationReporterSessionStats> Stats => p_statsFlow;

  public IObservable<bool> Enabled => p_enableFlow;

  public async Task<bool> IsEnabledAsync() => await p_enableFlow.FirstOrDefaultAsync();

  public void SetState(bool _enabled)
  {
    p_enableFlow.OnNext(_enabled);
    p_log.Info($"Stats reporter {(_enabled ? "enabled" : "disable")}");
  }

  private async Task ReportStartNewPathAsync(CancellationToken _ct = default)
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
