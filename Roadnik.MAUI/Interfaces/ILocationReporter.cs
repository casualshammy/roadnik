namespace Roadnik.MAUI.Interfaces;

internal interface ILocationReporter
{
  IObservable<Location> Location { get; }
  bool Enabled { get; }

  void SetState(bool _enabled);
}