using Android.OS;
using Android.Provider;
using Android.Views;
using AndroidX.Core.App;
using Ax.Fw.Extensions;
using Ax.Fw.Pools;
using Ax.Fw.SharedTypes.Interfaces;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Extensions;
using QRCoder;
using Roadnik.Common.JsonCtx;
using Roadnik.Common.ReqRes;
using Roadnik.Common.Toolkit;
using Roadnik.MAUI.Controls;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Data.JsonBridge;
using Roadnik.MAUI.Data.LocationProvider;
using Roadnik.MAUI.Data.Serialization;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.JsonCtx;
using Roadnik.MAUI.Modules.LocationProvider;
using Roadnik.MAUI.Pages.Parts;
using Roadnik.MAUI.Toolkit;
using Roadnik.MAUI.ViewModels;
using System.Globalization;
using System.Net.Http.Json;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.Json;
using System.Web;
using static Roadnik.MAUI.Data.AppConsts;
using static Roadnik.MAUI.Data.PageConsts.MainPageConsts;
using L = Roadnik.MAUI.Resources.Strings.AppResources;

namespace Roadnik.MAUI.Pages;

public partial class MainPage : CContentPage
{
  private const string p_backgroundPageUrl = "file:///android_asset/background.html";

  private readonly IPreferencesStorage p_prefs;
  private readonly IPushMessagesController p_pushMsgCtrl;
  private readonly IReadOnlyLifetime p_lifetime;
  private readonly IHttpClientProvider p_httpClient;
  private readonly ILog p_log;
  private readonly IObservable<bool> p_pageIsVisible;
  private readonly Subject<bool> p_pageAppearedChangeFlow = new();
  private readonly Subject<bool> p_webAppTracksSynchonizedSubj = new();
  private readonly MainPageViewModel p_bindingCtx;
  private readonly PowerManager p_powerManager;
  private readonly MapInteractor p_mapInteractor;
  private bool p_mapFollowingMe = false;
  private CancellationTokenSource? p_sideBtnAnimCts;

