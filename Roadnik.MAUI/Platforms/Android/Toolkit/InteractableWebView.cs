namespace Roadnik.MAUI.Toolkit;

public partial class InteractableWebView
{
  partial void ChangedHandler(object _sender)
  {
    if (_sender is not WebView { Handler.PlatformView: Android.Webkit.WebView nativeWebView })
      return;

    //nativeWebView.SetWebViewClient(new JavascriptWebViewClient($"{p_javascriptFunction}"));
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
