using Android.OS;
using AndroidX.Core.App;
using Ax.Fw.Extensions;
using Ax.Fw.Pools;
using Ax.Fw.SharedTypes.Interfaces;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using QRCoder;
using Roadnik.Common.ReqRes;
using Roadnik.Common.Toolkit;
using Roadnik.MAUI.Controls;
using Roadnik.MAUI.Data;
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
using System.Text.Json;
using static Roadnik.MAUI.Data.Consts;

namespace Roadnik.MAUI.Pages;

public partial class MainPage : CContentPage
{
  private const string p_loadingPageUrl = "loading.html";
  private readonly IPreferencesStorage p_storage;
  private readonly IReadOnlyLifetime p_lifetime;
  private readonly IHttpClientProvider p_httpClient;
  private readonly ILogger p_log;
  private readonly IObservable<bool> p_pageIsVisible;
  private readonly Subject<bool> p_pageAppearedChangeFlow = new();
  private readonly Subject<bool> p_webAppTracksSynchonizedSubj = new();
  private readonly MainPageViewModel p_bindingCtx;

  public MainPage()
  {
    p_log = Container.Locate<ILogger>()["main-page"];
    p_log.Info($"Main page is opening...");

    InitializeComponent();

    var pageController = Container.Locate<IPagesController>();
    pageController.OnMainPage(this);

    p_storage = Container.Locate<IPreferencesStorage>();
    p_lifetime = Container.Locate<IReadOnlyLifetime>();
    p_httpClient = Container.Locate<IHttpClientProvider>();
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
      .Throttle(TimeSpan.FromSeconds(1))
      .Scan(false, (_acc, _tuple) =>
      {
        var (appeared, appWindowActivated) = _tuple;
        if (!appeared)
          return false;

        return appWindowActivated;
      });

    p_lifetime.ToDisposeOnEnded(SharedPool<EventLoopScheduler>.Get(out var scheduler));

    p_storage.PreferencesChanged
      .Select(_ =>
      {
        var serverAddress = p_storage.GetValueOrDefault<string>(PREF_SERVER_ADDRESS);
        var roomId = p_storage.GetValueOrDefault<string>(PREF_ROOM);

        return (serverAddress, roomId);
      })
      .DistinctUntilChanged(_ => HashCode.Combine(_.serverAddress, _.roomId))
      .Sample(TimeSpan.FromSeconds(1), scheduler)
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

        var serverAddress = p_storage.GetValueOrDefault<string>(PREF_SERVER_ADDRESS);
        var roomId = p_storage.GetValueOrDefault<string>(PREF_ROOM);
        var username = p_storage.GetValueOrDefault<string>(PREF_USERNAME);
        var url = ReqResUtil.GetMapAddress(serverAddress, roomId, null, null, null, null);
        if (url == null)
        {
          p_bindingCtx.WebViewUrl = p_loadingPageUrl;
          bindingCtx.IsRemoteServerNotResponding = true;
          return;
        }

        _ = MainThread.InvokeOnMainThreadAsync(() => Toast.Make($"{serverAddress}\n{roomId}/{username}", ToastDuration.Short).Show(_ct));

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

    var webAppLocationProvider = new AndroidLocationProvider(p_log, p_lifetime);
    p_webAppTracksSynchonizedSubj
      .CombineLatest(p_pageIsVisible)
      .HotAlive(p_lifetime, (_tuple, _life) =>
      {
        var (webAppTracksReady, pageIsVisible) = _tuple;
        if (!webAppTracksReady || !pageIsVisible)
          return;

        _ = Task.Run(async () =>
        {
          try
          {
            if (!await IsLocationPermissionOkAsync())
              return;

            var providers = new string[] { Android.Locations.LocationManager.GpsProvider, Android.Locations.LocationManager.NetworkProvider };
            webAppLocationProvider.StartLocationWatcher(providers, out _);
            _life.DoOnEnding(() => webAppLocationProvider.StopLocationWatcher());

            webAppLocationProvider.Location
              .Buffer(TimeSpan.FromSeconds(1))
              .SelectAsync(async (_locs, _ct) =>
              {
                if (_ct.IsCancellationRequested)
                  return;
                if (_locs.Count == 0)
                  return;

                var loc = _locs
                  .OrderBy(_ => _.Accuracy ?? 100d)
                  .First();

                try
                {
                  var lat = loc.Latitude.ToString(CultureInfo.InvariantCulture);
                  var lng = loc.Longitude.ToString(CultureInfo.InvariantCulture);
                  var acc = (loc.Accuracy ?? 100d).ToString(CultureInfo.InvariantCulture);

                  await MainThread.InvokeOnMainThreadAsync(async () =>
                  {
                    var result = await p_webView.EvaluateJavaScriptAsync($"updateCurrentLocation({lat},{lng},{acc})");
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
  }

  private async void FAB_Clicked(object _sender, EventArgs _e)
  {
    // privacy policy
    var serverAddress = p_storage.GetValueOrDefault<string>(PREF_SERVER_ADDRESS);
    if (serverAddress != null && serverAddress.StartsWith(ROADNIK_APP_ADDRESS))
    {
      const int currentVersion = 3;
      var version = p_storage.GetValueOrDefault<int>(PREF_PRIVACY_POLICY_VERSION);
      if (version < currentVersion)
      {
        var result = await this.ShowPopupAsync(new AgreementsPopup());
        if (result is not bool agreed || !agreed)
          return;

        p_storage.SetValue(PREF_PRIVACY_POLICY_VERSION, currentVersion);
      }
    }

    // check permissions and run
    var locationReporter = Container.Locate<ILocationReporter>();

    if (!await locationReporter.IsEnabledAsync())
    {
      if (!await IsLocationPermissionOkAsync())
        return;

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
    var serverAddress = p_storage.GetValueOrDefault<string>(PREF_SERVER_ADDRESS);
    var roomId = p_storage.GetValueOrDefault<string>(PREF_ROOM);
    var url = ReqResUtil.GetMapAddress(serverAddress, roomId, null, null, null, null);
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
    var serverAddress = p_storage.GetValueOrDefault<string>(PREF_SERVER_ADDRESS);
    if (serverAddress.IsNullOrWhiteSpace())
      return;

    var roomId = p_storage.GetValueOrDefault<string>(PREF_ROOM);
    if (roomId.IsNullOrWhiteSpace())
      return;

    var username = p_storage.GetValueOrDefault<string>(PREF_USERNAME);
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
}