  public MainPage()
  {
    p_log = Container.Locate<ILog>()["main-page"];
    p_log.Info($"Main page is opening...");

    InitializeComponent();

    var pageController = Container.Locate<IPagesController>();
    pageController.OnMainPage(this);

    p_prefs = Container.Locate<IPreferencesStorage>();
    p_lifetime = Container.Locate<IReadOnlyLifetime>();
    p_httpClient = Container.Locate<IHttpClientProvider>();
    var context = global::Android.App.Application.Context;
    p_powerManager = (PowerManager)context.GetSystemService(Android.Content.Context.PowerService)!;
    p_pushMsgCtrl = Container.Locate<IPushMessagesController>();
    var locationReporter = Container.Locate<ILocationReporter>();

    p_bindingCtx = (MainPageViewModel)BindingContext;
    p_mapInteractor = new MapInteractor(p_webView);

    App.ButtonLongPressed
      .SelectAsync(async (_btn, _ct) =>
      {
        if (_btn == p_goToMyLocationBtn)
          await MainThread.InvokeOnMainThreadAsync(async () => await GoToMyLocation_LongClickedAsync(_btn));
      })
      .Subscribe(p_lifetime);

    p_pageIsVisible = p_pageAppearedChangeFlow
      .CombineLatest(App.WindowActivated)
      //.Throttle(TimeSpan.FromSeconds(1))
      .Scan(false, (_acc, _tuple) =>
      {
        var (appeared, appWindowActivated) = _tuple;
        if (!appeared)
          return false;

        return appWindowActivated;
      });

    p_lifetime.ToDisposeOnEnded(SharedPool<EventLoopScheduler>.Get(out var scheduler));

    p_prefs.PreferencesChanged
      .Select(_ =>
      {
        var roomId = p_prefs.GetValueOrDefault(PREF_ROOM, PrefsStorageJsonCtx.Default.String);
        return (IsDebug: DEBUG_APP_ADDRESS != null, RoomId: roomId);
      })
      .DistinctUntilChanged(_ => HashCode.Combine(_.IsDebug, _.RoomId))
      .CombineLatest(p_pageIsVisible, (_prefs, _pageVisible) => (IsDebug: _prefs.IsDebug, RoomId: _prefs.RoomId, PageVisible: _pageVisible))
      .ObserveOn(scheduler)
      .Subscribe(_entry =>
      {
        var (isDebug, roomId, pageShown) = _entry;
        if (!pageShown)
        {
          p_bindingCtx.WebViewUrl = p_backgroundPageUrl;
          return;
        }

        if (roomId.IsNullOrWhiteSpace())
        {
          _ = MainThread.InvokeOnMainThreadAsync(async () =>
          {
            var page = new OptionsErrorPage(L.page_options_error_incorrect_room_id, L.page_options_error_open_settings);
            await Navigation.PushModalAsync(page);
          });
          return;
        }

        var username = p_prefs.GetValueOrDefault(PREF_USERNAME, PrefsStorageJsonCtx.Default.String);
        var mapState = p_prefs.GetValueOrDefault(PREF_WEBAPP_MAP_STATE, JsBridgeJsonCtx.Default.HostMsgMapStateData);

        var toastText = isDebug ? $"DEBUG MODE\n{roomId}\n{username}" : $"{roomId}\n{username}";
        _ = MainThread.InvokeOnMainThreadAsync(() => Toast.Make(toastText, ToastDuration.Long).Show());

        var url = GetWebAppAddress(DEBUG_APP_ADDRESS ?? ROADNIK_APP_ADDRESS, roomId, mapState);
        p_bindingCtx.WebViewUrl = url;
      }, p_lifetime);

    p_prefs.PreferencesChanged
      .Subscribe(_unit =>
      {
        var tokenExists = !p_prefs.GetValueOrDefault(PREF_DISCORD_TOKEN, PrefsStorageJsonCtx.Default.String).IsNullOrEmpty();
        var isEnabled = p_prefs.GetValueOrDefault(PREF_DISCORD_ENABLED, PrefsStorageJsonCtx.Default.Boolean);
        _ = MainThread.InvokeOnMainThreadAsync(() =>
        {
          p_bindingCtx.IsDiscordButtonVisible = tokenExists;
          p_bindingCtx.DiscordBtnColor = isEnabled
            ? DISCORD_BTN_BRUSH
            : Brush.Black;
        });
      }, p_lifetime);

    p_lifetime.ToDisposeOnEnded(SharedPool<EventLoopScheduler>.Get(out var webAppDataScheduler));

    p_webView.JsonData
      .ObserveOn(webAppDataScheduler)
      .WithLatestFrom(p_pageIsVisible)
      .SelectAsync(OnMsgFromWebAppAsync)
      .Subscribe(p_lifetime);

    p_pushMsgCtrl.OnNewNotification
      .CombineLatest(p_webAppTracksSynchonizedSubj, (_, _webAppIsReady) => _webAppIsReady)
      .Where(_ => _)
      .ToUnit()
      .Throttle(TimeSpan.FromMilliseconds(250))
      .SelectAsync(async (_, _ct) => await OnNotificationAsync(_ct))
      .Subscribe(p_lifetime);

    p_pageAppearedChangeFlow
      .Where(_ => _)
      .Throttle(TimeSpan.FromSeconds(1))
      .Subscribe(_ =>
      {
        if (Build.VERSION.SdkInt > BuildVersionCodes.SV2 && Platform.CurrentActivity != null)
        {
          // ActivityCompat.ShouldShowRequestPermissionRationale(Platform.CurrentActivity, "android.permission.POST_NOTIFICATIONS") // always returns false!!!

          var granted = ActivityCompat.CheckSelfPermission(Platform.CurrentActivity, "android.permission.POST_NOTIFICATIONS");
          if (granted != Android.Content.PM.Permission.Granted)
            ActivityCompat.RequestPermissions(Platform.CurrentActivity, ["android.permission.POST_NOTIFICATIONS"], 1000);
        }
      }, p_lifetime);

    locationReporter.Enabled
      .DistinctUntilChanged()
      .Subscribe(_enabled =>
      {
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
          if (_enabled)
          {
            if (Application.Current?.Resources.TryGetValue("DangerLowBrush", out var rawBrush) == true && rawBrush is Brush brush)
              p_startRecordButton.Background = brush;
            else
              p_log.Error($"Resource 'DangerLowBrush' is not found!");

            await SetupAndShowDiscordBtnAnimationAsync();
          }
          else
          {
            if (Application.Current?.Resources.TryGetValue("PrimaryBrush", out var rawBrush) == true && rawBrush is Brush brush)
              p_startRecordButton.Background = brush;
            else
              p_log.Error($"Resource 'PrimaryBrush' is not found!");

            p_sideBtnAnimCts?.Cancel();
            p_sideBtnAnimCts = null;
            await CollapseDiscordButtonAsync();
          }
        });
      }, p_lifetime);

    p_pageIsVisible
      .Where(_ => !_)
      .Subscribe(_ => p_webAppTracksSynchonizedSubj.OnNext(false), p_lifetime);

    p_pageIsVisible
      .Subscribe(_ =>
      {
        var showOnLockScreen = p_prefs.GetValueOrDefault(PREF_DISPLAY_ON_LOCK_SCREEN, PrefsStorageJsonCtx.Default.Boolean);

        if (pageController.CurrentPage == this)
          Platform.CurrentActivity?.SetShowWhenLocked(showOnLockScreen);
        else
          Platform.CurrentActivity?.SetShowWhenLocked(false);
      }, p_lifetime);

    var compassProvider = Container.Locate<ICompassProvider>();
    var webAppLocationProvider = new AndroidLocationProvider(p_log, p_lifetime);
    p_webAppTracksSynchonizedSubj
      .CombineLatest(p_pageIsVisible)
      .HotAlive(p_lifetime, null, (_tuple, _life) =>
      {
        var (webAppTracksReady, pageIsVisible) = _tuple;
        if (!webAppTracksReady || !pageIsVisible)
          return;

        _ = Task.Run(async () =>
        {
          try
          {
            var permissionGranted = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (permissionGranted != PermissionStatus.Granted)
              return;

            webAppLocationProvider.StartLocationWatcher(LocationProviders.All, TimeSpan.FromSeconds(1));
            _life.DoOnEnding(() => webAppLocationProvider.StopLocationWatcher());

            var semaphore = 0;

            Observable
              .CombineLatest(
                compassProvider.Values
                  .Sample(TimeSpan.FromMilliseconds(100)),
                webAppLocationProvider.Location
                  .Buffer(TimeSpan.FromSeconds(1)),
                (_c, _l) => (Heading: _c, Locations: _l)
              )
              .Where(_ =>
              {
                var entered = Interlocked.Exchange(ref semaphore, 1) == 0;
                return entered;
              })
              .SelectAsync(async (_tuple, _ct) =>
              {
                var (heading, locations) = _tuple;

                var loc = locations
                  .OrderBy(_ => _.Accuracy)
                  .FirstOrDefault();

                if (_ct.IsCancellationRequested)
                  return;

                try
                {
                  await MainThread.InvokeOnMainThreadAsync(async () =>
                  {
                    if (_ct.IsCancellationRequested)
                      return;

                    if (loc != null)
                    {
                      await p_mapInteractor.SetLocationAndHeadingAsync(loc, _ct);

                      if (p_mapFollowingMe)
                        await p_mapInteractor.SetMapCenterAsync((float)loc.Latitude, (float)loc.Longitude, _ct: _ct);
                    }

                    await p_mapInteractor.SetCompassHeadingAsync(heading, _ct);
                  });
                }
                catch (Exception ex)
                {
                  p_log.Error($"Can't handle current location and compass change: {ex}");
                }
              })
              .Subscribe(_ => Interlocked.Exchange(ref semaphore, 0), _life);
          }
          catch (Exception ex)
          {
            p_log.Error($"Can't start sending current location to web app due to geolocation error: {ex}");
          }
        });
      });

    p_log.Info($"Main page is opened");
  }

  protected override void OnAppearing()
  {
    base.OnAppearing();
    p_pageAppearedChangeFlow.OnNext(true);
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();
    p_pageAppearedChangeFlow.OnNext(false);
  }

  private async void FAB_Clicked(object _sender, EventArgs _e)
  {
    // privacy policy
    var version = p_prefs.GetValueOrDefault(PREF_PRIVACY_POLICY_VERSION, PrefsStorageJsonCtx.Default.Int32);
    if (version < PRIVACY_POLICY_VERSION)
    {
      var agreed = false;
      var result = await this.ShowPopupAsync(new AgreementsPopup(_agreed => agreed = _agreed));

      if (!agreed)
        return;

      p_prefs.SetValue(PREF_PRIVACY_POLICY_VERSION, PRIVACY_POLICY_VERSION, PrefsStorageJsonCtx.Default.Int32);
    }

    // check permissions and run
    var locationReporter = Container.Locate<ILocationReporter>();
    if (!await locationReporter.IsEnabledAsync())
    {
      var permissionGranted = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
      if (permissionGranted != PermissionStatus.Granted)
      {
        await Navigation.PushModalAsync(new LocationPermissionPage(true, _ok =>
        {
          p_log.Info($"Location permissions dialog result: {_ok}");
          return Task.CompletedTask;
        }));

        return;
      }

      await RequestIgnoreBatteryOptimizationAsync(p_lifetime.Token);
      locationReporter.SetState(true);

      var providers = new List<string>();
      var locProvider = p_prefs.GetValueOrDefault(PREF_LOCATION_PROVIDERS, PrefsStorageJsonCtx.Default.LocationProviders);
      if ((locProvider & LocationProviders.Gps) != 0)
        providers.Add(L.page_options_power_mode_high_accuracy);
      if ((locProvider & LocationProviders.Network) != 0)
        providers.Add(L.page_options_power_mode_medium_accuracy);
      if ((locProvider & LocationProviders.Passive) != 0)
        providers.Add(L.page_options_power_mode_passive);

      await Toast.Make($"{L.page_options_power_mode_title}: {string.Join(", ", providers)}", ToastDuration.Short).Show(p_lifetime.Token);
    }
    else
    {
      locationReporter.SetState(false);
    }
  }

  private void MainWebView_Navigating(object _sender, WebNavigatingEventArgs _e) => p_bindingCtx.IsSpinnerRequired = true;

  private void MainWebView_Navigated(object _sender, WebNavigatedEventArgs _e)
  {
    if (_e.Result != WebNavigationResult.Success)
      p_log.Warn($"WebView navigation error '{_e.Result}': {_e.Url}");

    if (_e.Url == p_backgroundPageUrl)
      p_bindingCtx.IsSpinnerRequired = false;
  }

  private async void GoToMyLocation_ClickedAsync(object _sender, EventArgs _e)
    => await OnGoToMyLocationBtnClick((Button)_sender, false);

  private async Task GoToMyLocation_LongClickedAsync(IButton _btn)
    => await OnGoToMyLocationBtnClick(_btn, true);

  private async Task OnGoToMyLocationBtnClick(
    IButton _btn,
    bool _followMyLocation)
  {
    var permissionGranted = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
    if (permissionGranted != PermissionStatus.Granted)
    {
      await Navigation.PushModalAsync(new LocationPermissionPage(false, _ok =>
      {
        p_log.Info($"Location permissions dialog result: {_ok}");
        return Task.CompletedTask;
      }));

      return;
    }

    if (_btn is not Button button)
      return;

    button.IsEnabled = false;
    var animation = new Animation(_rotation => p_goToMyLocationImage.Rotation = _rotation, 0, 360);
    try
    {
      animation.Commit(p_goToMyLocationImage, "my-loc-anim", 16, 2000, null, null, () => true);

      if (_followMyLocation)
      {
        p_mapFollowingMe = true;
        p_bindingCtx.LocationBtnImage = "location.svg";
        await p_mapInteractor.SetObservedUserAsync(null, true, p_lifetime.Token);
      }

      var location = await AndroidLocationProvider.GetCurrentBestLocationAsync(TimeSpan.FromSeconds(2), default);
      if (location != null)
      {
        var lat = location.Latitude.ToInvariantString();
        var lng = location.Longitude.ToInvariantString();
        await p_mapInteractor.SetMapCenterAsync((float)location.Latitude, (float)location.Longitude, null, 500, p_lifetime.Token);
      }
    }
    finally
    {
      animation.Dispose();
      button.IsEnabled = true;
      await p_goToMyLocationImage.RotateToAsync(0, 250);
    }
  }

  private async void Share_Clicked(object _sender, EventArgs _e)
  {
    var serverAddress = DEBUG_APP_ADDRESS ?? ROADNIK_APP_ADDRESS;
    var roomId = p_prefs.GetValueOrDefault(PREF_ROOM, PrefsStorageJsonCtx.Default.String);
    if (serverAddress.IsNullOrWhiteSpace() || roomId.IsNullOrWhiteSpace())
    {
      await DisplayAlertAsync("Room id is invalid", null, "Ok");
      return;
    }

    var url = $"{serverAddress.TrimEnd('/')}/r/?id={roomId}";

    var methodUrlLink = "Share link as text";
    var methodQrCode = "Share link as QR code";
    var method = await DisplayActionSheetAsync(null, null, null, methodUrlLink, methodQrCode);
    if (method == null)
      return;

    if (method == methodUrlLink)
    {
      var req = new ShareTextRequest(url, "Url");
      await Share.Default.RequestAsync(req);
    }
    else if (method == methodQrCode)
    {
      var pngBytes = await Task.Run(() =>
      {
        var generator = new PayloadGenerator.Url(url);
        var payload = generator.ToString();

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(20);
      });

      await this.ShowPopupAsync(new ImagePopup(pngBytes));
    }
  }

  private async Task OnMsgFromWebAppAsync((JsToCSharpMsg?, bool) _tuple, CancellationToken _ct)
  {
    var (msg, isPageVisible) = _tuple;
    if (msg == null)
      return;
    if (!isPageVisible)
      return;

    if (msg.MsgType == HOST_MSG_TRACKS_SYNCHRONIZED)
      OnHostMsgTracksSynchronized(msg);
    else if (msg.MsgType == JS_TO_CSHARP_MSG_TYPE_WAYPOINT_ADD_STARTED)
      await OnJsMsgPointAddStartedAsync(msg);
    else if (msg.MsgType == HOST_MSG_MAP_STATE)
      await OnWebAppMsgMapStateAsync(msg);
    else if (msg.MsgType == HOST_MSG_MAP_DRAG_STARTED)
      await OnHostMsgMapDragStartedAsync();
  }

  private void OnHostMsgTracksSynchronized(JsToCSharpMsg _msg)
  {
    p_bindingCtx.IsSpinnerRequired = false;

    var msgData = _msg.Data.Deserialize(JsBridgeJsonCtx.Default.HostMsgTracksSynchronizedData);
    if (msgData == null)
    {
      p_log.Error($"Can't parse msg data of type '{nameof(HOST_MSG_TRACKS_SYNCHRONIZED)}': '{_msg.Data}'");
      return;
    }

    if (msgData.IsFirstSync)
      p_webAppTracksSynchonizedSubj.OnNext(true);
  }

  private async Task OnJsMsgPointAddStartedAsync(JsToCSharpMsg _msg)
  {
    var serverAddress = DEBUG_APP_ADDRESS ?? ROADNIK_APP_ADDRESS;
    if (serverAddress.IsNullOrWhiteSpace())
      return;

    var roomId = p_prefs.GetValueOrDefault(PREF_ROOM, PrefsStorageJsonCtx.Default.String);
    if (roomId.IsNullOrWhiteSpace())
      return;

    var username = p_prefs.GetValueOrDefault(PREF_USERNAME, PrefsStorageJsonCtx.Default.String);
    if (username.IsNullOrWhiteSpace())
      return;

    var appId = p_prefs.GetValueOrDefault(PREF_APP_INSTALLATION_ID, PrefsStorageJsonCtx.Default.Guid);

    var latLng = _msg.Data.Deserialize<LatLng>(GenericSerializationOptions.CaseInsensitive);
    if (latLng == null)
    {
      p_log.Error($"Tried to create new point, but could not parse location!\n{_msg.Data}");
      return;
    }

    var dialogResult = await MainThread.InvokeOnMainThreadAsync(() =>
      DisplayPromptAsync($"Add new point at [{(int)latLng.Lat}, {(int)latLng.Lng}]", "Please enter description:", maxLength: 128));
    if (dialogResult == null)
      return;

    try
    {
      p_log.Info($"Sending request to create point [{(int)latLng.Lat}, {(int)latLng.Lng}] in room '{roomId}'");

      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
      using var content = JsonContent.Create(
        CreateRoomPointReq.From(GenericToolkit.ConcealAppInstanceId(appId), roomId, username, latLng.Lat, latLng.Lng, dialogResult),
        RestJsonCtx.Default.CreateRoomPointReq);
      using var req = new HttpRequestMessage(HttpMethod.Post, $"{serverAddress.TrimEnd('/')}/api/v1{ReqPaths.CREATE_ROOM_POINT}") { Content = content };
      using var res = await p_httpClient.Value.SendAsync(req, cts.Token);
      res.EnsureSuccessStatusCode();
      p_log.Info($"Point [{(int)latLng.Lat}, {(int)latLng.Lng}] is successfully created in room '{roomId}'");
    }
    catch (Exception ex)
    {
      p_log.Error($"Request to create point [{(int)latLng.Lat}, {(int)latLng.Lng}] in room '{roomId}' was failed", ex);
    }
  }

  private async Task OnWebAppMsgMapStateAsync(JsToCSharpMsg _msg)
  {
    try
    {
      var data = JsonSerializer.Deserialize(_msg.Data, JsBridgeJsonCtx.Default.HostMsgMapStateData);
      if (data == null)
      {
        p_log.Error($"Data of webapp msg of type '{HOST_MSG_MAP_STATE}' is null");
        return;
      }

      p_prefs.SetValue(PREF_WEBAPP_MAP_STATE, data, JsBridgeJsonCtx.Default.HostMsgMapStateData);

      if (data.SelectedAppId != null)
        await CancelFollowCurrentLocationAsync();
    }
    catch (Exception ex)
    {
      p_log.Error($"Cannot deserialize the data of webapp msg of type '{HOST_MSG_MAP_STATE}': {ex}");
    }
  }

  private Task OnHostMsgMapDragStartedAsync()
    => CancelFollowCurrentLocationAsync();

  private async Task OnNotificationAsync(CancellationToken _ct)
  {
    PushNotificationEvent? ev = null;
    while (p_pushMsgCtrl.Notifications.TryTake(out var e))
      ev = e;

    if (ev == null)
      return;

    if (ev.NotificationId == PUSH_MSG_NEW_POINT)
    {
      var data = ev.Data.Deserialize(AndroidPushJsonCtx.Default.PushMsgRoomPointAdded);
      if (data == default)
        return;

      await MainThread.InvokeOnMainThreadAsync(async () =>
      {
        await p_mapInteractor.SetMapCenterAsync((float)data.Lat, (float)data.Lng, 15, 500, _ct);
      });
    }
    else if (ev.NotificationId == PUSH_MSG_NEW_TRACK)
    {
      var data = ev.Data.Deserialize(AndroidPushJsonCtx.Default.PushMsgNewTrackStarted);
      if (data == null)
        return;

      await MainThread.InvokeOnMainThreadAsync(async () =>
      {
        if (!await p_mapInteractor.SetMapCenterToUserAsync(data.AppId, 13, _ct))
          await p_mapInteractor.SetMapCenterToAllUsersAsync(_ct);
      });
    }
  }

  private void ShellOpen_Clicked(object sender, EventArgs e)
  {
    Shell.Current.FlyoutIsPresented = true;
  }

  private void DiscordToggle_Tapped(object _sender, TappedEventArgs _e)
  {
    var isEnabled = p_prefs.GetValueOrDefault(PREF_DISCORD_ENABLED, PrefsStorageJsonCtx.Default.Boolean);
    p_prefs.SetValue(PREF_DISCORD_ENABLED, !isEnabled, PrefsStorageJsonCtx.Default.Boolean);
  }

  private async Task SetupAndShowDiscordBtnAnimationAsync()
  {
    var tokenExists = !p_prefs.GetValueOrDefault(PREF_DISCORD_TOKEN, PrefsStorageJsonCtx.Default.String).IsNullOrEmpty();
    var discordEnabled = p_prefs.GetValueOrDefault(PREF_DISCORD_ENABLED, PrefsStorageJsonCtx.Default.Boolean);
    var statusText = p_prefs.GetValueOrDefault(PREF_DISCORD_STATUS, PrefsStorageJsonCtx.Default.String);
    if (tokenExists && discordEnabled && !statusText.IsNullOrWhiteSpace())
    {
      p_bindingCtx.DiscordStatusText = statusText;

      p_sideBtnAnimCts?.Cancel();
      var cts = p_sideBtnAnimCts = new CancellationTokenSource();

      // calculate target width of button based on text width and screen width
      var density = DeviceDisplay.Current.MainDisplayInfo.Density;
      var screenWidth = DeviceDisplay.Current.MainDisplayInfo.Width / density;
      var maxTargetWidth = screenWidth * 0.8;
      var context = global::Android.App.Application.Context;
      using var paint = new Android.Graphics.Paint();
      paint.TextSize = Android.Util.TypedValue.ApplyDimension(
        Android.Util.ComplexUnitType.Sp,
        (float)MAP_SIDE_BTN_LABEL_FONT_SIZE_SP,
        context.Resources!.DisplayMetrics);
      var textWidthDp = paint.MeasureText(statusText ?? string.Empty) / density;
      var targetWidth = Math.Min(maxTargetWidth, MAP_SIDE_BTN_COLLAPSED_SIZE + textWidthDp + 16); // 16: padding on the right side of label
      targetWidth = Math.Max(MAP_SIDE_BTN_COLLAPSED_SIZE, targetWidth);

      var bounds = p_bindingCtx.DiscordBtnBounds;
      if (bounds.Width >= targetWidth)
        return;

      var startWidth = bounds.Width;
      var startOpacity = p_bindingCtx.DiscordBtnOpacity;
      var tcs = new TaskCompletionSource<Unit>();

      var animation = new Animation();
      animation.Add(0, 1, new Animation(_t =>
      {
        var w = startWidth + (targetWidth - startWidth) * _t;
        p_bindingCtx.DiscordBtnBounds = new Rect(bounds.X, bounds.Y, w, bounds.Height);
      }));
      animation.Add(0, 1, new Animation(_t =>
      {
        const double expandedOpacity = MAP_SIDE_BTN_COLLAPSED_OPACITY * 2;
        p_bindingCtx.DiscordBtnOpacity = startOpacity + (expandedOpacity - startOpacity) * _t;
      }));

      animation.Commit(p_discordFrame, "DiscordResize", 16, 300, Easing.CubicOut, (_, __) => tcs.TrySetResult(Unit.Default));
      await tcs.Task;

      try
      {
        await Task.Delay(5000, cts.Token);
      }
      catch (System.OperationCanceledException) { }

      if (!cts.IsCancellationRequested)
        await CollapseDiscordButtonAsync();
    }
  }

  private async Task CollapseDiscordButtonAsync()
  {
    var bounds = p_bindingCtx.DiscordBtnBounds;
    if (bounds.Width <= MAP_SIDE_BTN_COLLAPSED_SIZE)
    {
      p_bindingCtx.DiscordBtnOpacity = MAP_SIDE_BTN_COLLAPSED_OPACITY;
      p_bindingCtx.DiscordStatusText = null;
      return;
    }

    var startWidth = bounds.Width;
    var startOpacity = p_bindingCtx.DiscordBtnOpacity;
    var tcs = new TaskCompletionSource<Unit>();

    var animation = new Animation();
    animation.Add(0, 1, new Animation(_t =>
    {
      var w = startWidth + (MAP_SIDE_BTN_COLLAPSED_SIZE - startWidth) * _t;
      p_bindingCtx.DiscordBtnBounds = new Rect(bounds.X, bounds.Y, w, bounds.Height);
    }));
    animation.Add(0, 1, new Animation(_t =>
    {
      p_bindingCtx.DiscordBtnOpacity = startOpacity + (MAP_SIDE_BTN_COLLAPSED_OPACITY - startOpacity) * _t;
    }));

    animation.Commit(p_discordFrame, "DiscordResize", 16, 300, Easing.CubicIn, (_, __) => tcs.TrySetResult(Unit.Default));
    await tcs.Task;

    p_bindingCtx.DiscordStatusText = null;
  }

  private async Task RequestIgnoreBatteryOptimizationAsync(CancellationToken _ct)
  {
    var context = global::Android.App.Application.Context;
    if (p_powerManager.IsIgnoringBatteryOptimizations(context.PackageName))
      return;

    var dialogResult = await DisplayAlertAsync(
      L.page_main_battery_optimization_title,
      L.page_main_battery_optimization_body,
      "OK",
      L.generic_cancel);

    if (_ct.IsCancellationRequested)
      return;
    if (!dialogResult)
      return;

    var intent = new Android.Content.Intent(Settings.ActionIgnoreBatteryOptimizationSettings);
    intent.AddFlags(Android.Content.ActivityFlags.NewTask);
    context.StartActivity(intent);
  }

  private static string GetWebAppAddress(
    string _serverAddress,
    string? _roomId,
    HostMsgMapStateData? _mapState = null)
  {
    var serverUri = new Uri(_serverAddress);
    var urlBuilder = new UriBuilder($"{serverUri.Scheme}://{WEBAPP_HOST}:{serverUri.Port}/r/");

    var query = HttpUtility.ParseQueryString(urlBuilder.Query);
    query["id"] = _roomId;
    query["api_url"] = _serverAddress;
    if (_mapState?.Lat != null)
      query["lat"] = _mapState.Lat.ToString(CultureInfo.InvariantCulture);
    if (_mapState?.Lng != null)
      query["lng"] = _mapState.Lng.ToString(CultureInfo.InvariantCulture);
    if (_mapState?.Zoom != null)
      query["zoom"] = ((int)_mapState.Zoom).ToString(CultureInfo.InvariantCulture);
    if (_mapState != null && !_mapState.Layer.IsNullOrWhiteSpace())
      query["layer"] = _mapState.Layer;
    if (_mapState?.Overlays != null)
    {
      var json = JsonSerializer.Serialize(_mapState.Overlays, JsBridgeJsonCtx.Default.IReadOnlyListString);
      var jsonBytes = Encoding.UTF8.GetBytes(json);
      var base64 = Convert.ToBase64String(jsonBytes);
      query["overlays"] = base64;
    }
    if (_mapState != null && !_mapState.SelectedAppId.IsNullOrWhiteSpace())
      query["selected_app_id"] = _mapState.SelectedAppId;
    if (_mapState?.SelectedPathWindowLeft != null)
      query["selected_path_window_left"] = _mapState.SelectedPathWindowLeft.Value.ToString(CultureInfo.InvariantCulture); ;
    if (_mapState?.SelectedPathWindowBottom != null)
      query["selected_path_window_bottom"] = _mapState.SelectedPathWindowBottom.Value.ToString(CultureInfo.InvariantCulture);
    if (_mapState?.UsbLeft != null)
      query["usb_left"] = _mapState.UsbLeft.Value.ToString(CultureInfo.InvariantCulture);
    if (_mapState?.UsbBottom != null)
      query["usb_bottom"] = _mapState.UsbBottom.Value.ToString(CultureInfo.InvariantCulture);

    urlBuilder.Query = query.ToString();

    var url = urlBuilder.ToString();
    return url;
  }

  private async Task CancelFollowCurrentLocationAsync()
  {
    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      if (!p_mapFollowingMe)
        return;

      p_mapFollowingMe = false;
      p_bindingCtx.LocationBtnImage = "location_empty.svg";
      p_log.Info("Map now is not following device location");
    });
  }

}