using Newtonsoft.Json.Linq;
using Roadnik.MAUI.Data;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Roadnik.MAUI.Toolkit;

public partial class InteractableWebView : WebView
{
  private Subject<JsToCSharpMsg> p_jsonDataFlow = new();
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

  public IObservable<JsToCSharpMsg> JsonData { get; }
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
      {
        var msg = jToken.ToObject<JsToCSharpMsg>();
        if (msg != null)
          p_jsonDataFlow.OnNext(msg);
      }
    }
    catch { }
  }

  public void OnConsoleMsg(string _msgLevel, string _msg) => p_consoleMsgFlow.OnNext((_msgLevel, _msg));

}
