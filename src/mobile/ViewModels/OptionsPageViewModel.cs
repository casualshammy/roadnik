using Ax.Fw;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Common.ReqRes;
using Roadnik.Common.Toolkit;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Data.LocationProvider;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.JsonCtx;
using Roadnik.MAUI.Pages;
using System.Reactive.Linq;
using System.Text.Json;
using System.Windows.Input;
using static Roadnik.MAUI.Data.AppConsts;
using L = Roadnik.MAUI.Resources.Strings.AppResources;

namespace Roadnik.MAUI.ViewModels;

internal class OptionsPageViewModel : BaseViewModel
{
  private readonly IPreferencesStorage p_storage;
  private readonly IPagesController p_pagesController;
  private readonly IHttpClientProvider p_httpClient;
  private readonly IDiscordIntegration p_discord;
  private readonly ILog p_log;
  private string? p_roomId;
  private string? p_username;
  private int p_minimumTime;
  private int p_minimumDistance;
  private TrackpointReportingConditionType p_trackpointReportingCondition;
  private int p_minAccuracy;
  private LocationProviders p_locationProviders;
  private bool p_wipeOldTrackOnNewEnabled;
  private bool p_notificationOnNewTrack;
  private bool p_notificationOnNewPoint;
  private bool p_bleHrmEnabled;
  private HrmDeviceInfo? p_bleHrmDeviceInfo;
  private bool p_displayOnLockScreenEnabled;
  private bool p_discordEnabled;
  private bool p_discordTokenExist;
  private string? p_discordCustomStatus;

