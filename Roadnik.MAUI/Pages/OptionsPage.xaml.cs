using Roadnik.MAUI.Data;
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
    var serverName = await DisplayPromptAsync(
      "Server address", 
      null, 
      "Save", 
      placeholder: "http://example.com:5544/", 
      initialValue: bindingCtx.ServerName,
      keyboard: Keyboard.Url);

    if (serverName == null)
      return;

    bindingCtx.ServerName = serverName;
  }

  private async void ServerKey_Tapped(object _sender, EventArgs _e)
  {
    var bindingCtx = (OptionsPageViewModel)BindingContext;
    var serverKey = await DisplayPromptAsync(
      "Server key",
      "Alphanumeric characters, underscores, hyphens are allowed. Minimal length - 4 characters", 
      "Save", 
      initialValue: bindingCtx.ServerKey,
      maxLength: 16);

    if (serverKey == null)
      return;

    bindingCtx.ServerKey = serverKey;
  }

  private async void MinimumInterval_Tapped(object _sender, EventArgs _e)
  {
    var bindingCtx = (OptionsPageViewModel)BindingContext;
    var mimimalIntervalRaw = await DisplayPromptAsync(
      "Interval in seconds:", 
      "Minimal interval for anonymous user is 10 sec, for registered user is 1 sec",
      initialValue: bindingCtx.MinimumTime.ToString(),
      keyboard: Keyboard.Numeric);

    if (mimimalIntervalRaw != null && int.TryParse(mimimalIntervalRaw, out var mimimalInterval))
      bindingCtx.MinimumTime = mimimalInterval;
  }

  private async void MinimumDistance_Tapped(object _sender, EventArgs _e)
  {
    var bindingCtx = (OptionsPageViewModel)BindingContext;
    var mimimalDistanceRaw = await DisplayPromptAsync(
      "Distance in metres:", 
      null,
      initialValue: bindingCtx.MinimumDistance.ToString(),
      keyboard: Keyboard.Numeric);

    if (mimimalDistanceRaw != null && int.TryParse(mimimalDistanceRaw, out var mimimalDistance))
      bindingCtx.MinimumDistance = mimimalDistance;
  }

  private async void TrackpointReportingCondition_Tapped(object _sender, EventArgs _e)
  {
    var bindingCtx = (OptionsPageViewModel)BindingContext;

    var and = "Time AND distance";
    var or = "Time OR distance";
    var result = await DisplayActionSheet("Trackpoint reporting condition", null, null, and, or);
    if (result == null)
      return;

    if (result == and)
      bindingCtx.TrackpointReportingConditionText = TrackpointReportingConditionType.TimeAndDistance.ToString();
    else if (result == or)
      bindingCtx.TrackpointReportingConditionText = TrackpointReportingConditionType.TimeOrDistance.ToString();
  }

}