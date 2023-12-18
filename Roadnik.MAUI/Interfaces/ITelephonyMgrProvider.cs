namespace Roadnik.MAUI.Interfaces;

public interface ITelephonyMgrProvider
{
  IObservable<double?> SignalLevel { get; }
}
