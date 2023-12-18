using Ax.Fw;
using Ax.Fw.Attributes;
using Ax.Fw.Cache;
using Ax.Fw.Extensions;
using JustLogger.Interfaces;
using Roadnik.Common.Toolkit;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using System.Reactive;
using System.Reactive.Subjects;
using System.Text.Json;
using static Roadnik.MAUI.Data.Consts;

namespace Roadnik.MAUI.Modules.PreferencesStorage;

[ExportClass(typeof(IPreferencesStorage), Singleton: true)]
internal class PreferencesStorageImpl : IPreferencesStorage
{
  private readonly ILogger p_log;
  private readonly SyncCache<string, object?> p_cache = new(new SyncCacheSettings(100, 10, TimeSpan.FromHours(1)));
  private readonly ReplaySubject<Unit> p_prefChangedFlow = new(1);

  public PreferencesStorageImpl(ILogger _log)
  {
    p_log = _log["pref-storage"];

    SetupDefaultPreferences();
    MigratePreferences();

    p_prefChangedFlow.OnNext();
  }

  public IObservable<Unit> PreferencesChanged => p_prefChangedFlow;

  public T? GetValueOrDefault<T>(string _key)
  {
    if (p_cache.TryGet(_key, out var obj))
      return (T?)obj;

    var preferenceValue = Preferences.Default.Get(_key, (string?)null);
    if (preferenceValue == null)
      return default;

    obj = JsonSerializer.Deserialize<T>(preferenceValue);
    p_cache.Put(_key, obj);
    return (T?)obj;
  }

  public void SetValue<T>(string _key, T? _value)
  {
    var json = JsonSerializer.Serialize(_value);
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

  private void SetupDefaultPreferences()
  {
    if (GetValueOrDefault<int>(PREF_DB_VERSION) != default)
      return;

    SetValue(PREF_DB_VERSION, 1);

    SetValue(PREF_SERVER_ADDRESS, ROADNIK_APP_ADDRESS);
    SetValue(PREF_ROOM, Utilities.GetRandomString(ReqResUtil.MaxRoomIdLength, false));
    SetValue(PREF_TIME_INTERVAL, 10);
    SetValue(PREF_DISTANCE_INTERVAL, 100);
    SetValue(PREF_TRACKPOINT_REPORTING_CONDITION, TrackpointReportingConditionType.TimeAndDistance);
    SetValue(PREF_MIN_ACCURACY, 30);
    SetValue(PREF_USERNAME, $"user-{Random.Shared.Next(100, 1000)}");
    SetValue(PREF_MAP_OPEN_BEHAVIOR, MapOpeningBehavior.AllTracks);
    SetValue(PREF_NOTIFY_NEW_POINT, true);
    SetValue(PREF_NOTIFY_NEW_TRACK, true);
    SetValue(PREF_WIPE_OLD_TRACK_ON_NEW_ENABLED, true);
  }

  private void MigratePreferences()
  {
    var dbVersion = GetValueOrDefault<int>(PREF_DB_VERSION);
    if (!int.TryParse(AppInfo.Current.BuildString, out var appVersion))
    {
      p_log.Error($"Can't parse app version: '{AppInfo.Current.BuildString}'");
      return;
    }

    if (appVersion != dbVersion)
    {
      p_log.Info($"Application is updated - wiping cache...");
      var cacheDir = new DirectoryInfo(FileSystem.Current.CacheDirectory);
      foreach (var file in cacheDir.EnumerateFiles("*", SearchOption.AllDirectories))
        if (!file.TryDelete())
          p_log.Warn($"Can't delete cache file: '{file.FullName}'");

      p_log.Info($"Cache is wiped");
    }

    var migrations = GetMigrations();
    for (var i = dbVersion + 1; i <= appVersion; i++)
      if (migrations.TryGetValue(i, out var action))
      {
        p_log.Info($"Migrating db up to version -->> {i}");
        action();
        p_log.Info($"Db is migrated to version -->> {i}");
      }

    SetValue(PREF_DB_VERSION, appVersion);
  }

  private IReadOnlyDictionary<int, Action> GetMigrations()
  {
    var migrations = new Dictionary<int, Action>();

    migrations.Add(175, () =>
    {
      var roomId = GetValueOrDefault<string>(PREF_ROOM);
      if (!roomId.IsNullOrEmpty() && roomId.Length < ReqResUtil.MinRoomIdLength)
      {
        var length = ReqResUtil.MinRoomIdLength - roomId.Length;
        var newRoomId = $"{roomId}{new string('-', length)}";
        SetValue(PREF_ROOM, newRoomId);
        p_log.Info($"Migration 175: new room id: '{newRoomId}'");
      }
    });

    return migrations;
  }

}
