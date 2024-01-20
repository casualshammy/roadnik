using Android.App;
using Android.Content;
using Android.OS;
using Roadnik.MAUI.Interfaces;

namespace Roadnik.MAUI;

[Activity(Exported = true)]
[IntentFilter(
  [Intent.ActionView], 
  Categories = [Intent.ActionView, Intent.CategoryDefault, Intent.CategoryBrowsable],
  DataScheme = "https", 
  DataHost = "roadnik.app", 
  DataPathPattern = "/r/", 
  AutoVerify = true)]
public class DeepLinkActivity : Activity
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
      var intent = new Intent(this, typeof(MainActivity));
      StartActivity(intent);
      MainThread.BeginInvokeOnMainThread(Finish);
    }
  }
}