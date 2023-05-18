using Roadnik.Common.Toolkit;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using System.Windows.Input;
using static Roadnik.MAUI.Data.Consts;

namespace Roadnik.MAUI.ViewModels;

internal class OptionsPageViewModel : BaseViewModel
{
  private readonly IPreferencesStorage p_storage;
  private readonly IPagesController p_pagesController;
  private string? p_serverName;
  private string? p_roomId;
  private string? p_username;
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
    p_pagesController = Container.Locate<IPagesController>();

    p_serverName = p_storage.GetValueOrDefault<string>(PREF_SERVER_ADDRESS);
    p_roomId = p_storage.GetValueOrDefault<string>(PREF_ROOM);
    p_username = p_storage.GetValueOrDefault<string>(PREF_USERNAME);
    p_minimumTime = p_storage.GetValueOrDefault<int>(PREF_TIME_INTERVAL);
    p_minimumDistance = p_storage.GetValueOrDefault<int>(PREF_DISTANCE_INTERVAL);
    p_trackpointReportingCondition = p_storage.GetValueOrDefault<TrackpointReportingConditionType>(PREF_TRACKPOINT_REPORTING_CONDITION);
    p_minAccuracy = p_storage.GetValueOrDefault<int>(PREF_MIN_ACCURACY);
    p_mapOpeningBehavior = p_storage.GetValueOrDefault<MapOpeningBehavior>(PREF_MAP_OPEN_BEHAVIOR);
    p_mapCacheEnabled = p_storage.GetValueOrDefault<bool>(PREF_MAP_CACHE_ENABLED);
    p_notificationOnNewUser = p_storage.GetValueOrDefault<bool>(PREF_NOTIFY_NEW_USER);

    ServerAddressCommand = new Command(OnServerAddressCommand);
    RoomIdCommand = new Command(OnRoomIdCommand);
    UsernameCommand = new Command(OnUsernameCommand);
    MinimumIntervalCommand = new Command(OnMinimumInterval);
    MinimumDistanceCommand = new Command(OnMinimumDistance);
    TrackpointReportingConditionCommand = new Command(OnTrackpointReportingCondition);
    MinAccuracyCommand = new Command(OnMinAccuracy);
    MapOpenBehaviorCommand = new Command(OnMapOpenBehavior);
    MapCacheCommand = new Command(OnMapCache);
    NotifyNewUserCommand = new Command(OnNotifyNewUser);
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
      if (value == null || !ReqResUtil.IsRoomIdSafe(value))
        return;

