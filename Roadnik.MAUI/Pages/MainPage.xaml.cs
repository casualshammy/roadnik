using Ax.Fw;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using JustLogger.Interfaces;
using Roadnik.Common.Toolkit;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.Toolkit;
using Roadnik.MAUI.ViewModels;
using System.Diagnostics;
using System.Globalization;
using System.Reactive;
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
  private volatile bool p_webViewReadyToJsSubs = false;

  public MainPage()
  {
    InitializeComponent();
    p_storage = Container.Locate<IPreferencesStorage>();
    p_lifetime = Container.Locate<IReadOnlyLifetime>();
    p_httpClient = Container.Locate<IHttpClientProvider>();
    p_log = Container.Locate<ILogger>()["main-page"];

    if (BindingContext is not MainPageViewModel bindingCtx)
    {
      p_log.Error($"Can't get binding ctx!");
      throw new InvalidDataException($"Can't get binding ctx!");
    }

    p_bindingCtx = bindingCtx;

    Observable
      .Return(Unit.Default)
      .SelectAsync(async (_, _ct) => await IsLocationPermissionOkAsync())
      .Subscribe(p_lifetime);

    p_lifetime.DisposeOnCompleted(Pool<EventLoopScheduler>.Get(out var scheduler));

    p_storage.PreferencesChanged
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

        var url = GetFullServerUrl();
        if (url == null)
        {
          p_bindingCtx.WebViewUrl = p_loadingPageUrl;
          bindingCtx.IsRemoteServerNotResponding = true;
          return;
        }

        try
        {
          using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
          using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, _ct);
          using var req = new HttpRequestMessage(HttpMethod.Head, url);
          using var res = await p_httpClient.Value.SendAsync(req, linkedCts.Token);
          res.EnsureSuccessStatusCode();
          p_bindingCtx.WebViewUrl = url;
          bindingCtx.IsRemoteServerNotResponding = false;
        }
        catch (Exception ex)
        {
          Debug.WriteLine(ex);
          p_bindingCtx.WebViewUrl = p_loadingPageUrl;
          bindingCtx.IsRemoteServerNotResponding = true;
        }
      }, scheduler)
      .Subscribe(p_lifetime);

    p_lifetime.DisposeOnCompleted(Pool<EventLoopScheduler>.Get(out var webViewReadyForJsSubsScheduler));

    p_webView.JsonData
      .ObserveOn(webViewReadyForJsSubsScheduler)
      .Where(_ => p_webViewReadyToJsSubs)
      .DistinctUntilChanged(_ => _.First)
      .Sample(TimeSpan.FromSeconds(1), webViewReadyForJsSubsScheduler)
      .Select(_jToken =>
      {
        if (_jToken == null)
          return;

        var webAppState = _jToken.ToObject<WebAppState>();
        if (webAppState == null)
          return;

        p_storage.SetValue(PREF_WEB_APP_STATE, webAppState);
      })
      .Subscribe(p_lifetime);
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

  private string? GetFullServerUrl()
  {
    var serverAddress = p_storage.GetValueOrDefault<string>(PREF_SERVER_ADDRESS);
    var serverKey = p_storage.GetValueOrDefault<string>(PREF_SERVER_KEY);
    if (string.IsNullOrWhiteSpace(serverAddress) || string.IsNullOrWhiteSpace(serverKey))
      return null;

    var url = $"{serverAddress.TrimEnd('/')}/?key={serverKey}";
    return url;
  }

  private async void FAB_Clicked(object _sender, EventArgs _e)
  {
    var locationReporter = Container.Locate<ILocationReporter>();
    var locationReporterService = Container.Locate<ILocationReporterService>();

    if (!await locationReporter.IsEnabled())
    {
      if (!await IsLocationPermissionOkAsync())
        return;

      if (Application.Current?.Resources.TryGetValue("DangerLowBrush", out var rawBrush) == true && rawBrush is Brush brush)
        p_startRecordButton.Background = brush;
      else
        p_log.Error($"Resource 'DangerLowBrush' is not found!");

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

  private void MainWebView_Navigating(object _sender, WebNavigatingEventArgs _e)
  {
    p_bindingCtx.IsSpinnerRequired = true;
    p_webViewReadyToJsSubs = false;
  }

  private async void MainWebView_Navigated(object _sender, WebNavigatedEventArgs _e)
  {
    if (_e.Result != WebNavigationResult.Success)
      p_log.Warn($"WebView navigation error '{_e.Result}': {_e.Url}");

    p_bindingCtx.IsSpinnerRequired = false;

    if (_e.Url.EndsWith(p_loadingPageUrl) || _e.Url == "about:blank")
      return;

    var webAppState = p_storage.GetValueOrDefault<WebAppState>(PREF_WEB_APP_STATE);
    if (webAppState == null)
    {
      p_webViewReadyToJsSubs = true;
      return;
    }

    var command = $"setState({Serialization.SerializeToCamelCaseJson(webAppState)});";
    await MainThread.InvokeOnMainThreadAsync(async () =>
    {
      var result = await p_webView.EvaluateJavaScriptAsync(command);
      if (result != null)
        p_webViewReadyToJsSubs = true;
    });
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

  private void Reload_Clicked(object _sender, EventArgs _e) => p_pageVisibleChangeFlow.OnNext(true);

  private async void Share_Clicked(object _sender, EventArgs _e)
  {
    var url = GetFullServerUrl();
    if (url == null)
    {
      await DisplayAlert("Server address or server key is invalid", null, "Ok");
      return;
    }

    var req = new ShareTextRequest(url, "Url");
    await Share.Default.RequestAsync(req);
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

}