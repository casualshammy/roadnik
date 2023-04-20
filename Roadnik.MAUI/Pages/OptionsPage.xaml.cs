using Roadnik.MAUI.ViewModels;

namespace Roadnik.MAUI.Pages;

public partial class OptionsPage : ContentPage
{
  public OptionsPage()
  {
    InitializeComponent();
  }

  private async void ServerAddress_Tapped(object _sender, EventArgs _e)
  {
    var bindingCtx = (OptionsPageViewModel)BindingContext;
    var serverName = await DisplayPromptAsync("Server address:", "", "Save", placeholder: "http://example.com:5544/", initialValue: bindingCtx.ServerName);
    if (serverName == null)
      return;

    bindingCtx.ServerName = serverName;
  }

  private async void ServerKey_Tapped(object _sender, EventArgs _e)
  {
    var bindingCtx = (OptionsPageViewModel)BindingContext;
    var serverKey = await DisplayPromptAsync("Server key:", "");
    if (serverKey == null) 
      return;

    bindingCtx.ServerKey = serverKey;
  }

  private async void MinimumInterval_Tapped(object _sender, EventArgs _e)
  {
    var bindingCtx = (OptionsPageViewModel)BindingContext;
    var mimimalIntervalRaw = await DisplayPromptAsync("Minimal interval:", "", keyboard: Keyboard.Numeric);
    if (mimimalIntervalRaw != null && int.TryParse(mimimalIntervalRaw, out var mimimalInterval))
      bindingCtx.MinimumTime = mimimalInterval;
  }

  private async void MinimumDistance_Tapped(object _sender, EventArgs _e)
  {
    var bindingCtx = (OptionsPageViewModel)BindingContext;
    var mimimalDistanceRaw = await DisplayPromptAsync("Minimal distance:", "", keyboard: Keyboard.Numeric);
    if (mimimalDistanceRaw != null && int.TryParse(mimimalDistanceRaw, out var mimimalDistance))
      bindingCtx.MinimumDistance = mimimalDistance;
  }

}