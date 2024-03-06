using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.Modules.DeepLinksController;
using System.Text.Json;

namespace Roadnik.MAUI;

[Activity(
  Theme = "@style/Maui.SplashTheme",
  MainLauncher = true,
  LaunchMode = LaunchMode.SingleInstance,
  ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
  private readonly IPushMessagesController? p_pushMsgCtrl;

  public MainActivity()
  {
    if (Microsoft.Maui.Controls.Application.Current is IMauiApp app)
      p_pushMsgCtrl = app.Container.Locate<IPushMessagesController>();
  }

  protected override void OnCreate(Bundle? _savedInstanceState)
  {
    base.OnCreate(_savedInstanceState);

    if (Microsoft.Maui.Controls.Application.Current is not IMauiApp app)
      return;

    var log = app.Container.Locate<ILog>()["main-activity"];
    log.Info($"Main activity is started");

    var pushMsg = Intent?.GetStringExtra("push-msg");
    if (!pushMsg.IsNullOrEmpty())
    {
      var @event = JsonSerializer.Deserialize<PushNotificationEvent>(pushMsg);
      if (@event != null)
        p_pushMsgCtrl?.AddPushMsg(@event);
    }

    var url = Intent?.Extras?.GetString(DeepLinksControllerImpl.AndroidExtraKey);
    if (!string.IsNullOrWhiteSpace(url))
      app.Container.Locate<IDeepLinksController>().NewDeepLinkAsync(url);
  }

  protected override void OnNewIntent(Intent? _intent)
  {
    base.OnNewIntent(_intent);

    var pushMsg = _intent?.GetStringExtra("push-msg");
    if (!pushMsg.IsNullOrEmpty())
    {
      var @event = JsonSerializer.Deserialize<PushNotificationEvent>(pushMsg);
      if (@event != null)
        p_pushMsgCtrl?.AddPushMsg(@event);
    }
  }
}
