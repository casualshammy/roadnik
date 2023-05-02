using Newtonsoft.Json.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Roadnik.MAUI.Toolkit;

public partial class InteractableWebView : WebView
{
  private Subject<JToken> p_jsonDataFlow = new();

  public InteractableWebView() : base()
  {
    JsonData = p_jsonDataFlow
      .Publish()
      .RefCount();
  }

  public IObservable<JToken> JsonData { get; }

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
}
