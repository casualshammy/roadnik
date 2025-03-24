using Android.OS;
using Android.Provider;
using AndroidX.Core.App;
using Ax.Fw.Extensions;
using Ax.Fw.Pools;
using Ax.Fw.SharedTypes.Interfaces;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using QRCoder;
using Roadnik.Common.JsonCtx;
using Roadnik.Common.ReqRes;
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
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.Json;
using System.Web;
using static Roadnik.MAUI.Data.Consts;
using L = Roadnik.MAUI.Resources.Strings.AppResources;

namespace Roadnik.MAUI.Pages;

public partial class MainPage : CContentPage
{
  private const string p_backgroundPageUrl = "file:///android_asset/background.html";

  private readonly IPreferencesStorage p_prefs;
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
    var pushMsgCtrl = Container.Locate<IPushMessagesController>();
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
        var serverAddress = DEBUG_APP_ADDRESS ?? ROADNIK_APP_ADDRESS;
        var roomId = p_prefs.GetValueOrDefault<string>(PREF_ROOM);

        return (serverAddress, roomId);
      })
      .DistinctUntilChanged(_ => HashCode.Combine(_.serverAddress, _.roomId))
      //.Sample(TimeSpan.FromSeconds(1), scheduler)
      .CombineLatest(p_pageIsVisible, (_prefs, _pageVisible) => (ServerAddress: _prefs.serverAddress, RoomId: _prefs.roomId, PageVisible: _pageVisible))
      .ObserveOn(scheduler)
      .SelectAsync(async (_entry, _ct) =>
      {
        var (serverAddress, roomId, pageShown) = _entry;
        if (!pageShown)
        {
          p_bindingCtx.WebViewUrl = p_backgroundPageUrl;
          return;
        }

        if (serverAddress.IsNullOrWhiteSpace())
        {
          _ = MainThread.InvokeOnMainThreadAsync(async () =>
          {
            var page = new OptionsErrorPage(L.page_options_error_incorrect_server_address, L.page_options_error_open_settings);
            await Navigation.PushModalAsync(page);
          });
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

        var username = p_prefs.GetValueOrDefault<string>(PREF_USERNAME);
        var mapState = p_prefs.GetValueOrDefault(PREF_WEBAPP_MAP_STATE, JsBridgeJsonCtx.Default.HostMsgMapStateData);

        _ = MainThread.InvokeOnMainThreadAsync(() => Toast.Make($"{serverAddress}\n{roomId}/{username}", ToastDuration.Long).Show(_ct));

        var url = GetWebAppAddress(serverAddress, roomId, mapState);
        p_bindingCtx.WebViewUrl = url;
      }, scheduler)
      .Subscribe(p_lifetime);

    p_lifetime.ToDisposeOnEnded(SharedPool<EventLoopScheduler>.Get(out var webAppDataScheduler));

    p_webView.JsonData
      .ObserveOn(webAppDataScheduler)
      .WithLatestFrom(p_pageIsVisible)
      .SelectAsync(OnMsgFromWebAppAsync)
      .Subscribe(p_lifetime);

    pushMsgCtrl.PushMessages
      .CombineLatest(p_webAppTracksSynchonizedSubj, (_pushEvent, _webAppIsReady) => (PushEvent: _pushEvent, WebAppIsReady: _webAppIsReady))
      .Where(_ => _.WebAppIsReady)
      .Select(_ => _.PushEvent)
      .Throttle(TimeSpan.FromMilliseconds(250))
      .SelectAsync(OnNotificationAsync)
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
        _ = MainThread.InvokeOnMainThreadAsync(() =>
        {
          if (_enabled)
          {
            if (Application.Current?.Resources.TryGetValue("DangerLowBrush", out var rawBrush) == true && rawBrush is Brush brush)
              p_startRecordButton.Background = brush;
            else
              p_log.Error($"Resource 'DangerLowBrush' is not found!");
          }
          else
          {
            if (Application.Current?.Resources.TryGetValue("PrimaryBrush", out var rawBrush) == true && rawBrush is Brush brush)
              p_startRecordButton.Background = brush;
            else
              p_log.Error($"Resource 'PrimaryBrush' is not found!");
          }
        });
      }, p_lifetime);

    p_pageIsVisible
      .Where(_ => !_)
      .Subscribe(_ => p_webAppTracksSynchonizedSubj.OnNext(false), p_lifetime);

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

            compassProvider.Values
              .Sample(TimeSpan.FromMilliseconds(100))
              .SelectAsync(async (_heading, _ct) =>
              {
                if (_ct.IsCancellationRequested)
                  return;

                try
                {
                  await MainThread.InvokeOnMainThreadAsync(async () =>
                  {
                    await p_mapInteractor.SetCompassHeadingAsync(_heading, _ct);
                  });
                }
                catch (Exception ex)
                {
                  p_log.Error($"Can't handle compass change: {ex}");
                }
              })
              .Subscribe(_life);

            webAppLocationProvider.Location
              .Buffer(TimeSpan.FromSeconds(1))
              .SelectAsync(async (_locations, _ct) =>
              {
                if (_ct.IsCancellationRequested)
                  return;

                if (_locations.Count == 0)
                  return;

                var loc = _locations
                  .OrderBy(_ => _.Accuracy)
                  .First();

                try
                {
                  await MainThread.InvokeOnMainThreadAsync(async () =>
                  {
                    await p_mapInteractor.SetLocationAndHeadingAsync(loc, _ct);

                    if (p_mapFollowingMe)
                      await p_mapInteractor.SetMapCenterAsync((float)loc.Latitude, (float)loc.Longitude, _ct: _ct);
                  });
                }
                catch (Exception ex)
                {
                  p_log.Error($"Can't handle current location change: {ex}");
                }
              })
              .Subscribe(_life);
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
    var version = p_prefs.GetValueOrDefault<int>(PREF_PRIVACY_POLICY_VERSION);
    if (version < PRIVACY_POLICY_VERSION)
    {
      var result = await this.ShowPopupAsync(new AgreementsPopup());
      if (result is not bool agreed || !agreed)
        return;

      p_prefs.SetValue(PREF_PRIVACY_POLICY_VERSION, PRIVACY_POLICY_VERSION);
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

      await RequestIgnoreBattaryOptimizationAsync(p_lifetime.Token);
      locationReporter.SetState(true);

      var providers = new List<string>();
      var locProvider = p_prefs.GetValueOrDefault<LocationProviders>(PREF_LOCATION_PROVIDERS);
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

      var location = await AndroidLocationProvider.GetCurrentBestLocationAsync(TimeSpan.FromSeconds(5), default);
      if (location != null)
      {
        var lat = location.Latitude.ToString(CultureInfo.InvariantCulture);
        var lng = location.Longitude.ToString(CultureInfo.InvariantCulture);
        await p_mapInteractor.SetMapCenterAsync((float)location.Latitude, (float)location.Longitude, null, 500, p_lifetime.Token);
      }
    }
    finally
    {
      animation.Dispose();
      button.IsEnabled = true;
      await p_goToMyLocationImage.RotateTo(0, 250);
    }
  }

  private async void Share_Clicked(object _sender, EventArgs _e)
  {
    var serverAddress = DEBUG_APP_ADDRESS ?? ROADNIK_APP_ADDRESS;
    var roomId = p_prefs.GetValueOrDefault<string>(PREF_ROOM);
    if (serverAddress.IsNullOrWhiteSpace() || roomId.IsNullOrWhiteSpace())
    {
      await DisplayAlert("Room id is invalid", null, "Ok");
      return;
    }

    var url = $"{serverAddress.TrimEnd('/')}/r/?id={roomId}";

    var methodUrlLink = "Share link as text";
    var methodQrCode = "Share link as QR code";
    var method = await DisplayActionSheet(null, null, null, methodUrlLink, methodQrCode);
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

    var roomId = p_prefs.GetValueOrDefault<string>(PREF_ROOM);
    if (roomId.IsNullOrWhiteSpace())
      return;

    var username = p_prefs.GetValueOrDefault<string>(PREF_USERNAME);
    if (username.IsNullOrWhiteSpace())
      return;

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
      using var req = new HttpRequestMessage(HttpMethod.Post, $"{serverAddress.TrimEnd('/')}{ReqPaths.CREATE_NEW_POINT}");
      using var content = JsonContent.Create(new CreateRoomPointReq
      {
        RoomId = roomId,
        Username = username,
        Lat = latLng.Lat,
        Lng = latLng.Lng,
        Description = dialogResult
      }, RestJsonCtx.Default.CreateRoomPointReq);
      req.Content = content;
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
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

      if (data.SelectedPath != null)
        await CancelFollowCurrentLocationAsync();
    }
    catch (Exception ex)
    {
      p_log.Error($"Cannot deserialize the data of webapp msg of type '{HOST_MSG_MAP_STATE}': {ex}");
    }
  }

  private Task OnHostMsgMapDragStartedAsync()
    => CancelFollowCurrentLocationAsync();

  private async Task OnNotificationAsync(PushNotificationEvent _e, CancellationToken _ct)
  {
    if (_e.NotificationId == PUSH_MSG_NEW_POINT)
    {
      var data = _e.Data.Deserialize<LatLng>();
      if (data == default)
        return;

      await MainThread.InvokeOnMainThreadAsync(async () =>
      {
        await p_mapInteractor.SetMapCenterAsync((float)data.Lat, (float)data.Lng, 15, 500, _ct);
      });
    }
    else if (_e.NotificationId == PUSH_MSG_NEW_TRACK)
    {
      var username = _e.Data.Deserialize<string>();
      if (username == null)
        return;

      await MainThread.InvokeOnMainThreadAsync(async () =>
      {
        if (!await p_mapInteractor.SetMapCenterToUserAsync(username, 13, _ct))
          await p_mapInteractor.SetMapCenterToAllUsersAsync(_ct);
      });
    }
  }

  private void ShellOpen_Clicked(object sender, EventArgs e)
  {
    Shell.Current.FlyoutIsPresented = true;
  }

  private async Task RequestIgnoreBattaryOptimizationAsync(CancellationToken _ct)
  {
    var context = global::Android.App.Application.Context;
    if (p_powerManager.IsIgnoringBatteryOptimizations(context.PackageName))
      return;

    var dialogResult = await DisplayAlert(
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
    if (_mapState != null && !_mapState.SelectedPath.IsNullOrWhiteSpace())
      query["selected_path"] = _mapState.SelectedPath;
    if (_mapState?.SelectedPathWindowLeft != null)
      query["selected_path_window_left"] = _mapState.SelectedPathWindowLeft.Value.ToString(CultureInfo.InvariantCulture); ;
    if (_mapState?.SelectedPathWindowBottom != null)
      query["selected_path_window_bottom"] = _mapState.SelectedPathWindowBottom.Value.ToString(CultureInfo.InvariantCulture);

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