  public OptionsPageViewModel()
  {
    p_storage = Container.Locate<IPreferencesStorage>();
    p_pagesController = Container.Locate<IPagesController>();
    p_httpClient = Container.Locate<IHttpClientProvider>();
    p_discord = Container.Locate<IDiscordIntegration>();
    p_log = Container.Locate<ILog>()["options-page-view-model"];

    RoomIdCommand = new Command(OnRoomIdCommand);
    UsernameCommand = new Command(OnUsernameCommand);
    MinimumIntervalCommand = new Command(OnMinimumInterval);
    MinimumDistanceCommand = new Command(OnMinimumDistance);
    TrackpointReportingConditionCommand = new Command(OnTrackpointReportingCondition);
    MinAccuracyCommand = new Command(OnMinAccuracy);
    DiscordAuthCommand = new Command(OnDiscordAuth);
    DiscordRevokeCommand = new Command(OnDiscordRevoke);
    DiscordStatusCommand = new Command(OnDiscordStatus);

    var lifetime = Container.Locate<IReadOnlyLifetime>();
    p_storage.PreferencesChanged
      //.Sample(TimeSpan.FromSeconds(1))
      //.StartWithDefault()
      .Subscribe(_ =>
      {
        MainThread.BeginInvokeOnMainThread(() =>
        {
          SetProperty(ref p_roomId, p_storage.GetValueOrDefault(PREF_ROOM, PrefsStorageJsonCtx.Default.String), nameof(RoomId));
          SetProperty(ref p_username, p_storage.GetValueOrDefault(PREF_USERNAME, PrefsStorageJsonCtx.Default.String), nameof(Nickname));
          SetProperty(ref p_minimumTime, p_storage.GetValueOrDefault(PREF_TIME_INTERVAL, PrefsStorageJsonCtx.Default.Int32), nameof(MinimumTime));
          SetProperty(ref p_minimumDistance, p_storage.GetValueOrDefault(PREF_DISTANCE_INTERVAL, PrefsStorageJsonCtx.Default.Int32), nameof(MinimumDistance));
          SetProperty(ref p_trackpointReportingCondition, p_storage.GetValueOrDefault(PREF_TRACKPOINT_REPORTING_CONDITION, PrefsStorageJsonCtx.Default.TrackpointReportingConditionType), nameof(TrackpointReportingConditionText));
          SetProperty(ref p_minAccuracy, p_storage.GetValueOrDefault(PREF_MIN_ACCURACY, PrefsStorageJsonCtx.Default.Int32), nameof(MinAccuracy));
          SetProperty(ref p_wipeOldTrackOnNewEnabled, p_storage.GetValueOrDefault(PREF_WIPE_OLD_TRACK_ON_NEW_ENABLED, PrefsStorageJsonCtx.Default.Boolean), nameof(WipeOldTrackOnNewEnabled));
          SetProperty(ref p_locationProviders, p_storage.GetValueOrDefault(PREF_LOCATION_PROVIDERS, PrefsStorageJsonCtx.Default.LocationProviders),
            nameof(LocationProviderGpsEnabled),
            nameof(LocationProviderNetworkEnabled),
            nameof(LocationProviderPassiveEnabled));

          SetProperty(ref p_notificationOnNewTrack, p_storage.GetValueOrDefault(PREF_NOTIFY_NEW_TRACK, PrefsStorageJsonCtx.Default.Boolean), nameof(NotificationOnNewTrack));
          SetProperty(ref p_notificationOnNewPoint, p_storage.GetValueOrDefault(PREF_NOTIFY_NEW_POINT, PrefsStorageJsonCtx.Default.Boolean), nameof(NotificationOnNewPoint));
          SetProperty(ref p_bleHrmEnabled, p_storage.GetValueOrDefault(PREF_BLE_HRM_ENABLED, PrefsStorageJsonCtx.Default.Boolean), nameof(BleHrmEnabled));
          SetProperty(ref p_bleHrmDeviceInfo, p_storage.GetValueOrDefault(PREF_BLE_HRM_DEVICE_INFO, PrefsStorageJsonCtx.Default.HrmDeviceInfo), nameof(BleHrmDeviceGuid), nameof(BleHrmDeviceName));
          SetProperty(ref p_displayOnLockScreenEnabled, p_storage.GetValueOrDefault(PREF_DISPLAY_ON_LOCK_SCREEN, PrefsStorageJsonCtx.Default.Boolean), nameof(DisplayOnLockScreenEnabled));
          SetProperty(ref p_discordTokenExist, !p_storage.GetValueOrDefault(PREF_DISCORD_TOKEN, PrefsStorageJsonCtx.Default.String).IsNullOrEmpty(), nameof(DiscordAuthenticated), nameof(DiscordNotAuthenticated));
          SetProperty(ref p_discordEnabled, p_storage.GetValueOrDefault(PREF_DISCORD_ENABLED, PrefsStorageJsonCtx.Default.Boolean), nameof(DiscordEnabled));
          SetProperty(ref p_discordCustomStatus, p_storage.GetValueOrDefault(PREF_DISCORD_STATUS, PrefsStorageJsonCtx.Default.String), nameof(DiscordCustomStatus));
        });
      }, lifetime);
  }

  public string? RoomId
  {
    get => p_roomId;
    set
    {
      if (value == null || !ReqResUtil.IsRoomIdValid(value))
        return;

      p_storage.SetValue(PREF_ROOM, value, PrefsStorageJsonCtx.Default.String);
    }
  }
  public string? Nickname
  {
    get => p_username;
    set
    {
      if (!ReqResUtil.IsUsernameSafe(value))
        return;

      p_storage.SetValue(PREF_USERNAME, value, PrefsStorageJsonCtx.Default.String);
    }
  }
  public int MinimumTime
  {
    get => p_minimumTime;
    set
    {
      p_storage.SetValue(PREF_TIME_INTERVAL, value, PrefsStorageJsonCtx.Default.Int32);
    }
  }
  public int MinimumDistance
  {
    get => p_minimumDistance;
    set
    {
      p_storage.SetValue(PREF_DISTANCE_INTERVAL, value, PrefsStorageJsonCtx.Default.Int32);
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

      p_storage.SetValue(PREF_TRACKPOINT_REPORTING_CONDITION, condition, PrefsStorageJsonCtx.Default.TrackpointReportingConditionType);
    }
  }
  public int MinAccuracy
  {
    get => p_minAccuracy;
    set
    {
      p_storage.SetValue(PREF_MIN_ACCURACY, value, PrefsStorageJsonCtx.Default.Int32);
    }
  }

