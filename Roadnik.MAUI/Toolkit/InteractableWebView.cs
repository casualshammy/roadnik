﻿using JustLogger.Interfaces;
using Newtonsoft.Json.Linq;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Roadnik.MAUI.Toolkit;

public partial class InteractableWebView : WebView
{
  private readonly ITilesCache? p_tilesCache;
  private readonly ILogger? p_log;
  private readonly IPreferencesStorage? p_storage;
  private readonly Subject<JsToCSharpMsg> p_jsonDataFlow = new();
  private readonly Subject<(string MsgLevel, string Msg)> p_consoleMsgFlow = new();
  
  public InteractableWebView() : base()
  {
    var cMauiApp = Application.Current as CMauiApplication;
    p_tilesCache = cMauiApp?.Container.Locate<ITilesCache>();
    p_log = cMauiApp?.Container.Locate<ILogger>()["interactable-web-view"];
    p_storage = cMauiApp?.Container.Locate<IPreferencesStorage>();

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
