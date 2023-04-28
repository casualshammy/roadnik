using Roadnik.MAUI.Modules.LocationReporter;

namespace Roadnik.MAUI.Interfaces;

public interface ILocationReporter
{
  IObservable<LocationReporterSessionStats> Stats { get; }

  Task<Location?> GetCurrentAnyLocationAsync(TimeSpan _timeout, CancellationToken _ct);
  Task<Location?> GetCurrentBestLocationAsync(TimeSpan _timeout, CancellationToken _ct);
  Task<bool> IsEnabled();
  void SetState(bool _enabled);
}