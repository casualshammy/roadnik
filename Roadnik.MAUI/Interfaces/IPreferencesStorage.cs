using System.Reactive;

namespace Roadnik.MAUI.Interfaces;

internal interface IPreferencesStorage
{
  string SERVER_ADDRESS { get; }
  string SERVER_KEY { get; }
  string TIME_INTERVAL { get; }
  string DISTANCE_INTERVAL { get; }
  string INITIALIZED { get; }
  string TRACKPOINT_REPORTING_CONDITION { get; }
  IObservable<Unit> PreferencesChanged { get; }
  string USER_MSG { get; }

  T? GetValueOrDefault<T>(string _key);
  void RemoveValue(string _key);
  void SetValue<T>(string _key, T _value);
}
