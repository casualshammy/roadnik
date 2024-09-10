namespace Roadnik.MAUI.Interfaces;

internal interface ICompassProvider
{
  IObservable<float?> Values { get; }
}
