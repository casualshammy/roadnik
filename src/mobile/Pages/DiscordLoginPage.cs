using Roadnik.MAUI.Controls;

namespace Roadnik.MAUI.Pages;

/// <summary>
/// Shows discord.com/login in a WebView.
/// After the user logs in, injects JS to extract the session token from localStorage
/// and completes the <see cref="WaitForTokenAsync"/> task.
/// </summary>
internal class DiscordLoginPage : ContentPage
{
  // Iframe trick: create an iframe to access the parent's localStorage from the injected JS context.
  // Returns the raw token string without surrounding JSON quotes.
  private const string JS_GET_TOKEN =
    "(function(){" +
    "var i=document.createElement('iframe');" +
    "document.body.appendChild(i);" +
    "var t=i.contentWindow.localStorage.token;" +
    "return t?t.replace(/^\"|\"$/g,''):null;" +
    "})()";

  private readonly WebView p_webView;
  private readonly Spinner p_spinner;
  private TaskCompletionSource<string?>? p_tokenTcs;

  public DiscordLoginPage()
  {
    Title = "Discord — Sign In";

    p_webView = new WebView
    {
      Source = "https://discord.com/login",
      VerticalOptions = LayoutOptions.Fill,
      HorizontalOptions = LayoutOptions.Fill,
    };
    p_webView.Navigated += OnNavigated;
    p_webView.Navigating += Navigating;

    p_spinner = new Spinner
    {
      IsVisible = false,
      VerticalOptions = LayoutOptions.Center,
      HorizontalOptions = LayoutOptions.Center,
    };

    Content = new Grid
    {
      Children = {
        p_webView,
        p_spinner
      }
    };
  }

  internal Task<string?> WaitForTokenAsync(CancellationToken _ct)
  {
    p_tokenTcs = new TaskCompletionSource<string?>();
    _ct.Register(() => p_tokenTcs.TrySetCanceled());
    return p_tokenTcs.Task;
  }

  private void Navigating(object? _sender, WebNavigatingEventArgs _e)
  {
    p_spinner.IsVisible = true;
  }

  private async void OnNavigated(object? _sender, WebNavigatedEventArgs _e)
  {
    p_spinner.IsVisible = false;

    // Discord redirects to /channels/... after a successful login
    var url = _e.Url ?? string.Empty;
    if (!url.Contains("discord.com/channels") && !url.Contains("discord.com/app"))
      return;

    try
    {
      var result = await p_webView.EvaluateJavaScriptAsync(JS_GET_TOKEN);

      // EvaluateJavaScriptAsync wraps string results in outer quotes
      if (!string.IsNullOrWhiteSpace(result) && result != "null")
      {
        var token = result.Trim('"');
        p_tokenTcs?.TrySetResult(token);
      }
    }
    catch (Exception ex)
    {
      p_tokenTcs?.TrySetException(ex);
    }
  }

}
