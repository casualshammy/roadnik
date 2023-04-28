using Roadnik.Common.Toolkit;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.ViewModels;

namespace Roadnik.MAUI.Pages;

public partial class OptionsPage : ContentPage
{
  private readonly OptionsPageViewModel p_bindingCtx;

  public OptionsPage()
  {
    InitializeComponent();
    p_bindingCtx = (OptionsPageViewModel)BindingContext;
  }

  private async void ServerAddress_Tapped(object _sender, EventArgs _e)
  {
    var serverName = await DisplayPromptAsync(
      "Server address",
      null,
      "Save",
      placeholder: "http://example.com:5544/",
      initialValue: p_bindingCtx.ServerName,
      keyboard: Keyboard.Url);

    if (serverName == null)
      return;

    p_bindingCtx.ServerName = serverName;
  }

  private async void ServerKey_Tapped(object _sender, EventArgs _e)
  {
    var serverKey = await DisplayPromptAsync(
      "Server key",
      $"Only alphanumeric characters and hyphens are allowed. Minimum length - {ReqResUtil.MinKeyKength} characters, maximum - {ReqResUtil.MaxKeyKength} characters",
      "Save",
      initialValue: p_bindingCtx.ServerKey,
      maxLength: ReqResUtil.MaxKeyKength);

    if (serverKey == null)
      return;

    p_bindingCtx.ServerKey = serverKey;
  }

  private async void MinimumInterval_Tapped(object _sender, EventArgs _e)
  {
    var mimimalIntervalRaw = await DisplayPromptAsync(
      "Interval in seconds:",
      "Minimum interval for anonymous user is 10 sec, for registered user is 1 sec. Maximum interval is 1 hour (3600 sec)",
      initialValue: p_bindingCtx.MinimumTime.ToString(),
      keyboard: Keyboard.Numeric);

    if (mimimalIntervalRaw != null &&
      int.TryParse(mimimalIntervalRaw, out var mimimalInterval) &&
      mimimalInterval >= 1 &&
      mimimalInterval <= 3600)
      p_bindingCtx.MinimumTime = mimimalInterval;
  }

  private async void MinimumDistance_Tapped(object _sender, EventArgs _e)
  {
    var mimimalDistanceRaw = await DisplayPromptAsync(
      "Distance in metres:",
      "0 to disable limit. Maximum value - 10 km (10000 metres)",
      initialValue: p_bindingCtx.MinimumDistance.ToString(),
      keyboard: Keyboard.Numeric);

    if (mimimalDistanceRaw != null &&
      int.TryParse(mimimalDistanceRaw, out var mimimalDistance) &&
      mimimalDistance <= 10000)
      p_bindingCtx.MinimumDistance = mimimalDistance;
  }

  private async void TrackpointReportingCondition_Tapped(object _sender, EventArgs _e)
  {
    var and = "Time AND distance";
    var or = "Time OR distance";
    var result = await DisplayActionSheet("Trackpoint reporting condition", null, null, and, or);
    if (result == null)
      return;

    if (result == and)
      p_bindingCtx.TrackpointReportingConditionText = TrackpointReportingConditionType.TimeAndDistance.ToString();
    else if (result == or)
      p_bindingCtx.TrackpointReportingConditionText = TrackpointReportingConditionType.TimeOrDistance.ToString();
  }

  private async void MinAccuracy_Tapped(object _sender, EventArgs _e)
  {
    var minAccuracyRaw = await DisplayPromptAsync(
      "Accuracy in metres:",
      "Minimum value - 1 meter. Sane value is between 10 and 30 metres",
      initialValue: p_bindingCtx.MinAccuracy.ToString(),
      keyboard: Keyboard.Numeric);

    if (minAccuracyRaw == null)
      return;
    if (!int.TryParse(minAccuracyRaw, out var minAccuracy))
      return;
    if (minAccuracy < 1)
      minAccuracy = 1;
    if (minAccuracy > 1000)
      minAccuracy = 1000;

    p_bindingCtx.MinAccuracy = minAccuracy;
  }

}