using Ax.Fw.Attributes;
using Ax.Fw.Cache;
using Ax.Fw.Extensions;
using Newtonsoft.Json;
using Roadnik.MAUI.Interfaces;
using System.Reactive;
using System.Reactive.Subjects;

namespace Roadnik.MAUI.Modules.PreferencesStorage;

[ExportClass(typeof(IPreferencesStorage), Singleton: true)]
internal class PreferencesStorageImpl : IPreferencesStorage
{
  private readonly SyncCache<string, object> p_cache = new(new SyncCacheSettings(100, 10, TimeSpan.FromHours(1)));
  private readonly ReplaySubject<Unit> p_prefChangedFlow = new(1);

  public PreferencesStorageImpl()
  {
    p_prefChangedFlow.OnNext();
  }

  public IObservable<Unit> PreferencesChanged => p_prefChangedFlow;

  public T? GetValueOrDefault<T>(string _key) where T : notnull
  {
    if (p_cache.TryGet(_key, out var obj))
      return (T?)obj;

    var preferenceValue = Preferences.Default.Get(_key, (string?)null);
    if (preferenceValue == null)
      return default;

    obj = JsonConvert.DeserializeObject<T>(preferenceValue);
    p_cache.Put(_key, obj);
    return (T?)obj;
  }

  public void SetValue<T>(string _key, T _value) where T : notnull
  {
    var json = JsonConvert.SerializeObject(_value);
    Preferences.Default.Set(_key, json);
    p_cache.Put(_key, _value);
    p_prefChangedFlow.OnNext();
  }

  public void RemoveValue(string _key)
  {
    Preferences.Default.Remove(_key);
    p_cache.TryRemove(_key, out _);
    p_prefChangedFlow.OnNext();
  }

  public string INITIALIZED { get; } = "settings.initialized";
  public string SERVER_ADDRESS { get; } = "settings.network.server-address";
  public string SERVER_KEY { get; } = "settings.network.server-key";
  public string TIME_INTERVAL { get; } = "settings.report.time-interval";
  public string DISTANCE_INTERVAL { get; } = "settings.report.distance-interval";
  public string TRACKPOINT_REPORTING_CONDITION { get; } = "settings.report.trackpoint-reporting-condition";

}
