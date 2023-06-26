using Android.OS;
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

#pragma warning disable CA1416 // Validate platform compatibility
    if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
    {
      var mauiWebView = (_sender as WebView)?.Handler?.PlatformView as MauiWebView;
      if (mauiWebView != null)
      {
        var client = mauiWebView.WebViewClient as MauiWebViewClient;
        if (client != null)
        {
          var handler = typeof(MauiWebViewClient)
            .GetField("_handler", BindingFlags.NonPublic | BindingFlags.Instance)?
            .GetValue(client) as WebViewHandler;

          if (handler != null)
            nativeWebView.SetWebViewClient(new CachedMauiWebViewClient(handler, p_tilesCache, p_log, p_storage));
        }
      }
    }
#pragma warning restore CA1416 // Validate platform compatibility

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
