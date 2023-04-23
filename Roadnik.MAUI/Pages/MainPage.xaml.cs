using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.Toolkit;
using Roadnik.MAUI.ViewModels;
using System.Globalization;
using System.Reactive;
using System.Reactive.Linq;

namespace Roadnik.MAUI.Pages;

public partial class MainPage : CContentPage
{
  private readonly IPreferencesStorage p_storage;
  private readonly IReadOnlyLifetime p_lifetime;

  public MainPage()
  {
    InitializeComponent();
    p_storage = Container.Locate<IPreferencesStorage>();
    p_lifetime = Container.Locate<IReadOnlyLifetime>();

    Observable
      .Return(Unit.Default)
      .SelectAsync(async (_, _ct) =>
      {
        var permission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (permission != PermissionStatus.Granted)
          await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
      })
      .Subscribe(p_lifetime);
  }

  protected override void OnAppearing()
  {
    base.OnAppearing();

    if (BindingContext is not MainPageViewModel bindingCtx)
      return;

    var serverAddress = p_storage.GetValueOrDefault<string>(p_storage.SERVER_ADDRESS);
    var serverKey = p_storage.GetValueOrDefault<string>(p_storage.SERVER_KEY);

    string url;
    if (string.IsNullOrWhiteSpace(serverAddress))
      url = "https://github.com/casualshammy";
    else if (string.IsNullOrWhiteSpace(serverKey))
      url = serverAddress;
    else
      url = $"{serverAddress}?key={serverKey}";

    bindingCtx.WebViewUrl = url;
  }

  private void FAB_Clicked(object _sender, EventArgs _e)
  {
    var ctx = (MainPageViewModel)BindingContext;

    var locationReporter = Container.Locate<ILocationReporter>();
    var locationReporterService = Container.Locate<ILocationReporterService>();
    if (locationReporter != null)
    {
      locationReporter.SetState(!locationReporter.Enabled);
      if (locationReporter.Enabled)
      {
        ctx.StartRecordButtonColor = Color.Parse("OrangeRed");
        locationReporterService.Start();
      }
      else
      {
        ctx.StartRecordButtonColor = Color.Parse("CornflowerBlue");
        locationReporterService.Stop();
      }
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
    var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
    var location = await Geolocation.GetLocationAsync(request);
    if (location != null)
      await p_webView.EvaluateJavaScriptAsync($"setLocation({location.Latitude.ToString(CultureInfo.InvariantCulture)},{location.Longitude.ToString(CultureInfo.InvariantCulture)})");
  }
}