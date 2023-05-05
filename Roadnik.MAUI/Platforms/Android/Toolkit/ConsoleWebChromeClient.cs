using Android.Webkit;

namespace Roadnik.MAUI.Toolkit;

class ConsoleWebChromeClient : WebChromeClient
{
  private readonly InteractableWebView p_interactableWebView;

  public ConsoleWebChromeClient(InteractableWebView _interactableWebView) : base()
  {
    p_interactableWebView = _interactableWebView;
  }

  public override bool OnConsoleMessage(ConsoleMessage? _consoleMessage)
  {
    if (_consoleMessage is null)
      return true;

    var msgLevel = _consoleMessage.InvokeMessageLevel();
    var msg = _consoleMessage.Message();

    if (msgLevel != null && msg != null)
      p_interactableWebView.OnConsoleMsg(msgLevel.ToString(), msg);

    return true;
  }
}
