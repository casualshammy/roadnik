namespace Roadnik.MAUI.Interfaces;

public interface ILocationReporter
{
  IObservable<Location> Location { get; }

  Task<Location?> GetCurrentAnyLocationAsync(TimeSpan _timeout, CancellationToken _ct);
  Task<Location?> GetCurrentBestLocationAsync(TimeSpan _timeout, CancellationToken _ct);
  Task<bool> IsEnabled();
  void SetState(bool _enabled);
}