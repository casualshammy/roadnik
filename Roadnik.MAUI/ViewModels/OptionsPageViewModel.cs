using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using static Roadnik.MAUI.Data.Consts;

namespace Roadnik.MAUI.ViewModels;

internal class OptionsPageViewModel : BaseViewModel
{
  private readonly IPreferencesStorage p_storage;
  private string? p_serverName;
  private string? p_serverKey;
  private int p_minimumTime;
  private int p_minimumDistance;
  private TrackpointReportingConditionType p_trackpointReportingCondition;
  private int p_minAccuracy;

  public OptionsPageViewModel()
  {
    p_storage = Container.Locate<IPreferencesStorage>();
    p_serverName = p_storage.GetValueOrDefault<string>(PREF_SERVER_ADDRESS);
    p_serverKey = p_storage.GetValueOrDefault<string>(PREF_SERVER_KEY);
    p_minimumTime = p_storage.GetValueOrDefault<int>(PREF_TIME_INTERVAL);
    p_minimumDistance = p_storage.GetValueOrDefault<int>(PREF_DISTANCE_INTERVAL);
    p_trackpointReportingCondition = p_storage.GetValueOrDefault<TrackpointReportingConditionType>(PREF_TRACKPOINT_REPORTING_CONDITION);
    p_minAccuracy = p_storage.GetValueOrDefault<int>(PREF_MIN_ACCURACY);
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
  public string? ServerKey
  {
    get => p_serverKey;
    set
    {
      SetProperty(ref p_serverKey, value);
      if (p_serverKey != null)
        p_storage.SetValue(PREF_SERVER_KEY, p_serverKey);
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

}