      SetProperty(ref p_roomId, value);
      if (p_roomId != null)
        p_storage.SetValue(PREF_ROOM, p_roomId);
    }
  }
  public string? Nickname
  {
    get => p_username;
    set
    {
      if (value == null)
        return;

      var v = value;
      if (!ReqResUtil.IsUserDefinedStringSafe(v))
        v = ReqResUtil.ClearUserMsg(v);

      if (!ReqResUtil.IsUserDefinedStringSafe(v))
        return;

      SetProperty(ref p_username, v);
      if (p_username != null && !string.IsNullOrWhiteSpace(p_username))
        p_storage.SetValue(PREF_USERNAME, p_username);
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
        return Resources.Strings.AppResources.page_options_mapOpenBehavior_allTracks;
      else if (p_mapOpeningBehavior == MapOpeningBehavior.LastPosition)
        return Resources.Strings.AppResources.page_options_mapOpenBehavior_lastPosition;
      else
        return Resources.Strings.AppResources.page_options_mapOpenBehavior_lastTrack;
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

  public ICommand ServerAddressCommand { get; }
  public ICommand RoomIdCommand { get; }
  public ICommand UsernameCommand { get; }
  public ICommand MinimumIntervalCommand { get; }
  public ICommand MinimumDistanceCommand { get; }
  public ICommand TrackpointReportingConditionCommand { get; }
  public ICommand MinAccuracyCommand { get; }
  public ICommand MapOpenBehaviorCommand { get; }
  public ICommand MapCacheCommand { get; }
  public ICommand NotifyNewUserCommand { get; }

  private async void OnServerAddressCommand(object _arg)
  {
    var currentPage = p_pagesController.CurrentPage;
    if (currentPage == null)
      return;

    var serverName = await currentPage.DisplayPromptAsync(
        "Server address",
        null,
        "Save",
        placeholder: "http://example.com:5544/",
        initialValue: ServerName,
        keyboard: Microsoft.Maui.Keyboard.Url);

    if (serverName == null)
      return;

    ServerName = serverName;
  }

  private async void OnRoomIdCommand(object _arg)
  {
    var currentPage = p_pagesController.CurrentPage;
    if (currentPage == null)
      return;

    var roomId = await currentPage.DisplayPromptAsync(
      "Room ID",
      $"Only alphanumeric characters and hyphens are allowed. Minimum length - {ReqResUtil.MinRoomIdLength} characters, maximum - {ReqResUtil.MaxRoomIdLength} characters",
      "Save",
      initialValue: RoomId,
      maxLength: ReqResUtil.MaxRoomIdLength);

    if (roomId == null)
      return;

    RoomId = roomId;
  }

  private async void OnUsernameCommand(object _arg)
  {
    var currentPage = p_pagesController.CurrentPage;
    if (currentPage == null)
      return;

    var usernameRaw = await currentPage.DisplayPromptAsync(
      "Username:",
      $"Empty username is not allowed. Minimum length - {ReqResUtil.MinRoomIdLength} characters, maximum - {ReqResUtil.MaxRoomIdLength} characters",
      "Save",
      initialValue: Nickname,
      maxLength: ReqResUtil.MaxRoomIdLength);

    if (usernameRaw == null)
      return;

    Nickname = usernameRaw;
  }

  private async void OnMinimumInterval(object _arg)
  {
    var currentPage = p_pagesController.CurrentPage;
    if (currentPage == null)
      return;

    var mimimalIntervalRaw = await currentPage.DisplayPromptAsync(
      "Interval in seconds:",
      "Minimum interval for anonymous user is 10 sec, for registered user is 1 sec. Maximum interval is 1 hour (3600 sec)",
      initialValue: MinimumTime.ToString(),
      keyboard: Microsoft.Maui.Keyboard.Numeric);

    if (mimimalIntervalRaw != null &&
      int.TryParse(mimimalIntervalRaw, out var mimimalInterval) &&
      mimimalInterval >= 1 &&
      mimimalInterval <= 3600)
      MinimumTime = mimimalInterval;
  }

  private async void OnMinimumDistance(object _arg)
  {
    var currentPage = p_pagesController.CurrentPage;
    if (currentPage == null)
      return;

    var mimimalDistanceRaw = await currentPage.DisplayPromptAsync(
      "Distance in metres:",
      "0 to disable limit. Maximum value - 10 km (10000 metres)",
      initialValue: MinimumDistance.ToString(),
      keyboard: Microsoft.Maui.Keyboard.Numeric);

    if (mimimalDistanceRaw != null &&
      int.TryParse(mimimalDistanceRaw, out var mimimalDistance) &&
      mimimalDistance <= 10000)
      MinimumDistance = mimimalDistance;
  }

  private async void OnTrackpointReportingCondition(object _arg)
  {
    var currentPage = p_pagesController.CurrentPage;
    if (currentPage == null)
      return;

    var and = "Time AND distance";
    var or = "Time OR distance";
    var result = await currentPage.DisplayActionSheet("Trackpoint reporting condition", null, null, and, or);
    if (result == null)
      return;

    if (result == and)
      TrackpointReportingConditionText = TrackpointReportingConditionType.TimeAndDistance.ToString();
    else if (result == or)
      TrackpointReportingConditionText = TrackpointReportingConditionType.TimeOrDistance.ToString();
  }

  private async void OnMinAccuracy(object _arg)
  {
    var currentPage = p_pagesController.CurrentPage;
    if (currentPage == null)
      return;

    var minAccuracyRaw = await currentPage.DisplayPromptAsync(
      "Accuracy in metres:",
      "Minimum value - 1 meter. Sane value is between 10 and 30 metres",
      initialValue: MinAccuracy.ToString(),
      keyboard: Microsoft.Maui.Keyboard.Numeric);

    if (minAccuracyRaw == null)
      return;
    if (!int.TryParse(minAccuracyRaw, out var minAccuracy))
      return;
    if (minAccuracy < 1)
      minAccuracy = 1;
    if (minAccuracy > 1000)
      minAccuracy = 1000;

    MinAccuracy = minAccuracy;
  }

  private async void OnMapOpenBehavior(object _arg)
  {
    var currentPage = p_pagesController.CurrentPage;
    if (currentPage == null)
      return;

    var result = await currentPage.DisplayActionSheet(
      "What to show on map opening:",
      null,
      null,
      Resources.Strings.AppResources.page_options_mapOpenBehavior_allTracks,
      Resources.Strings.AppResources.page_options_mapOpenBehavior_lastPosition,
      Resources.Strings.AppResources.page_options_mapOpenBehavior_lastTrack);

    if (result == null)
      return;

    if (result == Resources.Strings.AppResources.page_options_mapOpenBehavior_allTracks)
      MapOpenBehavior = MapOpeningBehavior.AllTracks.ToString();
    else if (result == Resources.Strings.AppResources.page_options_mapOpenBehavior_lastPosition)
      MapOpenBehavior = MapOpeningBehavior.LastPosition.ToString();
    else if (result == Resources.Strings.AppResources.page_options_mapOpenBehavior_lastTrack)
      MapOpenBehavior = MapOpeningBehavior.LastTrackedRoute.ToString();
  }

  private void OnMapCache(object? _arg)
  {
    if (_arg is not bool toggled)
      return;

    MapCacheEnabled = toggled;
  }

  private void OnNotifyNewUser(object? _arg)
  {
    if (_arg is not bool toggled)
      return;

    NotificationOnNewUser = toggled;
  }

}
