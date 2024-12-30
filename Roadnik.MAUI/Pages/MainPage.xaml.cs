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
using Roadnik.Common.ReqRes;
using Roadnik.MAUI.Controls;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Data.JsonBridge;
using Roadnik.MAUI.Data.Serialization;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.JsonCtx;
using Roadnik.MAUI.Modules.LocationProvider;
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
  private const string p_loadingPageUrl = "loading.html";
  private readonly IPreferencesStorage p_prefs;
  private readonly IReadOnlyLifetime p_lifetime;
  private readonly IHttpClientProvider p_httpClient;
  private readonly ILog p_log;
  private readonly IObservable<bool> p_pageIsVisible;
  private readonly Subject<bool> p_pageAppearedChangeFlow = new();
  private readonly Subject<bool> p_webAppTracksSynchonizedSubj = new();
  private readonly MainPageViewModel p_bindingCtx;
  private readonly PowerManager p_powerManager;

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

    if (BindingContext is not MainPageViewModel bindingCtx)
    {
      p_log.Error($"Can't get binding ctx!");
      throw new InvalidDataException($"Can't get binding ctx!");
    }

    p_bindingCtx = bindingCtx;

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
        var serverAddress = p_prefs.GetValueOrDefault<string>(PREF_SERVER_ADDRESS);
        var roomId = p_prefs.GetValueOrDefault<string>(PREF_ROOM);

        return (serverAddress, roomId);
      })
      .DistinctUntilChanged(_ => HashCode.Combine(_.serverAddress, _.roomId))
      //.Sample(TimeSpan.FromSeconds(1), scheduler)
      .CombineLatest(p_pageIsVisible)
      .ObserveOn(scheduler)
      .SelectAsync(async (_entry, _ct) =>
      {
        var (_, pageShown) = _entry;

        bindingCtx.IsInBackground = !pageShown;
        if (!pageShown)
        {
          p_bindingCtx.WebViewUrl = p_loadingPageUrl;
          return;
        }

        var serverAddress = p_prefs.GetValueOrDefault<string>(PREF_SERVER_ADDRESS);
        var roomId = p_prefs.GetValueOrDefault<string>(PREF_ROOM);
        var username = p_prefs.GetValueOrDefault<string>(PREF_USERNAME);
        var mapState = p_prefs.GetValueOrDefault(PREF_WEBAPP_MAP_STATE, JsBridgeJsonCtx.Default.HostMsgMapStateData);
        var url = GetMapAddress(serverAddress, roomId, mapState);
        if (url == null)
        {
          p_bindingCtx.WebViewUrl = p_loadingPageUrl;
          bindingCtx.IsRemoteServerNotResponding = true;
          return;
        }

        _ = MainThread.InvokeOnMainThreadAsync(() => Toast.Make($"{serverAddress}\n{roomId}/{username}", ToastDuration.Long).Show(_ct));

        try
        {
          using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
          using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, _ct);
          using var req = new HttpRequestMessage(HttpMethod.Get, url);
          using var res = await p_httpClient.Value.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);
          res.EnsureSuccessStatusCode();
          p_bindingCtx.WebViewUrl = url;
          bindingCtx.IsRemoteServerNotResponding = false;
        }
        catch (Exception ex)
        {
          p_log.Error($"Remote server probe returned error", ex);
          p_bindingCtx.WebViewUrl = p_loadingPageUrl;
          bindingCtx.IsRemoteServerNotResponding = true;
        }
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
            if (!await MainThread.InvokeOnMainThreadAsync(() => IsLocationPermissionOkAsync()))
              return;

            string[] providers;
            if (p_prefs.GetValueOrDefault<bool>(PREF_LOW_POWER_MODE))
              providers = [Android.Locations.LocationManager.NetworkProvider, Android.Locations.LocationManager.PassiveProvider];
            else
              providers = [Android.Locations.LocationManager.GpsProvider, Android.Locations.LocationManager.NetworkProvider];

            webAppLocationProvider.StartLocationWatcher(providers);
            _life.DoOnEnding(() => webAppLocationProvider.StopLocationWatcher());

            webAppLocationProvider.Location
              .Buffer(TimeSpan.FromSeconds(1))
              .CombineLatest(compassProvider.Values)
              .Sample(TimeSpan.FromMilliseconds(100))
              .SelectAsync(async (_tuple, _ct) =>
              {
                if (_ct.IsCancellationRequested)
                  return;

                var (locations, compassHeading) = _tuple;
                if (locations.Count == 0)
                  return;

                var loc = locations
                  .OrderBy(_ => _.Accuracy)
                  .First();

                try
                {
                  var lat = loc.Latitude.ToString(CultureInfo.InvariantCulture);
                  var lng = loc.Longitude.ToString(CultureInfo.InvariantCulture);
                  var acc = loc.Accuracy.ToString(CultureInfo.InvariantCulture);
                  var arc = loc.Course?.ToString(CultureInfo.InvariantCulture) ?? compassHeading?.ToString(CultureInfo.InvariantCulture) ?? "null";

                  await MainThread.InvokeOnMainThreadAsync(async () =>
                  {
                    var result = await p_webView.EvaluateJavaScriptAsync($"updateCurrentLocation2({lat},{lng},{acc},{arc})");
                    if (result == null)
                      p_log.Warn($"Can't send current location to web app: js code returned false");
                  });
                }
                catch (Exception ex)
                {
                  p_log.Error($"Can't send current location to web app: {ex}");
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
      OnWebAppMsgMapState(msg);
  }

  private async void FAB_Clicked(object _sender, EventArgs _e)
  {
    // privacy policy
    var serverAddress = p_prefs.GetValueOrDefault<string>(PREF_SERVER_ADDRESS);
    if (serverAddress != null && serverAddress.StartsWith(ROADNIK_APP_ADDRESS))
    {
      const int currentVersion = 3;
      var version = p_prefs.GetValueOrDefault<int>(PREF_PRIVACY_POLICY_VERSION);
      if (version < currentVersion)
      {
        var result = await this.ShowPopupAsync(new AgreementsPopup());
        if (result is not bool agreed || !agreed)
          return;

        p_prefs.SetValue(PREF_PRIVACY_POLICY_VERSION, currentVersion);
      }
    }

    // check permissions and run
    var locationReporter = Container.Locate<ILocationReporter>();

    if (!await locationReporter.IsEnabledAsync())
    {
      if (!await IsLocationPermissionOkAsync())
        return;

      await RequestIgnoreBattaryOptimizationAsync(p_lifetime.Token);
      locationReporter.SetState(true);
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
  }

  private async void GoToMyLocation_Clicked(object _sender, EventArgs _e)
  {
    if (!await IsLocationPermissionOkAsync())
      return;
    if (_sender is not Button button)
      return;

    button.IsEnabled = false;
    var animation = new Animation(_rotation => p_goToMyLocationImage.Rotation = _rotation, 0, 360);
    try
    {
      animation.Commit(p_goToMyLocationImage, "my-loc-anim", 16, 2000, null, null, () => true);

      var location = await AndroidLocationProvider.GetCurrentBestLocationAsync(TimeSpan.FromSeconds(10), default);
      if (location != null)
      {
        var lat = location.Latitude.ToString(CultureInfo.InvariantCulture);
        var lng = location.Longitude.ToString(CultureInfo.InvariantCulture);
        await p_webView.EvaluateJavaScriptAsync($"setLocation({lat},{lng})");
      }
    }
    finally
    {
      animation.Dispose();
      button.IsEnabled = true;
      p_goToMyLocationImage.Rotation = 0;
    }
  }

  private void LocationPermissionNo_Clicked(object _sender, EventArgs _e)
  {
    p_bindingCtx.IsPermissionWindowShowing = false;
  }

  private void LocationPermissionYes_Clicked(object _sender, EventArgs _e)
  {
    p_bindingCtx.IsPermissionWindowShowing = false;
    AppInfo.Current.ShowSettingsUI();
  }

  private void Reload_Clicked(object _sender, EventArgs _e)
  {
    p_bindingCtx.IsRemoteServerNotResponding = false;
    p_pageAppearedChangeFlow.OnNext(true);
  }

  private async void Share_Clicked(object _sender, EventArgs _e)
  {
    var serverAddress = p_prefs.GetValueOrDefault<string>(PREF_SERVER_ADDRESS);
    var roomId = p_prefs.GetValueOrDefault<string>(PREF_ROOM);
    var url = GetMapAddress(serverAddress, roomId);
    if (url == null)
    {
      await DisplayAlert("Server address or room id is invalid", null, "Ok");
      return;
    }

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
    var serverAddress = p_prefs.GetValueOrDefault<string>(PREF_SERVER_ADDRESS);
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
      p_log.ErrorJson($"Tried to create new point, but could not parse location!", _msg.Data);
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
      using var content = JsonContent.Create(new CreateNewPointReq(roomId, username, latLng.Lat, latLng.Lng, dialogResult));
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

  private void OnWebAppMsgMapState(JsToCSharpMsg _msg)
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
    }
    catch (Exception ex)
    {
      p_log.Error($"Cannot deserialize the data of webapp msg of type '{HOST_MSG_MAP_STATE}': {ex}");
    }
  }

  private async Task OnNotificationAsync(PushNotificationEvent _e, CancellationToken _ct)
  {
    if (_e.NotificationId == PUSH_MSG_NEW_POINT)
    {
      var data = _e.Data.Deserialize<LatLng>();
      if (data == default)
        return;

      var command = $"setLocation({data.Lat}, {data.Lng}, {15});";

      await MainThread.InvokeOnMainThreadAsync(async () =>
      {
        var result = await p_webView.EvaluateJavaScriptAsync(command);
        if (result == null)
          p_log.Error($"Command returned an error: '{command}'");
      });
    }
    else if (_e.NotificationId == PUSH_MSG_NEW_TRACK)
    {
      var username = _e.Data.Deserialize<string>();
      if (username == null)
        return;

      var command = $"setViewToTrack(\"{username}\", {13}) || setViewToAllTracks();";

      await MainThread.InvokeOnMainThreadAsync(async () =>
      {
        var result = await p_webView.EvaluateJavaScriptAsync(command);
        if (result == null)
          p_log.Error($"Command returned an error: '{command}'");
      });
    }
  }

  private void ShellOpen_Clicked(object sender, EventArgs e)
  {
    Shell.Current.FlyoutIsPresented = true;
  }

  private async Task<bool> IsLocationPermissionOkAsync()
  {
    var permission = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
    if (permission == PermissionStatus.Granted)
      return true;

    var osVersion = DeviceInfo.Current.Version;
    if (osVersion.Major < 11)
    {
      return await Permissions.RequestAsync<Permissions.LocationAlways>() == PermissionStatus.Granted;
    }
    else // if (Permissions.ShouldShowRationale<Permissions.LocationAlways>())
    {
      p_bindingCtx.IsPermissionWindowShowing = true;
      return false;
    }
    throw new NotImplementedException();
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

  private static string? GetMapAddress(
    string? _serverAddress,
    string? _roomId,
    HostMsgMapStateData? _mapState = null)
  {
    if (string.IsNullOrWhiteSpace(_serverAddress) || string.IsNullOrWhiteSpace(_roomId))
      return null;

    var urlBuilder = new UriBuilder(_serverAddress);
    urlBuilder.Path = "/r/";

    var query = HttpUtility.ParseQueryString(urlBuilder.Query);
    query["id"] = _roomId;
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
      query["selected_path_window_bottom"] = _mapState.SelectedPathWindowBottom.Value.ToString(CultureInfo.InvariantCulture); ;

    urlBuilder.Query = query.ToString();

    var url = urlBuilder.ToString();
    return url;
  }

}