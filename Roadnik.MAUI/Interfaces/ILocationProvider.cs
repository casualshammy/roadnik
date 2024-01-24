namespace Roadnik.MAUI.Interfaces;

public interface ILocationProvider
{
  IObservable<Location> Location { get; }
  IObservable<string> ProviderDisabled { get; }
  IObservable<string> ProviderEnabled { get; }

  void ChangeConstrains(TimeSpan _minTime, float _minDistanceMeters);
  void StopLocationWatcher();
  void StartLocationWatcher(out bool _providerEnabled);
  Task<Location?> GetCurrentBestLocationAsync(TimeSpan _timeout, CancellationToken _ct);
}