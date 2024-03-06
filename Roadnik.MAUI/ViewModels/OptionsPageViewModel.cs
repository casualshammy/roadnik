using Ax.Fw;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Common.ReqRes;
using Roadnik.Common.Toolkit;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using System.Reactive.Linq;
using System.Windows.Input;
using L = Roadnik.MAUI.Resources.Strings.AppResources;
using static Roadnik.MAUI.Data.Consts;

namespace Roadnik.MAUI.ViewModels;

internal class OptionsPageViewModel : BaseViewModel
{
  private readonly IPreferencesStorage p_storage;
  private readonly IPagesController p_pagesController;
  private readonly IHttpClientProvider p_httpClient;
  private readonly ILog p_log;
  private string? p_serverName;
  private string? p_roomId;
  private string? p_username;
  private int p_minimumTime;
  private int p_minimumDistance;
  private TrackpointReportingConditionType p_trackpointReportingCondition;
  private int p_minAccuracy;
  private bool p_lowPowerModeEnabled;
  private bool p_wipeOldTrackOnNewEnabled;
  private bool p_notificationOnNewTrack;
  private bool p_notificationOnNewPoint;

  public OptionsPageViewModel()
  {
    p_storage = Container.Locate<IPreferencesStorage>();
    p_pagesController = Container.Locate<IPagesController>();
    p_httpClient = Container.Locate<IHttpClientProvider>();
    p_log = Container.Locate<ILog>()["options-page-view-model"];

    ServerAddressCommand = new Command(OnServerAddressCommand);
    RoomIdCommand = new Command(OnRoomIdCommand);
    UsernameCommand = new Command(OnUsernameCommand);
    MinimumIntervalCommand = new Command(OnMinimumInterval);
    MinimumDistanceCommand = new Command(OnMinimumDistance);
    TrackpointReportingConditionCommand = new Command(OnTrackpointReportingCondition);
    MinAccuracyCommand = new Command(OnMinAccuracy);
    LowPowerModeCommand = new Command(OnLowPowerMode);
    WipeOldTrackOnNewCommand = new Command(OnWipeOldTrackOnNew);
    NotifyNewTrackCommand = new Command(OnNotifyNewTrack);
    NotifyNewPointCommand = new Command(OnNotifyNewPoint);

    var lifetime = Container.Locate<IReadOnlyLifetime>();
    p_storage.PreferencesChanged
      .Sample(TimeSpan.FromSeconds(1))
      .StartWithDefault()
      .Subscribe(_ =>
      {
        SetProperty(ref p_serverName, p_storage.GetValueOrDefault<string>(PREF_SERVER_ADDRESS), nameof(ServerName));
        SetProperty(ref p_roomId, p_storage.GetValueOrDefault<string>(PREF_ROOM), nameof(RoomId));
        SetProperty(ref p_username, p_storage.GetValueOrDefault<string>(PREF_USERNAME), nameof(Nickname));
        SetProperty(ref p_minimumTime, p_storage.GetValueOrDefault<int>(PREF_TIME_INTERVAL), nameof(MinimumTime));
        SetProperty(ref p_minimumDistance, p_storage.GetValueOrDefault<int>(PREF_DISTANCE_INTERVAL), nameof(MinimumDistance));
        SetProperty(ref p_trackpointReportingCondition, p_storage.GetValueOrDefault<TrackpointReportingConditionType>(PREF_TRACKPOINT_REPORTING_CONDITION), nameof(TrackpointReportingConditionText));
        SetProperty(ref p_minAccuracy, p_storage.GetValueOrDefault<int>(PREF_MIN_ACCURACY), nameof(MinAccuracy));
        SetProperty(ref p_lowPowerModeEnabled, p_storage.GetValueOrDefault<bool>(PREF_LOW_POWER_MODE), nameof(LowPowerModeEnabled));
        SetProperty(ref p_wipeOldTrackOnNewEnabled, p_storage.GetValueOrDefault<bool>(PREF_WIPE_OLD_TRACK_ON_NEW_ENABLED), nameof(WipeOldTrackOnNewEnabled));
        SetProperty(ref p_notificationOnNewTrack, p_storage.GetValueOrDefault<bool>(PREF_NOTIFY_NEW_TRACK), nameof(NotificationOnNewTrack));
        SetProperty(ref p_notificationOnNewPoint, p_storage.GetValueOrDefault<bool>(PREF_NOTIFY_NEW_POINT), nameof(NotificationOnNewPoint));
      }, lifetime);
  }

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
      if (value == null || !ReqResUtil.IsRoomIdValid(value))
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
      if (!ReqResUtil.IsUsernameSafe(value))
        return;

      SetProperty(ref p_username, value);
      p_storage.SetValue(PREF_USERNAME, value);
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

  public bool LowPowerModeEnabled
  {
    get => p_lowPowerModeEnabled;
    set
    {
      SetProperty(ref p_lowPowerModeEnabled, value);
      p_storage.SetValue(PREF_LOW_POWER_MODE, p_lowPowerModeEnabled);
    }
  }

