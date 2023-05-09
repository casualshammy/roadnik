using Roadnik.MAUI.Data;

namespace Roadnik.MAUI.Interfaces;

public interface ILocationReporter
{
  IObservable<LocationReporterSessionStats> Stats { get; }

  Task<bool> IsEnabledAsync();
  void SetState(bool _enabled);
}