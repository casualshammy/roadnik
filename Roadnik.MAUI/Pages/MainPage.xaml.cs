using Ax.Fw;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using JustLogger;
using JustLogger.Interfaces;
using Microsoft.Maui.Devices.Sensors;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Roadnik.Common.Toolkit;
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
  record LatLon(double Lat, double Lng);
  record WebAppState(LatLon Location, bool AutoPan, string? MapLayer, int Zoom);

  private readonly IPreferencesStorage p_storage;
  private readonly IReadOnlyLifetime p_lifetime;
  private readonly IHttpClientProvider p_httpClient;
  private readonly ILogger p_log;
  private readonly Subject<Unit> p_pageStatusChangeFlow = new();
  private volatile bool p_pageShown = false;
  private volatile WebAppState? p_webAppState;

  public MainPage()
  {
    InitializeComponent();
    p_storage = Container.Locate<IPreferencesStorage>();
    p_lifetime = Container.Locate<IReadOnlyLifetime>();
    p_httpClient = Container.Locate<IHttpClientProvider>();
    p_log = Container.Locate<ILogger>()["main-page"];

    Observable
      .Return(Unit.Default)
      .SelectAsync(async (_, _ct) => await IsLocationPermissionOkAsync())
      .Subscribe(p_lifetime);

    p_lifetime.DisposeOnCompleted(Pool<EventLoopScheduler>.Get(out var scheduler));

    p_storage.PreferencesChanged
      .Sample(TimeSpan.FromSeconds(1), scheduler)
      .Merge(p_pageStatusChangeFlow)
      .ObserveOn(scheduler)
      .SelectAsync(async (_, _ct) =>
      {
        if (BindingContext is not MainPageViewModel bindingCtx)
          return;

        bindingCtx.IsInBackground = !p_pageShown;
        if (!p_pageShown)
        {
          await SaveWebViewStateAndShowLoadingPageAsync("loading.html");
          return;
        }

        var url = GetFullServerUrl();
        if (url == null)
        {
          await SaveWebViewStateAndShowLoadingPageAsync("loading.html");
          bindingCtx.IsRemoteServerNotResponding = true;
          return;
        }

        try
        {
          using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
          using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, _ct);
          using var req = new HttpRequestMessage(HttpMethod.Head, url);
          var res = await p_httpClient.Value.SendAsync(req, linkedCts.Token);
          res.EnsureSuccessStatusCode();
          SetMapStateAsync(url);
          bindingCtx.IsRemoteServerNotResponding = false;
        }
        catch (Exception ex)
        {
          Debug.WriteLine(ex);
          await SaveWebViewStateAndShowLoadingPageAsync("loading.html");
          bindingCtx.IsRemoteServerNotResponding = true;
        }
      }, scheduler)
      .Subscribe(p_lifetime);
  }

  protected override void OnAppearing()
  {
    base.OnAppearing();
    p_pageShown = true;
    p_pageStatusChangeFlow.OnNext();
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();
    p_pageShown = false;
    p_pageStatusChangeFlow.OnNext();
  }

  private async Task SaveWebViewStateAndShowLoadingPageAsync(string _url)
  {
    if (BindingContext is not MainPageViewModel bindingCtx)
      return;

    try
    {
      await MainThread.InvokeOnMainThreadAsync(async () =>
      {
        var jsonRaw = await p_webView.EvaluateJavaScriptAsync("getState();");
        if (jsonRaw == null)
        {
          p_log.Error($"Can't get map state!");
          return;
        }
        var mapState = JsonConvert.DeserializeObject<WebAppState>(jsonRaw);
        if (mapState == null)
        {
          p_log.Error($"Can't parse map state!");
          return;
        }
        p_webAppState = mapState;
      });
    }
    finally
    {
      bindingCtx.WebViewUrl = _url;
    }
  }

  private void SetMapStateAsync(string _url)
  {
    if (BindingContext is not MainPageViewModel bindingCtx)
      return;

    try
    {
      if (p_webAppState == null)
        return;

      async void navigated(object? _sender, WebNavigatedEventArgs _e)
      {
        p_webView.Navigated -= navigated;

        if (_e.Result != WebNavigationResult.Success)
          return;

        var jsonSerializer = new JsonSerializerSettings()
        {
          ContractResolver = new DefaultContractResolver
          {
            NamingStrategy = new CamelCaseNamingStrategy()
          },
          Formatting = Formatting.Indented
        };
        var command = $"setState({JsonConvert.SerializeObject(p_webAppState, jsonSerializer)});";
        await MainThread.InvokeOnMainThreadAsync(() => p_webView.EvaluateJavaScriptAsync(command));
      }

      p_webView.Navigated += navigated;
      Observable
        .Timer(TimeSpan.FromSeconds(30))
        .Subscribe(_ => p_webView.Navigated -= navigated, p_lifetime);
    }
    finally
    {
      bindingCtx.WebViewUrl = _url;
    }
  }

  private async Task<bool> IsLocationPermissionOkAsync()
  {
    var permission = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
    if (permission == PermissionStatus.Granted)
      return true;

    if (BindingContext is not MainPageViewModel bindingCtx)
    {
      p_log.Error($"{nameof(IsLocationPermissionOkAsync)}: error obtaining binding ctx!");
      throw new InvalidOperationException($"{nameof(IsLocationPermissionOkAsync)}: error obtaining binding ctx!");
    }

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
        bindingCtx.IsPermissionWindowShowing = true;
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

    var url = $"{serverAddress.TrimEnd('/')}?key={serverKey}";
    return url;
  }

  private async void FAB_Clicked(object _sender, EventArgs _e)
  {
    var ctx = (MainPageViewModel)BindingContext;

    var locationReporter = Container.Locate<ILocationReporter>();
    var locationReporterService = Container.Locate<ILocationReporterService>();

    if (!await locationReporter.IsEnabled())
    {
      if (!await IsLocationPermissionOkAsync())
        return;

      if (Application.Current?.Resources.TryGetValue("DangerLow", out var rawColor) == true && rawColor is Color color)
        ctx.StartRecordButtonColor = color;

      locationReporterService.Start();
    }
    else
    {
      if (Application.Current?.Resources.TryGetValue("Primary", out var rawColor) == true && rawColor is Color color)
        ctx.StartRecordButtonColor = color;

      locationReporterService.Stop();
    }
  }

  private void MainWebView_Navigating(object _sender, WebNavigatingEventArgs _e)
  {
    if (BindingContext is not MainPageViewModel bindingCtx)
      return;

    bindingCtx.IsSpinnerRequired = true;
  }

  private void MainWebView_Navigated(object _sender, WebNavigatedEventArgs _e)
  {
    if (BindingContext is not MainPageViewModel bindingCtx)
      return;

    if (_e.Result != WebNavigationResult.Success)
      p_log.Warn($"WebView navigation error '{_e.Result}': {_e.Url}");

    bindingCtx.IsSpinnerRequired = false;
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
    if (BindingContext is not MainPageViewModel bindingCtx)
      return;

    bindingCtx.IsPermissionWindowShowing = false;
  }

  private void LocationPermissionYes_Clicked(object _sender, EventArgs _e)
  {
    if (BindingContext is not MainPageViewModel bindingCtx)
      return;

    bindingCtx.IsPermissionWindowShowing = false;
    AppInfo.Current.ShowSettingsUI();
  }

  private void Reload_Clicked(object _sender, EventArgs _e)
  {
    p_pageStatusChangeFlow.OnNext();
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