  public bool WipeOldTrackOnNewEnabled
  {
    get => p_wipeOldTrackOnNewEnabled;
    set
    {
      SetProperty(ref p_wipeOldTrackOnNewEnabled, value);
      p_storage.SetValue(PREF_WIPE_OLD_TRACK_ON_NEW_ENABLED, p_wipeOldTrackOnNewEnabled);
    }
  }

  public bool NotificationOnNewTrack
  {
    get => p_notificationOnNewTrack;
    set
    {
      SetProperty(ref p_notificationOnNewTrack, value);
      p_storage.SetValue(PREF_NOTIFY_NEW_TRACK, p_notificationOnNewTrack);
    }
  }
  public bool NotificationOnNewPoint
  {
    get => p_notificationOnNewPoint;
    set
    {
      SetProperty(ref p_notificationOnNewPoint, value);
      p_storage.SetValue(PREF_NOTIFY_NEW_POINT, p_notificationOnNewPoint);
    }
  }

  public ICommand ServerAddressCommand { get; }
  public ICommand RoomIdCommand { get; }
  public ICommand UsernameCommand { get; }
  public ICommand MinimumIntervalCommand { get; }
  public ICommand MinimumDistanceCommand { get; }
  public ICommand TrackpointReportingConditionCommand { get; }
  public ICommand MinAccuracyCommand { get; }
  public ICommand LowPowerModeCommand { get; }
  public ICommand WipeOldTrackOnNewCommand { get; }
  public ICommand NotifyNewTrackCommand { get; }
  public ICommand NotifyNewPointCommand { get; }

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
        keyboard: Keyboard.Url);

    if (serverName == null)
      return;

    ServerName = serverName;
  }

  private async void OnRoomIdCommand(object _arg)
  {
    var currentPage = p_pagesController.CurrentPage;
    if (currentPage == null)
      return;

    var modeEdit = "Edit value manually";
    var modeGenerate = "Generate new random id";
    var mode = await currentPage.DisplayActionSheet("What would you like to do?", "Cancel", null, modeEdit, modeGenerate);
    if (mode == null)
      return;

    string? roomId = null;
    if (mode == modeEdit)
    {
      roomId = await currentPage.DisplayPromptAsync(
        "Room ID",
        $"Only alphanumeric characters and hyphens are allowed. Minimum length: {ReqResUtil.MinRoomIdLength}, maximum: {ReqResUtil.MaxRoomIdLength}",
        "Save",
        initialValue: RoomId,
        maxLength: ReqResUtil.MaxRoomIdLength);
    }
    else if (mode == modeGenerate)
    {
      var serverAddress = p_storage.GetValueOrDefault<string>(PREF_SERVER_ADDRESS);
      if (!serverAddress.IsNullOrWhiteSpace())
      {
        var url = $"{serverAddress.TrimEnd('/')}{ReqPaths.GET_FREE_ROOM_ID}";
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        try
        {
          roomId = await p_httpClient.Value.GetStringAsync(url, cts.Token);
        }
        catch (Exception ex)
        {
          p_log.Error($"Can't get free room id from server", ex);
        }
      }
      if (roomId.IsNullOrEmpty())
        roomId = Utilities.GetRandomString(ReqResUtil.MaxRoomIdLength, false);
    }

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
      $"Minimum length - {ReqResUtil.MinUsernameLength}, maximum - {ReqResUtil.MaxUsernameLength}.\n" +
      $"Allowed characters: alphanumeric plus \\-_@#$",
      "Save",
      initialValue: Nickname,
      maxLength: ReqResUtil.MaxUsernameLength);

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
      "Minimum interval is 1 sec. Maximum interval is 1 hour (3600 sec).\n" +
      "Pay attention: minimum interval may be restricted by the server",
      initialValue: MinimumTime.ToString(),
      keyboard: Keyboard.Telephone);

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
      keyboard: Keyboard.Telephone);

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
      "Minimum value - 1 meter. Sane value is between 5 and 30 metres",
      initialValue: MinAccuracy.ToString(),
      keyboard: Keyboard.Telephone);

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

  private async void OnLowPowerMode(object? _arg)
  {
    if (_arg is not bool toggled)
      return;

    LowPowerModeEnabled = toggled;

    var currentPage = p_pagesController.CurrentPage;
    if (currentPage != null && toggled)
    {
      var body = L.page_options_low_power_mode_accuracy_warning
        .Replace("%min-location-accuracy", L.page_options_tracking_required_accuracy);

      await currentPage.DisplayAlert(
      L.page_options_low_power_mode_accuracy_warning_title,
      body,
      "OK");
    }
  }

  private void OnWipeOldTrackOnNew(object? _arg)
  {
    if (_arg is not bool toggled)
      return;

    WipeOldTrackOnNewEnabled = toggled;
  }

  private void OnNotifyNewTrack(object? _arg)
  {
    if (_arg is not bool toggled)
      return;

    NotificationOnNewTrack = toggled;
  }

  private void OnNotifyNewPoint(object? _arg)
  {
    if (_arg is not bool toggled)
      return;

    NotificationOnNewPoint = toggled;
  }

}