  public bool WipeOldTrackOnNewEnabled
  {
    get => p_wipeOldTrackOnNewEnabled;
    set
    {
      p_storage.SetValue(PREF_WIPE_OLD_TRACK_ON_NEW_ENABLED, value, PrefsStorageJsonCtx.Default.Boolean);
    }
  }

  public bool LocationProviderGpsEnabled
  {
    get => (p_locationProviders & LocationProviders.Gps) != 0;
    set
    {
      var newValue = value
        ? p_locationProviders | LocationProviders.Gps
        : p_locationProviders & ~LocationProviders.Gps;

      p_storage.SetValue(PREF_LOCATION_PROVIDERS, newValue, PrefsStorageJsonCtx.Default.LocationProviders);

      if (!value)
        ShowGpsDisabledWarning();
    }
  }

  public bool LocationProviderNetworkEnabled
  {
    get => (p_locationProviders & LocationProviders.Network) != 0;
    set
    {
      var newValue = value
        ? p_locationProviders | LocationProviders.Network
        : p_locationProviders & ~LocationProviders.Network;

      p_storage.SetValue(PREF_LOCATION_PROVIDERS, newValue, PrefsStorageJsonCtx.Default.LocationProviders);
    }
  }

  public bool LocationProviderPassiveEnabled
  {
    get => (p_locationProviders & LocationProviders.Passive) != 0;
    set
    {
      var newValue = value
        ? p_locationProviders | LocationProviders.Passive
        : p_locationProviders & ~LocationProviders.Passive;

      p_storage.SetValue(PREF_LOCATION_PROVIDERS, newValue, PrefsStorageJsonCtx.Default.LocationProviders);
    }
  }

  public bool NotificationOnNewTrack
  {
    get => p_notificationOnNewTrack;
    set
    {
      p_storage.SetValue(PREF_NOTIFY_NEW_TRACK, value, PrefsStorageJsonCtx.Default.Boolean);
    }
  }
  public bool NotificationOnNewPoint
  {
    get => p_notificationOnNewPoint;
    set
    {
      p_storage.SetValue(PREF_NOTIFY_NEW_POINT, value, PrefsStorageJsonCtx.Default.Boolean);
    }
  }

  public bool BleHrmEnabled
  {
    get => p_bleHrmEnabled;
    set
    {
      p_storage.SetValue(PREF_BLE_HRM_ENABLED, value, PrefsStorageJsonCtx.Default.Boolean);
    }
  }

  public Guid? BleHrmDeviceGuid
  {
    get => p_bleHrmDeviceInfo?.DeviceId;
    set
    {
      var newValue = value == null
        ? null
        : new HrmDeviceInfo(value.Value, p_bleHrmDeviceInfo?.DeviceName ?? string.Empty);

      p_storage.SetValue(PREF_BLE_HRM_DEVICE_INFO, newValue, PrefsStorageJsonCtx.Default.HrmDeviceInfo);
    }
  }

  public string? BleHrmDeviceName
  {
    get => p_bleHrmDeviceInfo?.DeviceName;
    set
    {
      var newValue = value == null
        ? null
        : new HrmDeviceInfo(p_bleHrmDeviceInfo?.DeviceId ?? Guid.Empty, value);

      p_storage.SetValue(PREF_BLE_HRM_DEVICE_INFO, newValue, PrefsStorageJsonCtx.Default.HrmDeviceInfo);
    }
  }

  public bool DisplayOnLockScreenEnabled
  {
    get => p_displayOnLockScreenEnabled;
    set
    {
      p_storage.SetValue(PREF_DISPLAY_ON_LOCK_SCREEN, value, PrefsStorageJsonCtx.Default.Boolean);
    }
  }

  public bool DiscordAuthenticated => p_discordTokenExist;
  public bool DiscordNotAuthenticated => !p_discordTokenExist;
  public bool DiscordEnabled
  {
    get => p_discordEnabled;
    set
    {
      p_storage.SetValue(PREF_DISCORD_ENABLED, value, PrefsStorageJsonCtx.Default.Boolean);
    }
  }
  public string? DiscordCustomStatus => p_discordCustomStatus;

