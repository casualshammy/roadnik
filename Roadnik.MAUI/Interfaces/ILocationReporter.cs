using Roadnik.MAUI.Data;

namespace Roadnik.MAUI.Interfaces;

public interface ILocationReporter
{
  IObservable<LocationReporterSessionStats> Stats { get; }
  IObservable<bool> Enabled { get; }

  Task<bool> IsEnabledAsync();
  void SetState(bool _enabled);
}