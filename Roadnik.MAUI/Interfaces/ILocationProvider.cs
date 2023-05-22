namespace Roadnik.MAUI.Interfaces;

public interface ILocationProvider
{
  IObservable<Location> Location { get; }

  void ChangeConstrains(TimeSpan _minTime, float _minDistanceMeters);
  void Disable();
  void Enable();
  Task<Location?> GetCurrentBestLocationAsync(TimeSpan _timeout, CancellationToken _ct);
}