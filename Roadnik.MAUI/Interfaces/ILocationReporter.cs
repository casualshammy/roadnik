using Roadnik.MAUI.Data;

namespace Roadnik.MAUI.Interfaces;

public interface ILocationReporter
{
  IObservable<LocationReporterSessionStats> Stats { get; }

  Task<bool> IsEnabled();
  void SetState(bool _enabled);
}