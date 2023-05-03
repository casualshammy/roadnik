using Android.Webkit;
using Java.Interop;

namespace Roadnik.MAUI.Toolkit;

public class JsBridge : Java.Lang.Object
{
  private readonly InteractableWebView p_webView;

  public JsBridge(InteractableWebView _webView)
  {
    this.p_webView = _webView;
  }

  [JavascriptInterface]
  [Export("invokeAction")]
  public void InvokeAction(string _data) => p_webView.InvokeAction(_data);
}
