using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.AppCompat.App;
using Roadnik.MAUI.Interfaces;

namespace Roadnik.MAUI;

[Activity(Exported = true)]
[IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.ActionView, Intent.CategoryDefault, Intent.CategoryBrowsable },
  DataScheme = "https", DataHost = "roadnik.app", DataPathPattern = "/r/")]
public class DeepLinkActivity : AppCompatActivity
{
  protected override void OnCreate(Bundle? _savedInstanceState)
  {
    base.OnCreate(_savedInstanceState);

    try
    {
      var url = Intent?.DataString;
      if (string.IsNullOrWhiteSpace(url))
        return;

      if (Microsoft.Maui.Controls.Application.Current is not IMauiApp app)
        return;

      app.Container.Locate<IDeepLinksController>().NewDeepLinkAsync(url);
    }
    finally
    {
      MainThread.BeginInvokeOnMainThread(Finish);
    }
  }
}