using Ax.Fw;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
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
  private readonly IPreferencesStorage p_storage;
  private readonly IReadOnlyLifetime p_lifetime;
  private readonly IHttpClientProvider p_httpClient;
  private readonly Subject<Unit> p_pageStatusChangeFlow = new();
  private volatile bool p_pageShown = false;

  public MainPage()
  {
    InitializeComponent();
    p_storage = Container.Locate<IPreferencesStorage>();
    p_lifetime = Container.Locate<IReadOnlyLifetime>();
    p_httpClient = Container.Locate<IHttpClientProvider>();

    Observable
      .Return(Unit.Default)
      .SelectAsync(async (_, _ct) => await RequestLocationPermissionAsync())
      .Subscribe(p_lifetime);

    p_lifetime.DisposeOnCompleted(Pool<EventLoopScheduler>.Get(out var scheduler));

    p_storage.PreferencesChanged
      .Merge(p_pageStatusChangeFlow)
      .Sample(TimeSpan.FromSeconds(1), scheduler)
      .ObserveOn(scheduler)
      .SelectAsync(async (_, _ct) =>
      {
        if (BindingContext is not MainPageViewModel bindingCtx)
          return;

        bindingCtx.IsInBackground = !p_pageShown;
        if (!p_pageShown)
        {
          bindingCtx.WebViewUrl = "__blank";
          return;
        }

        var url = GetFullServerUrl();
        if (url == null)
        {
          bindingCtx.WebViewUrl = "__blank";
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
          bindingCtx.WebViewUrl = url;
          bindingCtx.IsRemoteServerNotResponding = false;
        }
        catch (Exception ex)
        {
          Debug.WriteLine(ex);
          bindingCtx.WebViewUrl = "__blank";
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

  private async Task RequestLocationPermissionAsync()
  {
    var permission = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
    if (permission == PermissionStatus.Granted)
      return;

    if (BindingContext is not MainPageViewModel bindingCtx)
      return;

    var platform = DeviceInfo.Platform;
    var osVersion = DeviceInfo.Current.Version;
    if (platform == DevicePlatform.Android)
    {
      if (osVersion.Major < 11)
        await Permissions.RequestAsync<Permissions.LocationAlways>();
      else if (Permissions.ShouldShowRationale<Permissions.LocationAlways>())
        bindingCtx.IsPermissionWindowShowing = true;
    }
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

  private void FAB_Clicked(object _sender, EventArgs _e)
  {
    var ctx = (MainPageViewModel)BindingContext;

    var locationReporter = Container.Locate<ILocationReporter>();
    var locationReporterService = Container.Locate<ILocationReporterService>();

    if (locationReporter.Enabled)
    {
      if (Application.Current?.Resources.TryGetValue("Primary", out var rawColor) == true && rawColor is Color color)
        ctx.StartRecordButtonColor = color;

      locationReporterService.Stop();
    }
    else
    {
      if (Application.Current?.Resources.TryGetValue("DangerLow", out var rawColor) == true && rawColor is Color color)
        ctx.StartRecordButtonColor = color;

      locationReporterService.Start();
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

  private void LocationPermissionYes_Clicked(object sender, EventArgs e)
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

}