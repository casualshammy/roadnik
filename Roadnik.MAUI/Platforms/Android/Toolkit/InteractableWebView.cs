using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Roadnik.Common.Toolkit;
using Roadnik.MAUI.Platforms.Android.Toolkit;
using System.Reflection;

namespace Roadnik.MAUI.Toolkit;

public partial class InteractableWebView
{
  partial void ChangedHandler(object _sender)
  {
    if (_sender is not WebView { Handler.PlatformView: Android.Webkit.WebView nativeWebView })
      return;

    var mauiWebView = (_sender as WebView)?.Handler?.PlatformView as MauiWebView;
    if (mauiWebView != null)
    {
      var client = mauiWebView.WebViewClient as MauiWebViewClient;
      if (client != null)
      {
        var handlerRef = typeof(MauiWebViewClient)
          .GetField("_handler", BindingFlags.NonPublic | BindingFlags.Instance)?
          .GetValue(client) as WeakReference<WebViewHandler?>;

        if (handlerRef == null)
          p_log?.Error($"Can't get the instance of {typeof(WebViewHandler)}: ref is null");
        else if (!handlerRef.TryGetTarget(out var handler))
          p_log?.Error($"Can't get the instance of {typeof(WebViewHandler)}: ref target is null");
        else
          nativeWebView.SetWebViewClient(new CachedMauiWebViewClient(handler, p_tilesCache, p_log, p_storage));
      }
    }

    nativeWebView.Settings.JavaScriptEnabled = true;
    nativeWebView.Settings.UserAgentString += $" {ReqResUtil.UserAgent}/{AppInfo.Current.VersionString}";

    nativeWebView.SetWebChromeClient(new ConsoleWebChromeClient(this, p_log));
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
