using Ax.Fw;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using JustLogger.Interfaces;
using QRCoder;
using Roadnik.Common.ReqRes;
using Roadnik.Common.Toolkit;
using Roadnik.MAUI.Controls;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.Toolkit;
using Roadnik.MAUI.ViewModels;
using System.Globalization;
using System.Net.Http.Json;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using static Roadnik.MAUI.Data.Consts;

namespace Roadnik.MAUI.Pages;

public partial class MainPage : CContentPage
{
  private const string p_loadingPageUrl = "loading.html";
  private readonly IPreferencesStorage p_storage;
  private readonly IReadOnlyLifetime p_lifetime;
  private readonly IHttpClientProvider p_httpClient;
  private readonly ILogger p_log;
  private readonly Subject<bool> p_pageVisibleChangeFlow = new();
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

    if (BindingContext is not MainPageViewModel bindingCtx)
    {
      p_log.Error($"Can't get binding ctx!");
      throw new InvalidDataException($"Can't get binding ctx!");
    }

    p_bindingCtx = bindingCtx;

    p_lifetime.ToDisposeOnEnded(Pool<EventLoopScheduler>.Get(out var scheduler));

    p_storage.PreferencesChanged
      .Select(_ =>
      {
        var serverAddress = p_storage.GetValueOrDefault<string>(PREF_SERVER_ADDRESS);
        var roomId = p_storage.GetValueOrDefault<string>(PREF_ROOM);

        return (serverAddress, roomId);
      })
      .DistinctUntilChanged(_ => HashCode.Combine(_.serverAddress, _.roomId))
      .Sample(TimeSpan.FromSeconds(1), scheduler)
      .CombineLatest(p_pageVisibleChangeFlow)
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
        var url = ReqResUtil.GetMapAddress(serverAddress, roomId);
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
          using var req = new HttpRequestMessage(HttpMethod.Head, url);
          using var res = await p_httpClient.Value.SendAsync(req, linkedCts.Token);
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

    p_lifetime.ToDisposeOnEnded(Pool<EventLoopScheduler>.Get(out var webAppDataScheduler));

    p_webView.JsonData
      .ObserveOn(webAppDataScheduler)
      .WithLatestFrom(p_pageVisibleChangeFlow)
      .SelectAsync(OnMsgFromWebAppAsync)
      .Subscribe(p_lifetime);

