using Microsoft.Maui.Controls;
using Roadnik.Common.Toolkit;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using static Roadnik.MAUI.Data.Consts;

namespace Roadnik.MAUI.ViewModels;

internal class OptionsPageViewModel : BaseViewModel
{
  private readonly IPreferencesStorage p_storage;
  private string? p_serverName;
  private string? p_roomId;
  private string? p_nickname;
  private int p_minimumTime;
  private int p_minimumDistance;
  private TrackpointReportingConditionType p_trackpointReportingCondition;
  private int p_minAccuracy;
  private MapOpeningBehavior p_mapOpeningBehavior;
  private bool p_mapCacheEnabled;
  private bool p_notificationOnNewUser;

  public OptionsPageViewModel()
  {
    p_storage = Container.Locate<IPreferencesStorage>();
    p_serverName = p_storage.GetValueOrDefault<string>(PREF_SERVER_ADDRESS);
    p_roomId = p_storage.GetValueOrDefault<string>(PREF_SERVER_KEY);
    p_nickname = p_storage.GetValueOrDefault<string>(PREF_NICKNAME);
    p_minimumTime = p_storage.GetValueOrDefault<int>(PREF_TIME_INTERVAL);
    p_minimumDistance = p_storage.GetValueOrDefault<int>(PREF_DISTANCE_INTERVAL);
    p_trackpointReportingCondition = p_storage.GetValueOrDefault<TrackpointReportingConditionType>(PREF_TRACKPOINT_REPORTING_CONDITION);
    p_minAccuracy = p_storage.GetValueOrDefault<int>(PREF_MIN_ACCURACY);
    p_mapOpeningBehavior = p_storage.GetValueOrDefault<MapOpeningBehavior>(PREF_MAP_OPEN_BEHAVIOR);
    p_mapCacheEnabled = p_storage.GetValueOrDefault<bool>(PREF_MAP_CACHE_ENABLED);
    p_notificationOnNewUser = p_storage.GetValueOrDefault<bool>(PREF_NOTIFY_NEW_USER);
  }

  public string Title { get; } = "Options";
  public string? ServerName
  {
    get => p_serverName;
    set
    {
      SetProperty(ref p_serverName, value);
      if (p_serverName != null)
        p_storage.SetValue(PREF_SERVER_ADDRESS, p_serverName);
    }
  }
  public string? RoomId
  {
    get => p_roomId;
    set
    {
      if (value == null || !ReqResUtil.IsKeySafe(value))
        return;

      SetProperty(ref p_roomId, value);
      if (p_roomId != null)
        p_storage.SetValue(PREF_SERVER_KEY, p_roomId);
    }
  }
  public string? Nickname
  {
    get => p_nickname;
    set
    {
      if (value == null)
        return;

      var v = value;
      if (!ReqResUtil.IsUserDefinedStringSafe(v))
        v = ReqResUtil.ClearUserMsg(v);

      if (!ReqResUtil.IsUserDefinedStringSafe(v))
        return;

      SetProperty(ref p_nickname, v);
      if (p_nickname != null && !string.IsNullOrWhiteSpace(p_nickname))
        p_storage.SetValue(PREF_NICKNAME, p_nickname);
    }
  }
  public int MinimumTime
  {
    get => p_minimumTime;
    set
    {
      SetProperty(ref p_minimumTime, value);
      p_storage.SetValue(PREF_TIME_INTERVAL, p_minimumTime);
    }
  }
  public int MinimumDistance
  {
    get => p_minimumDistance;
    set
    {
      SetProperty(ref p_minimumDistance, value);
      p_storage.SetValue(PREF_DISTANCE_INTERVAL, p_minimumDistance);
    }
  }
  public string TrackpointReportingConditionText
  {
    get
    {
      if (p_trackpointReportingCondition == TrackpointReportingConditionType.TimeAndDistance)
        return "Time AND Distance";
      else
        return "Time OR Distance";
    }
    set
    {
      if (!Enum.TryParse<TrackpointReportingConditionType>(value, out var condition))
        return;

      SetProperty(ref p_trackpointReportingCondition, condition);
      p_storage.SetValue(PREF_TRACKPOINT_REPORTING_CONDITION, p_trackpointReportingCondition);
    }
  }
  public int MinAccuracy
  {
    get => p_minAccuracy;
    set
    {
      SetProperty(ref p_minAccuracy, value);
      p_storage.SetValue(PREF_MIN_ACCURACY, p_minAccuracy);
    }
  }
  public string MapOpenBehavior
  {
    get
    {
      if (p_mapOpeningBehavior == MapOpeningBehavior.AllTracks)
        return "Show all tracks";
      else
        return "Show last viewed location";
    }
    set
    {
      if (!Enum.TryParse<MapOpeningBehavior>(value, out var behavior))
        return;

      SetProperty(ref p_mapOpeningBehavior, behavior);
      p_storage.SetValue(PREF_MAP_OPEN_BEHAVIOR, p_mapOpeningBehavior);
    }
  }
  public bool MapCacheEnabled
  {
    get => p_mapCacheEnabled;
    set
    {
      SetProperty(ref p_mapCacheEnabled, value);
      p_storage.SetValue(PREF_MAP_CACHE_ENABLED, p_mapCacheEnabled);
    }
  }
  public bool NotificationOnNewUser
  {
    get => p_notificationOnNewUser;
    set
    {
      SetProperty(ref p_notificationOnNewUser, value);
      p_storage.SetValue(PREF_NOTIFY_NEW_USER, p_notificationOnNewUser);
    }
  }

}
