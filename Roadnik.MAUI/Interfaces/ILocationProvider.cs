namespace Roadnik.MAUI.Interfaces;

public interface ILocationProvider
{
  IObservable<Location> Location { get; }
  IObservable<string> ProviderDisabled { get; }
  IObservable<string> ProviderEnabled { get; }

  void StopLocationWatcher(string _clientId);
  void StartLocationWatcher(string _clientId, out bool _providerEnabled);
  Task<Location?> GetCurrentBestLocationAsync(TimeSpan _timeout, CancellationToken _ct);
}