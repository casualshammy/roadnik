using Android.App;
using Android.Content.PM;
using Android.OS;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.Modules.DeepLinksController;

namespace Roadnik.MAUI;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
  protected override void OnCreate(Bundle? _savedInstanceState)
  {
    Console.WriteLine($"MainActivity is started");
    base.OnCreate(_savedInstanceState);

    var url = Intent?.Extras?.GetString(DeepLinksControllerImpl.AndroidExtraKey);
    if (string.IsNullOrWhiteSpace(url))
      return;

    if (Microsoft.Maui.Controls.Application.Current is not IMauiApp app)
      return;

    app.Container.Locate<IDeepLinksController>().NewDeepLinkAsync(url);
  }
}
