namespace Roadnik.MAUI.Interfaces;

public interface ILocationProvider
{
  IObservable<Location> Location { get; }
  IObservable<Location> FilteredLocation { get; }

  void ChangeConstrains(TimeSpan _minTime, float _minDistanceMeters);
  void Disable();
  void Enable();
}