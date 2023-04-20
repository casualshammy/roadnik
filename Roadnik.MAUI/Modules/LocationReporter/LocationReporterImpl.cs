using Ax.Fw;
using Ax.Fw.Attributes;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using JustLogger.Interfaces;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.ViewModels;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Roadnik.MAUI.Modules.LocationReporter;

[ExportClass(typeof(ILocationReporter), Singleton: true, ActivateOnStart: true)]
internal class LocationReporterImpl : ILocationReporter
{
  record ForceReqData(DateTimeOffset DateTime, bool Ok);

  private readonly HttpClient p_httpClient = new();
  private readonly ReplaySubject<Location> p_locationFlow = new(1);
  private readonly Subject<Unit> p_forceReload = new();
  private readonly ILogger p_log;
  private volatile bool p_enabled = false;

  public LocationReporterImpl(
    IReadOnlyLifetime _lifetime,
    ILogger _log,
    IPreferencesStorage _storage)
  {
    p_log = _log["location-reporter"];

    _lifetime.DisposeOnCompleted(Pool<EventLoopScheduler>.Get(out var scheduler));

    var forceReqFlow = p_forceReload
      .Scan(new ForceReqData(DateTimeOffset.MinValue, true), (_acc, _entry) =>
      {
        var now = DateTimeOffset.UtcNow;
        if (now - _acc.DateTime < TimeSpan.FromSeconds(10))
          return _acc with { Ok = false };

        return new ForceReqData(now, true);
      })
      .Where(_ => _.Ok)
      .ToUnit();

    Observable
      .Interval(TimeSpan.FromSeconds(10))
      .Where(_ => p_enabled)
      .ToUnit()
      .Merge(forceReqFlow)
      .ObserveOn(scheduler)
      .SelectAsync(async (_, _ct) =>
      {
        try
        {
          var serverAddress = _storage.GetValueOrDefault<string>(_storage.SERVER_ADDRESS);
          var serverKey = _storage.GetValueOrDefault<string>(_storage.SERVER_KEY);
          if (string.IsNullOrWhiteSpace(serverAddress) || string.IsNullOrWhiteSpace(serverKey))
            return;

          var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
          var location = await Geolocation.GetLocationAsync(request, _ct);

          if (location != null)
          {
            p_locationFlow.OnNext(location);

            var url =
              $"{serverAddress.TrimEnd('/')}/store?" +
              $"key={serverKey}&" +
              $"lat={location.Latitude}&" +
              $"lon={location.Longitude}&" +
              $"alt={location.Altitude ?? 0}&" +
              $"speed={location.Speed ?? 0}&" +
              $"acc={location.Accuracy ?? 100}&" +
              $"bearing={location.Course ?? 0}";

            await p_httpClient.GetAsync(url, _ct);
          }
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
      }, scheduler)
      .Subscribe(_lifetime);
    
  }

  public IObservable<Location> Location => p_locationFlow;
  public bool Enabled => p_enabled;

  public void SetState(bool _enabled)
  {
    p_enabled = _enabled;
    if (_enabled)
      p_forceReload.OnNext();
  }
}
