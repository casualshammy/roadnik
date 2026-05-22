using Ax.Fw;
using Ax.Fw.Cache;
using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Common.Toolkit;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Data.LocationProvider;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.JsonCtx;
using System.Reactive;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using static Roadnik.MAUI.Data.AppConsts;

namespace Roadnik.MAUI.Modules.PreferencesStorage;

internal class PreferencesStorageImpl : IPreferencesStorage, IAppModule<IPreferencesStorage>
{
  public static IPreferencesStorage ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance((ILog _log) => new PreferencesStorageImpl(_log));
  }

  private readonly ILog p_log;
  private readonly SyncCache<string, object?> p_cache = new(new SyncCacheSettings(100, 10, TimeSpan.FromHours(1)));
  private readonly ReplaySubject<Unit> p_prefChangedFlow = new(1);

  private PreferencesStorageImpl(ILog _log)
  {
    p_log = _log["pref-storage"];

    SetupDefaultPreferences();
    MigratePreferences();

    p_prefChangedFlow.OnNext();
  }

  public IObservable<Unit> PreferencesChanged => p_prefChangedFlow;

  public T? GetValueOrDefault<T>(string _key, JsonTypeInfo<T> _jsonTypeInfo)
  {
    if (p_cache.TryGet(_key, out var obj))
      return (T?)obj;

    var preferenceValue = Preferences.Default.Get(_key, (string?)null);
    if (preferenceValue == null)
      return default;

    obj = JsonSerializer.Deserialize(preferenceValue, _jsonTypeInfo);
    p_cache.Put(_key, obj);
    return (T?)obj;
  }

  public void SetValue<T>(string _key, T _value, JsonTypeInfo<T> _jsonTypeInfo)
  {
    var json = JsonSerializer.Serialize(_value, _jsonTypeInfo);
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
    if (GetValueOrDefault(PREF_DB_VERSION, PrefsStorageJsonCtx.Default.Int32) != default)
      return;

    SetValue(PREF_DB_VERSION, 1, PrefsStorageJsonCtx.Default.Int32);

    SetValue(PREF_ROOM, CommonUtilities.GetRandomString(ReqResUtil.MaxRoomIdLength, false), PrefsStorageJsonCtx.Default.String);
    SetValue(PREF_TIME_INTERVAL, 10, PrefsStorageJsonCtx.Default.Int32);
    SetValue(PREF_DISTANCE_INTERVAL, 100, PrefsStorageJsonCtx.Default.Int32);
    SetValue(PREF_TRACKPOINT_REPORTING_CONDITION, TrackpointReportingConditionType.TimeAndDistance, PrefsStorageJsonCtx.Default.TrackpointReportingConditionType);
    SetValue(PREF_MIN_ACCURACY, 20, PrefsStorageJsonCtx.Default.Int32);
    SetValue(PREF_USERNAME, $"user-{Random.Shared.Next(100, 1000)}", PrefsStorageJsonCtx.Default.String);
    SetValue(PREF_NOTIFY_NEW_POINT, true, PrefsStorageJsonCtx.Default.Boolean);
    SetValue(PREF_NOTIFY_NEW_TRACK, true, PrefsStorageJsonCtx.Default.Boolean);
    SetValue(PREF_WIPE_OLD_TRACK_ON_NEW_ENABLED, true, PrefsStorageJsonCtx.Default.Boolean);
    SetValue(PREF_LOCATION_PROVIDERS, LocationProviders.All, PrefsStorageJsonCtx.Default.LocationProviders);
    SetValue(PREF_BLE_HRM_ENABLED, false, PrefsStorageJsonCtx.Default.Boolean);
    SetValue(PREF_BLE_HRM_DEVICE_INFO, null, PrefsStorageJsonCtx.Default.HrmDeviceInfo);
    SetValue(PREF_APP_INSTALLATION_ID, Guid.NewGuid(), PrefsStorageJsonCtx.Default.Guid);
    SetValue(PREF_DISPLAY_ON_LOCK_SCREEN, false, PrefsStorageJsonCtx.Default.Boolean);
  }

  private void MigratePreferences()
  {
    var dbVersion = GetValueOrDefault(PREF_DB_VERSION, PrefsStorageJsonCtx.Default.Int32);
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

    SetValue(PREF_DB_VERSION, appVersion, PrefsStorageJsonCtx.Default.Int32);
  }

  private IReadOnlyDictionary<int, Action> GetMigrations()
  {
    var migrations = new Dictionary<int, Action>();

    var appId = GetValueOrDefault(PREF_APP_INSTALLATION_ID, PrefsStorageJsonCtx.Default.Guid);
    if (appId == default)
    {
      appId = Guid.NewGuid();
      SetValue(PREF_APP_INSTALLATION_ID, appId, PrefsStorageJsonCtx.Default.Guid);
      p_log.Info($"New app installation id: '{appId}'");
    }

    migrations.Add(175, () =>
    {
      var roomId = GetValueOrDefault(PREF_ROOM, PrefsStorageJsonCtx.Default.String);
      if (!roomId.IsNullOrEmpty() && roomId.Length < ReqResUtil.MinRoomIdLength)
      {
        var length = ReqResUtil.MinRoomIdLength - roomId.Length;
        var newRoomId = $"{roomId}{new string('-', length)}";
        SetValue(PREF_ROOM, newRoomId, PrefsStorageJsonCtx.Default.String);
        p_log.Info($"Migration 175: new room id: '{newRoomId}'");
      }
    });
    migrations.Add(192, () =>
    {
      var reportingCondition = GetValueOrDefault(PREF_TRACKPOINT_REPORTING_CONDITION, PrefsStorageJsonCtx.Default.Int32);
      if (reportingCondition == default)
        SetValue(PREF_TRACKPOINT_REPORTING_CONDITION, TrackpointReportingConditionType.TimeAndDistance, PrefsStorageJsonCtx.Default.TrackpointReportingConditionType);
    });
    migrations.Add(270, () =>
    {
      RemoveValue("settings.report.low-power-mode");
    });
    migrations.Add(272, () =>
    {
      RemoveValue("settings.report.power-mode");
    });
    migrations.Add(351, () =>
    {
      RemoveValue("settings.report.location-provider"); // PREF_LOCATION_PROVIDER
      SetValue(PREF_LOCATION_PROVIDERS, LocationProviders.All, PrefsStorageJsonCtx.Default.LocationProviders);
    });

    return migrations;
  }

}
