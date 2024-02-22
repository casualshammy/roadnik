namespace Roadnik.MAUI.Interfaces;

public interface ILocationProvider
{
  IObservable<Location> Location { get; }
  IObservable<string> ProviderDisabled { get; }
  IObservable<string> ProviderEnabled { get; }

  void StopLocationWatcher();
  void StartLocationWatcher(IReadOnlyList<string> _providers, out IReadOnlySet<string> _providersEnabled);
  static abstract Task<Location?> GetCurrentBestLocationAsync(TimeSpan _timeout, CancellationToken _ct);
}