namespace Roadnik.MAUI.Interfaces;

internal interface IPreferencesStorage
{
  string SERVER_ADDRESS { get; }
  string SERVER_KEY { get; }
  string TIME_INTERVAL { get; }
  string DISTANCE_INTERVAL { get; }
  string INITIALIZED { get; }

  T? GetValueOrDefault<T>(string _key) where T : notnull;
    void RemoveValue(string _key);
    void SetValue<T>(string _key, T _value) where T : notnull;
}