  public ICommand RoomIdCommand { get; }
  public ICommand UsernameCommand { get; }
  public ICommand MinimumIntervalCommand { get; }
  public ICommand MinimumDistanceCommand { get; }
  public ICommand TrackpointReportingConditionCommand { get; }
  public ICommand MinAccuracyCommand { get; }
  public ICommand DiscordAuthCommand { get; }
  public ICommand DiscordRevokeCommand { get; }
  public ICommand DiscordStatusCommand { get; }

  private async void OnRoomIdCommand(object _arg)
  {
    var currentPage = p_pagesController.CurrentPage;
    if (currentPage == null)
      return;

    var modeEdit = "Edit value manually";
    var modeGenerate = "Generate new random id";
    var mode = await currentPage.DisplayActionSheetAsync("What would you like to do?", "Cancel", null, modeEdit, modeGenerate);
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
      var serverAddress = DEBUG_APP_ADDRESS ?? ROADNIK_APP_ADDRESS;
      if (!serverAddress.IsNullOrWhiteSpace())
      {
        var url = $"{serverAddress.TrimEnd('/')}/api/v1{ReqPaths.GET_FREE_ROOM_ID}";
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
        roomId = CommonUtilities.GetRandomString(ReqResUtil.MaxRoomIdLength, false);
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
    var result = await currentPage.DisplayActionSheetAsync("Trackpoint reporting condition", null, null, and, or);
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

  private async void ShowGpsDisabledWarning()
  {
    var currentPage = p_pagesController.CurrentPage;
    if (currentPage == null)
      return;

    var body = L.page_options_power_mode_accuracy_warning
      .Replace("%min-location-accuracy%", L.page_options_tracking_required_accuracy);

    await currentPage.DisplayAlertAsync(
      L.page_options_power_mode_accuracy_warning_title,
      body,
      "OK");
  }

  private async void OnDiscordAuth(object? _arg)
  {
    var currentPage = p_pagesController.CurrentPage;
    if (currentPage == null)
      return;

    try
    {
      using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

      p_log.Info($"Opening Discord login page...");

      var loginPage = new DiscordLoginPage();
      await currentPage.Navigation.PushModalAsync(loginPage);

      string? token;
      try
      {
        token = await loginPage.WaitForTokenAsync(cts.Token);
      }
      finally
      {
        await currentPage.Navigation.PopModalAsync();
      }

      if (token.IsNullOrWhiteSpace())
      {
        p_log.Warn($"Discord login: no token received");
        return;
      }

      p_log.Info($"Discord login: token received");

      var appId = p_storage.GetValueOrDefault(PREF_APP_INSTALLATION_ID, PrefsStorageJsonCtx.Default.Guid);
      using var aes = new Ax.Fw.Crypto.AesWithGcm(appId.ToByteArray());
      var json = JsonSerializer.SerializeToUtf8Bytes(token, DiscordJsonCtx.Default.String);
      var encToken = aes.Encrypt(json);
      var encTokenString = Convert.ToBase64String(encToken);
      p_storage.SetValue(PREF_DISCORD_TOKEN, encTokenString, PrefsStorageJsonCtx.Default.String);

      p_log.Info($"Discord login: token saved");
    }
    catch (TaskCanceledException ex) when (ex.InnerException is not OperationCanceledException)
    {
      p_log.Warn($"Discord login was cancelled by the user");
    }
    catch (OperationCanceledException)
    {
      throw;
    }
    catch (Exception ex)
    {
      p_log.Error($"Discord login error", ex);
    }
  }

  private void OnDiscordRevoke(object? _arg)
    => p_discord.RevokeAuth();

  private async void OnDiscordStatus(object? _arg)
  {
    var currentPage = p_pagesController.CurrentPage;
    if (currentPage == null)
      return;

    var status = await currentPage.DisplayPromptAsync(
      "Custom Discord status",
      "Optional message added to your Discord status, e.g. \"Riding in the mountains 🏔️\"",
      "Save",
      "Clear",
      initialValue: DiscordCustomStatus,
      maxLength: 128);

    // null = dismissed, empty string = cleared
    if (status == null)
      return;

    p_storage.SetValue(PREF_DISCORD_STATUS, status.Trim(), PrefsStorageJsonCtx.Default.String);
  }

}
