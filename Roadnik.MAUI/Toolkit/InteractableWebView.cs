using Newtonsoft.Json.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Roadnik.MAUI.Toolkit;

public partial class InteractableWebView : WebView
{
  private Subject<JToken> p_jsonDataFlow = new();
  private Subject<(string MsgLevel, string Msg)> p_consoleMsgFlow = new();

  public InteractableWebView() : base()
  {
    JsonData = p_jsonDataFlow
      .Publish()
      .RefCount();

    ConsoleMsg = p_consoleMsgFlow
      .Publish()
      .RefCount();
  }

  public IObservable<JToken> JsonData { get; }
  public IObservable<(string MsgLevel, string Msg)> ConsoleMsg { get; }

  partial void ChangedHandler(object _sender);
  partial void ChangingHandler(object _sender, HandlerChangingEventArgs _e);

  protected override void OnHandlerChanging(HandlerChangingEventArgs _args)
  {
    base.OnHandlerChanging(_args);
    ChangingHandler(this, _args);
  }

  protected override void OnHandlerChanged()
  {
    base.OnHandlerChanged();
    ChangedHandler(this);
  }

  public void InvokeAction(string _data)
  {
    try
    {
      var jToken = JToken.Parse(_data);
      if (jToken != null)
        p_jsonDataFlow.OnNext(jToken);
    }
    catch { }
  }

  public void OnConsoleMsg(string _msgLevel, string _msg) => p_consoleMsgFlow.OnNext((_msgLevel, _msg));

}
