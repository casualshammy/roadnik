using Android.Webkit;
using JustLogger.Interfaces;

namespace Roadnik.MAUI.Toolkit;

class ConsoleWebChromeClient : WebChromeClient
{
  private readonly InteractableWebView p_interactableWebView;
  private readonly ILogger? p_log;

  public ConsoleWebChromeClient(InteractableWebView _interactableWebView, ILogger? _log) : base()
  {
    p_interactableWebView = _interactableWebView;
    p_log = _log?["chrome-client"];
  }

  public override bool OnConsoleMessage(ConsoleMessage? _consoleMessage)
  {
    if (_consoleMessage is null)
      return true;

    var msgLevel = _consoleMessage.InvokeMessageLevel();
    var msg = _consoleMessage.Message();

    if (msgLevel != null && msg != null)
      p_interactableWebView.OnConsoleMsg(msgLevel.ToString(), msg);

    if (msg != null && msgLevel == ConsoleMessage.MessageLevel.Error)
      p_log?.Error(msg);

    return true;
  }
}
