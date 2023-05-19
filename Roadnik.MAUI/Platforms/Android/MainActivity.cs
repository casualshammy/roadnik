using Android.App;
using Android.Content.PM;
using Android.OS;
using JustLogger.Interfaces;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.Modules.DeepLinksController;

namespace Roadnik.MAUI;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
  protected override void OnCreate(Bundle? _savedInstanceState)
  {
    base.OnCreate(_savedInstanceState);

    if (Microsoft.Maui.Controls.Application.Current is not IMauiApp app)
      return;

    var log = app.Container.Locate<ILogger>()["main-activity"];
    log.Info($"Main activity is started");

    var url = Intent?.Extras?.GetString(DeepLinksControllerImpl.AndroidExtraKey);
    if (string.IsNullOrWhiteSpace(url))
      return;

    app.Container.Locate<IDeepLinksController>().NewDeepLinkAsync(url);
  }
}
