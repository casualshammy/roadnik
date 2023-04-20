using Ax.Fw.Attributes;
using Newtonsoft.Json;
using Roadnik.MAUI.Interfaces;

namespace Roadnik.MAUI.Modules.PreferencesStorage;

[ExportClass(typeof(IPreferencesStorage), Singleton: true)]
internal class PreferencesStorageImpl : IPreferencesStorage
{
  public T? GetValueOrDefault<T>(string _key) where T : notnull
  {
    var preferenceValue = Preferences.Default.Get(_key, (string?)null);
    if (preferenceValue == null)
      return default;

    var obj = JsonConvert.DeserializeObject<T>(preferenceValue);
    return obj;
  }

  public void SetValue<T>(string _key, T _value) where T : notnull
  {
    var json = JsonConvert.SerializeObject(_value);
    Preferences.Default.Set(_key, json);
  }

  public void RemoveValue(string _key)
  {
    Preferences.Default.Remove(_key);
  }

  public string INITIALIZED { get; } = "settings.initialized";
  public string SERVER_ADDRESS { get; } = "settings.network.server-address";
  public string SERVER_KEY { get; } = "settings.network.server-key";
  public string TIME_INTERVAL { get; } = "settings.report.time-interval";
  public string DISTANCE_INTERVAL { get; } = "settings.report.distance-interval";

}