    p_log.Info($"Main page is opened");
  }

  protected override void OnAppearing()
  {
    base.OnAppearing();
    p_pageVisibleChangeFlow.OnNext(true);
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();
    p_pageVisibleChangeFlow.OnNext(false);
  }

  private async Task<bool> IsLocationPermissionOkAsync()
  {
    var permission = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
    if (permission == PermissionStatus.Granted)
      return true;

    var platform = DeviceInfo.Platform;
    var osVersion = DeviceInfo.Current.Version;
    if (platform == DevicePlatform.Android)
    {
      if (osVersion.Major < 11)
      {
        return (await Permissions.RequestAsync<Permissions.LocationAlways>() == PermissionStatus.Granted);
      }
      else if (Permissions.ShouldShowRationale<Permissions.LocationAlways>())
      {
        p_bindingCtx.IsPermissionWindowShowing = true;
        return false;
      }
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

    if (msg.MsgType == JS_TO_CSHARP_MSG_TYPE_APP_LOADED)
    {
      var layer = p_storage.GetValueOrDefault<string>(PREF_MAP_LAYER);
      if (layer == null)
        return;

      var webAppState = p_storage.GetValueOrDefault<MapViewState>(PREF_MAP_VIEW_STATE);

      var command = "";
      if (webAppState != null)
        command += $"setLocation({webAppState.Location.Lat}, {webAppState.Location.Lng}, {webAppState.Zoom});";

      command += $"setMapLayer({Serialization.SerializeToCamelCaseJson(layer)});";

      await MainThread.InvokeOnMainThreadAsync(async () =>
      {
        var result = await p_webView.EvaluateJavaScriptAsync(command);
        if (result == null)
          p_log.Error($"Commands returned an error: '{command}'");
      });
    }
    else if (msg.MsgType == JS_TO_CSHARP_MSG_TYPE_MAP_LAYER_CHANGED)
    {
      var layer = msg.Data.ToObject<string>();
      if (layer == null)
        return;

      p_storage.SetValue(PREF_MAP_LAYER, layer);
    }
    else if (msg.MsgType == JS_TO_CSHARP_MSG_TYPE_MAP_LOCATION_CHANGED)
    {
      var mapViewState = msg.Data.ToObject<MapViewState>();
      if (mapViewState == null)
        return;

      p_storage.SetValue(PREF_MAP_VIEW_STATE, mapViewState);
    }
    else if (msg.MsgType == JS_TO_CSHARP_MSG_TYPE_INITIAL_DATA_RECEIVED)
    {
      var webAppState = p_storage.GetValueOrDefault<MapViewState>(PREF_MAP_VIEW_STATE);
      if (webAppState == null)
        return;

      var mapOpenBehavior = p_storage.GetValueOrDefault<MapOpeningBehavior>(PREF_MAP_OPEN_BEHAVIOR);
      var lastTrackedRoute = p_storage.GetValueOrDefault<string>(PREF_MAP_SELECTED_TRACK);
      var command = mapOpenBehavior switch
      {
        MapOpeningBehavior.LastPosition => $"setLocation({webAppState.Location.Lat}, {webAppState.Location.Lng}, {webAppState.Zoom});",
        MapOpeningBehavior.AllTracks => $"setViewToAllTracks();",
        MapOpeningBehavior.LastTrackedRoute => lastTrackedRoute != null ? $"setViewToTrack(\"{lastTrackedRoute}\", {webAppState.Zoom}) || setViewToAllTracks();" : $"setViewToAllTracks();",
        _ => $"setViewToAllTracks();"
      };

      if (command == null)
        return;

      await MainThread.InvokeOnMainThreadAsync(async () =>
      {
        var result = await p_webView.EvaluateJavaScriptAsync(command);
        if (result == null)
          p_log.Error($"Commands returned an error: '{command}'");
      });
    }
    else if (msg.MsgType == JS_TO_CSHARP_MSG_TYPE_POPUP_OPENED)
    {
      p_storage.SetValue(PREF_MAP_SELECTED_TRACK, msg.Data.ToObject<string>());
    }
    else if (msg.MsgType == JS_TO_CSHARP_MSG_TYPE_POPUP_CLOSED)
    {
      p_storage.SetValue(PREF_MAP_SELECTED_TRACK, (string?)null);
    }
    else if (msg.MsgType == JS_TO_CSHARP_MSG_TYPE_WAYPOINT_ADD_STARTED)
    {
      await OnJsMsgPointAddStartedAsync(msg);
    }
  }

  private async void FAB_Clicked(object _sender, EventArgs _e)
  {
    var locationReporter = Container.Locate<ILocationReporter>();
    var locationReporterService = Container.Locate<ILocationReporterService>();

    if (!await locationReporter.IsEnabledAsync())
    {
      if (!await IsLocationPermissionOkAsync())
        return;

      if (Application.Current?.Resources.TryGetValue("DangerLowBrush", out var rawBrush) == true && rawBrush is Brush brush)
        p_startRecordButton.Background = brush;
      else
        p_log.Error($"Resource 'DangerLowBrush' is not found!");

      _ = Task.Run(SendStartNewPathReqAsync);
      locationReporterService.Start();
    }
    else
    {
      if (Application.Current?.Resources.TryGetValue("PrimaryBrush", out var rawBrush) == true && rawBrush is Brush brush)
        p_startRecordButton.Background = brush;
      else
        p_log.Error($"Resource 'PrimaryBrush' is not found!");

      locationReporterService.Stop();
    }
  }

  private void MainWebView_Navigating(object _sender, WebNavigatingEventArgs _e) => p_bindingCtx.IsSpinnerRequired = true;

  private void MainWebView_Navigated(object _sender, WebNavigatedEventArgs _e)
  {
    if (_e.Result != WebNavigationResult.Success)
      p_log.Warn($"WebView navigation error '{_e.Result}': {_e.Url}");

    p_bindingCtx.IsSpinnerRequired = false;
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

      var locationProvider = Container.Locate<ILocationProvider>();
      var location = await locationProvider.GetCurrentBestLocationAsync(TimeSpan.FromSeconds(10), default);
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
    p_pageVisibleChangeFlow.OnNext(true);
  }

  private async void Share_Clicked(object _sender, EventArgs _e)
  {
    var serverAddress = p_storage.GetValueOrDefault<string>(PREF_SERVER_ADDRESS);
    var roomId = p_storage.GetValueOrDefault<string>(PREF_ROOM);
    var url = ReqResUtil.GetMapAddress(serverAddress, roomId);
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

  private async void Message_Clicked(object _sender, EventArgs _e)
  {
    var msg = await DisplayPromptAsync(
      "Message:",
      "Some special characters are not allowed",
      maxLength: ReqResUtil.MaxUserMsgLength,
      initialValue: p_storage.GetValueOrDefault<string>(PREF_USER_MSG));

    if (msg == null)
      return;

    p_storage.SetValue(PREF_USER_MSG, ReqResUtil.ClearUserMsg(msg));
  }

  private async Task SendStartNewPathReqAsync()
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

    var wipeOldTrack = p_storage.GetValueOrDefault<bool>(PREF_WIPE_OLD_TRACK_ON_NEW_ENABLED);

    try
    {
      using var req = new HttpRequestMessage(HttpMethod.Post, $"{serverAddress.TrimEnd('/')}{ReqPaths.START_NEW_PATH}");
      using var content = JsonContent.Create(new StartNewPathReq(roomId, username, wipeOldTrack));
      req.Content = content;
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
      using var res = await p_httpClient.Value.SendAsync(req, cts.Token);
      res.EnsureSuccessStatusCode();
      p_log.Info($"Sent request to start new track '{roomId}/{username}'");
    }
    catch (Exception ex)
    {
      p_log.Error($"Request to start new path '{roomId}/{username}' was completed with error", ex);
    }
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

    var latLng = _msg.Data.ToObject<LatLng>();
    if (latLng == null)
    {
      p_log.Error($"Tried to create new point, but could not parse location!\n{_msg.Data.ToString(Newtonsoft.Json.Formatting.Indented)}");
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

}