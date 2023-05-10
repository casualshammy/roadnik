namespace Roadnik.MAUI.Toolkit;

public partial class InteractableWebView
{
  partial void ChangedHandler(object _sender)
  {
    if (_sender is not WebView { Handler.PlatformView: Android.Webkit.WebView nativeWebView })
      return;

    nativeWebView.Settings.JavaScriptEnabled = true;
    nativeWebView.Settings.UserAgentString += $" RoadnikApp/{AppInfo.Current.VersionString}";

    nativeWebView.SetWebChromeClient(new ConsoleWebChromeClient(this));
    nativeWebView.AddJavascriptInterface(new JsBridge(this), "jsBridge");
  }

  partial void ChangingHandler(object _sender, HandlerChangingEventArgs _e)
  {
    if (_e.OldHandler != null)
    {
      if (_sender is not WebView { Handler.PlatformView: Android.Webkit.WebView nativeWebView })
        return;

      nativeWebView.RemoveJavascriptInterface("jsBridge");
    }
  }
}
