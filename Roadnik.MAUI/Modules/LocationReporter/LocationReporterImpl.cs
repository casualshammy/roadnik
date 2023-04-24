using Ax.Fw;
using Ax.Fw.Attributes;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using JustLogger.Interfaces;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Roadnik.MAUI.Modules.LocationReporter;

[ExportClass(typeof(ILocationReporter), Singleton: true, ActivateOnStart: true)]
internal class LocationReporterImpl : ILocationReporter
{
  record ForceReqData(DateTimeOffset DateTime, bool Ok);
  record ReportingCtx(Location? Location, DateTimeOffset? LastTimeReported);

  private readonly ReplaySubject<Location> p_locationFlow = new(1);
  private readonly Subject<Unit> p_forceReload = new();
  private readonly ILogger p_log;
  private volatile bool p_enabled = false;

  public LocationReporterImpl(
    IReadOnlyLifetime _lifetime,
    ILogger _log,
    IPreferencesStorage _storage,
    IHttpClientProvider _httpClientProvider)
  {
    p_log = _log["location-reporter"];

    var prefsProp = _storage.PreferencesChanged
      .Select(_ => new
      {
        ServerAddress = _storage.GetValueOrDefault<string>(_storage.SERVER_ADDRESS),
        ServerKey = _storage.GetValueOrDefault<string>(_storage.SERVER_KEY),
        TimeInterval = TimeSpan.FromSeconds(_storage.GetValueOrDefault<int>(_storage.TIME_INTERVAL)),
        DistanceInterval = _storage.GetValueOrDefault<int>(_storage.DISTANCE_INTERVAL),
        ReportingCondition = _storage.GetValueOrDefault<TrackpointReportingConditionType>(_storage.TRACKPOINT_REPORTING_CONDITION)
      })
      .ToProperty(_lifetime);

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

    Observable
      .Interval(TimeSpan.FromSeconds(1.01), scheduler)
      .Where(_ => p_enabled)
      .ToUnit()
      .Merge(forceReqFlow)
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
        var now = DateTimeOffset.UtcNow;
        try
        {
          var prefs = prefsProp.Value;
          if (string.IsNullOrWhiteSpace(prefs?.ServerAddress) || string.IsNullOrWhiteSpace(prefs.ServerKey))
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

          var url =
            $"{prefs.ServerAddress.TrimEnd('/')}/store?" +
            $"key={prefs.ServerKey}&" +
            $"lat={location.Latitude}&" +
            $"lon={location.Longitude}&" +
            $"alt={location.Altitude ?? 0}&" +
            $"speed={location.Speed ?? 0}&" +
            $"acc={location.Accuracy ?? 100}&" +
            $"bearing={location.Course ?? 0}";

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
      .Do(_ => Interlocked.Decrement(ref counter))
      .Subscribe(_lifetime);

  }

  public IObservable<Location> Location => p_locationFlow;
  public bool Enabled => p_enabled;

  public void SetState(bool _enabled)
  {
    p_enabled = _enabled;
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

}
