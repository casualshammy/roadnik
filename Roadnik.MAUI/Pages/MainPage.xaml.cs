using Ax.Fw;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using JustLogger.Interfaces;
using Newtonsoft.Json;
using Roadnik.Common.Toolkit;
using Roadnik.Data;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.Toolkit;
using Roadnik.MAUI.ViewModels;
using System.Diagnostics;
using System.Globalization;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Roadnik.MAUI.Pages;

public partial class MainPage : CContentPage
{
  private const string PREF_WEB_APP_STATE = "web-app-state";
  private readonly IPreferencesStorage p_storage;
  private readonly IReadOnlyLifetime p_lifetime;
  private readonly IHttpClientProvider p_httpClient;
  private readonly ILogger p_log;
  private readonly Subject<bool> p_pageVisibleChangeFlow = new();
  private readonly MainPageViewModel p_bindingCtx;
  private volatile bool p_pageShown = false;
  private volatile WebAppState? p_webAppState;

  public MainPage()
  {
    InitializeComponent();
    p_storage = Container.Locate<IPreferencesStorage>();
    p_lifetime = Container.Locate<IReadOnlyLifetime>();
    p_httpClient = Container.Locate<IHttpClientProvider>();
    p_log = Container.Locate<ILogger>()["main-page"];

    p_webAppState = p_storage.GetValueOrDefault<WebAppState>(PREF_WEB_APP_STATE);

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
      .Merge(p_pageVisibleChangeFlow.ToUnit())
      .ObserveOn(scheduler)
      .SelectAsync(async (_, _ct) =>
      {
        bindingCtx.IsInBackground = !p_pageShown;
        if (!p_pageShown)
        {
          await SaveWebViewStateAndNavigateAsync("loading.html");
          return;
        }

        var url = GetFullServerUrl();
        if (url == null)
        {
          await SaveWebViewStateAndNavigateAsync("loading.html");
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
          NavigateAndSetMapStateAsync(url);
          bindingCtx.IsRemoteServerNotResponding = false;
        }
        catch (Exception ex)
        {
          Debug.WriteLine(ex);
          await SaveWebViewStateAndNavigateAsync("loading.html");
          bindingCtx.IsRemoteServerNotResponding = true;
        }
      }, scheduler)
      .Subscribe(p_lifetime);

    var saveWebStateFlow = Observable
      .Interval(TimeSpan.FromSeconds(1), scheduler)
      .ObserveOn(scheduler)
      .SelectAsync(async (_, _ct) => await SaveWebViewStateAndNavigateAsync(null), scheduler);

    p_pageVisibleChangeFlow
      .Scan((ILifetime?)null, (_acc, _isPageVisible) =>
      {
        if (_isPageVisible)
        {
          if (_acc != null)
            return _acc;

          var life = p_lifetime.GetChildLifetime();
          if (life == null)
            return _acc;

          //saveWebStateFlow.Subscribe(life);
          return life;
        }
        else
        {
          if (_acc == null)
            return _acc;

          _acc.Complete();
          return null;
        }
      })
      .Subscribe(p_lifetime);
  }

  protected override void OnAppearing()
  {
    base.OnAppearing();
    p_pageShown = true;
    p_pageVisibleChangeFlow.OnNext(true);
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();
    p_pageShown = false;
    p_pageVisibleChangeFlow.OnNext(false);
  }

  private async Task SaveWebViewStateAndNavigateAsync(string? _url)
  {
    try
    {
      await MainThread.InvokeOnMainThreadAsync(async () =>
      {
        var jsonRaw = await p_webView.EvaluateJavaScriptAsync("getState();");
        if (jsonRaw == null)
        {
          p_log.Warn($"Can't get map state!");
          return;
        }
        var mapState = JsonConvert.DeserializeObject<WebAppState>(jsonRaw);
        if (mapState == null)
        {
          p_log.Error($"Can't parse map state!");
          return;
        }
        p_webAppState = mapState;
        p_storage.SetValue(PREF_WEB_APP_STATE, p_webAppState);
      });
    }
    finally
    {
      if (_url != null)
        p_bindingCtx.WebViewUrl = _url;
    }
  }

  private void NavigateAndSetMapStateAsync(string _url)
  {
    try
    {
      if (p_webAppState == null)
        return;

      async void navigated(object? _sender, WebNavigatedEventArgs _e)
      {
        p_webView.Navigated -= navigated;

        if (_e.Result != WebNavigationResult.Success)
          return;

        var command = $"setState({Serialization.SerializeToCamelCaseJson(p_webAppState)});";
        await MainThread.InvokeOnMainThreadAsync(() => p_webView.EvaluateJavaScriptAsync(command));
      }

      p_webView.Navigated += navigated;
      Observable
        .Timer(TimeSpan.FromSeconds(30))
        .Subscribe(_ => p_webView.Navigated -= navigated, p_lifetime);
    }
    finally
    {
      p_bindingCtx.WebViewUrl = _url;
    }
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
    var serverAddress = p_storage.GetValueOrDefault<string>(p_storage.SERVER_ADDRESS);
    var serverKey = p_storage.GetValueOrDefault<string>(p_storage.SERVER_KEY);
    if (string.IsNullOrWhiteSpace(serverAddress) || string.IsNullOrWhiteSpace(serverKey))
      return null;

    var url = $"{serverAddress}?key={serverKey}";
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
  }

  private void MainWebView_Navigated(object _sender, WebNavigatedEventArgs _e)
  {
    if (_e.Result != WebNavigationResult.Success)
      p_log.Warn($"WebView navigation error '{_e.Result}': {_e.Url}");

    p_bindingCtx.IsSpinnerRequired = false;
  }

  private async void GoToMyLocation_Clicked(object _sender, EventArgs _e)
  {
    var locationReporter = Container.Locate<ILocationReporter>();
    var location = await locationReporter.GetCurrentAnyLocationAsync(TimeSpan.FromSeconds(3), default);
    if (location != null)
    {
      var lat = location.Latitude.ToString(CultureInfo.InvariantCulture);
      var lng = location.Longitude.ToString(CultureInfo.InvariantCulture);
      await p_webView.EvaluateJavaScriptAsync($"setLocation({lat},{lng})");
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
    p_pageVisibleChangeFlow.OnNext(true);
  }

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
      initialValue: p_storage.GetValueOrDefault<string>(p_storage.USER_MSG));

    if (msg == null)
      return;

    p_storage.SetValue(p_storage.USER_MSG, ReqResUtil.ClearUserMsg(msg));
  }

}