using Roadnik.MAUI.ViewModels;
using System.Reactive.Linq;
using System.Reactive;
using Ax.Fw.Extensions;
using Roadnik.MAUI.Toolkit;
using Roadnik.MAUI.Interfaces;

namespace Roadnik.MAUI.Pages;

public partial class MainPage : ContainerizedContentPage
{
  private readonly IPreferencesStorage p_storage;

  public MainPage()
  {
    InitializeComponent();
    p_storage = Container.Locate<IPreferencesStorage>();

    Observable
      .Return(Unit.Default)
      .SelectAsync(async (_, _ct) =>
      {
        var permission = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
        if (permission != PermissionStatus.Granted)
          await Permissions.RequestAsync<Permissions.LocationAlways>();
      })
      .Subscribe();
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
    if (_sender is not Button button)
      return;

    var locationReporter = Container.Locate<ILocationReporter>();
    if (locationReporter != null)
    {
      locationReporter.SetState(!locationReporter.Enabled);
      if (locationReporter.Enabled)
        button.BackgroundColor = Color.Parse("OrangeRed");
      else
        button.BackgroundColor = Color.Parse("CornflowerBlue");
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
}