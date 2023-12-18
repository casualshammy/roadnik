using Roadnik.MAUI.Data;

namespace Roadnik.MAUI.Interfaces;

public interface ILocationReporter
{
  IObservable<LocationReporterSessionStats> Stats { get; }

  Task<bool> IsEnabledAsync();
  Task ReportStartNewPathAsync(CancellationToken _ct = default);
  void SetState(bool _enabled);
}